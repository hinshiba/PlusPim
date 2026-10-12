import * as fs from "node:fs";
import * as net from "node:net";
import * as path from "node:path";
import * as vscode from "vscode";
import {
	DapExpansionSource,
	PseudoExpansionHintsProvider,
} from "./inlayHints.js";
import { SocketDebugAdapter } from "./pluspimAdapter.js";

export function activate(context: vscode.ExtensionContext) {
	console.log("PlusPim Extension was loaded.");

	// 情報を設定
	const factory = new PlusPimDescriptorFactory(context);
	context.subscriptions.push(
		factory,
		vscode.debug.registerDebugAdapterDescriptorFactory("pluspim", factory),
	);
	context.subscriptions.push(
		vscode.debug.onDidTerminateDebugSession((session) => {
			if (session.type === "pluspim") {
				factory.endSession(session.id);
			}
		}),
		vscode.window.onDidCloseTerminal((terminal) =>
			factory.onTerminalClosed(terminal),
		),
	);

	// 疑似命令の展開先のインレイヒント．デバッグセッション中だけ表示する
	const expansions = new DapExpansionSource();
	const hints = new PseudoExpansionHintsProvider(expansions);
	context.subscriptions.push(
		vscode.languages.registerInlayHintsProvider({ language: "mips" }, hints),
		vscode.debug.onDidTerminateDebugSession((session) => {
			if (session.type === "pluspim") {
				expansions.forget(session.id);
				hints.invalidate();
			}
		}),
		vscode.debug.onDidChangeActiveDebugSession(() => hints.invalidate()),
		vscode.workspace.onDidChangeConfiguration((e) => {
			if (e.affectsConfiguration("pluspim.inlayHints")) {
				hints.invalidate();
			}
		}),
		// プログラムは launch でアセンブルされるため，その応答の後にヒントを取り直す
		// trace の有無にかかわらず必要なので，trace 用のトラッカーとは別に登録する
		vscode.debug.registerDebugAdapterTrackerFactory("pluspim", {
			createDebugAdapterTracker(session) {
				return {
					onDidSendMessage(message: DapMessage) {
						if (
							message.type === "response" &&
							message.command === "launch" &&
							message.success
						) {
							expansions.markReady(session.id);
							hints.invalidate();
						}
					},
				};
			},
		}),
	);

	const output = vscode.window.createOutputChannel("PlusPim DAP Trace");
	context.subscriptions.push(output);
	context.subscriptions.push(
		vscode.debug.registerDebugAdapterTrackerFactory("pluspim", {
			createDebugAdapterTracker(session) {
				// trace有効時のみ
				if (session.configuration.trace) {
					output.show(true);
					return new PlusPimTracker(output);
				}
				return undefined; // トラッキングしない
			},
		}),
	);
}

export function deactivate() {}

// Linux 向けに実行権限がなければ付与する
// 失敗時は理由を文字列で返す．成功時は undefined
function ensureExecutable(binPath: string): string | undefined {
	if (process.platform === "win32") {
		return undefined;
	}
	try {
		fs.accessSync(binPath, fs.constants.X_OK);
		return undefined;
	} catch (e) {
		if ((e as NodeJS.ErrnoException).code === "ENOENT") {
			return `PlusPim binary not found: ${binPath}`;
		}
	}
	try {
		fs.chmodSync(binPath, 0o755);
		return undefined;
	} catch {
		return `PlusPim binary is not executable and could not be fixed automatically. Run: chmod +x "${binPath}"`;
	}
}

/** 1つのセッションが持つ資源 */
interface SessionResources {
	terminal: vscode.Terminal;
	/** PlusPim の接続を待つサーバ．最初の接続を受けたら閉じる */
	server?: net.Server | undefined;
	/** PlusPim との接続 */
	socket?: net.Socket | undefined;
}

class PlusPimDescriptorFactory
	implements vscode.DebugAdapterDescriptorFactory, vscode.Disposable
{
	/** セッション ID ごとの資源 */
	private readonly sessions = new Map<string, SessionResources>();
	/** 閉じられていないターミナル．セッションが終わっても残し，拡張の終了時にだけ破棄する */
	private readonly terminals = new Set<vscode.Terminal>();

	constructor(private readonly context: vscode.ExtensionContext) {}

	async createDebugAdapterDescriptor(
		session: vscode.DebugSession,
	): Promise<vscode.DebugAdapterDescriptor> {
		// vscode.DebugConfigurationの[key: string]: any
		// Normalize program paths defensively
		const programInput = session.configuration.program;
		const programs: string[] = (
			Array.isArray(programInput) ? programInput : [programInput]
		)
			.filter((p) => p !== null && p !== undefined)
			.map((p) => String(p));

		const extraArgsInput: unknown[] = session.configuration.args ?? [];
		const extraArgs: string[] = extraArgsInput
			.filter((a) => a !== null && a !== undefined)
			.map((a) => String(a));

		const rid = process.platform === "win32" ? "win-x64" : "linux-x64";
		const exe = process.platform === "win32" ? "PlusPim.exe" : "PlusPim";
		// 開発モードのときだけ開発ビルドを優先
		const preferDebug =
			this.context.extensionMode === vscode.ExtensionMode.Development;
		const debugBinPath = this.context.asAbsolutePath(`bin/debug/${exe}`);
		const releaseBinPath = this.context.asAbsolutePath(`bin/${rid}/${exe}`);
		const useDebug = preferDebug && fs.existsSync(debugBinPath);
		if (preferDebug && !useDebug) {
			vscode.window.showWarningMessage(
				"The extension was launched in development mode, but no debug build was found. Using the release build instead.",
			);
		}
		const binPath = useDebug ? debugBinPath : releaseBinPath;
		const execError = ensureExecutable(binPath);
		if (execError) {
			vscode.window.showErrorMessage(execError);
			throw new Error(execError);
		}

		// 先に待ち受けて，OS が決めたポートを PlusPim に渡す
		const server = net.createServer();
		const port = await listenOnLoopback(server);

		// ターミナルで呼んでもらう
		const name = `Debug: ${programs.length > 0 ? programs.map((p) => path.basename(p)).join(", ") : "PlusPim"}`;
		const args = [
			"-d",
			"--connect",
			`127.0.0.1:${port}`,
			...extraArgs,
			...programs,
		];
		const terminal = vscode.window.createTerminal({
			name,
			shellPath: binPath,
			shellArgs: args,
		});
		const resources: SessionResources = { terminal, server };
		this.sessions.set(session.id, resources);
		this.terminals.add(terminal);
		terminal.show();

		try {
			// 最初に来た接続を DAP の接続とする
			const socket = await acceptFirstConnection(
				server,
				terminal,
				name,
				CONNECT_TIMEOUT_MS,
			);
			server.close();
			resources.server = undefined;
			resources.socket = socket;
			return new vscode.DebugAdapterInlineImplementation(
				new SocketDebugAdapter(socket, () => this.endSession(session.id)),
			);
		} catch (e) {
			// 記述子の作成に失敗すると，終了のイベントが来ないことがあるので，ここで片付ける
			this.endSession(session.id);
			throw e;
		}
	}

	/** セッションの待ち受けと接続を閉じる．ターミナルは出力を読めるように残す */
	endSession(sessionId: string): void {
		const resources = this.sessions.get(sessionId);
		if (resources === undefined) {
			return;
		}
		this.sessions.delete(sessionId);
		resources.server?.close();
		resources.socket?.destroy();
	}

	onTerminalClosed(terminal: vscode.Terminal): void {
		this.terminals.delete(terminal);
	}

	/** 拡張の終了時にだけ呼ばれる．すべての資源とターミナルを破棄する */
	dispose(): void {
		for (const sessionId of [...this.sessions.keys()]) {
			this.endSession(sessionId);
		}
		for (const terminal of this.terminals) {
			terminal.dispose();
		}
		this.terminals.clear();
	}
}

/** PlusPim が接続してくるのを待つ時間 */
const CONNECT_TIMEOUT_MS = 5000;

/** ループバックの空きポートで待ち受け，そのポート番号を返す */
function listenOnLoopback(server: net.Server): Promise<number> {
	return new Promise((resolve, reject) => {
		server.once("error", reject);
		server.listen({ host: "127.0.0.1", port: 0 }, () => {
			server.off("error", reject);
			const address = server.address();
			if (address === null || typeof address === "string") {
				server.close();
				reject(new Error("Failed to get the port to listen on"));
				return;
			}
			resolve(address.port);
		});
	});
}

/**
 * 最初に来た接続を返す．PlusPim が時間内に接続しない場合と，接続前にターミナルが閉じた場合は失敗する．
 * 失敗は例外で伝え，通知の表示は VS Code に任せる
 */
function acceptFirstConnection(
	server: net.Server,
	terminal: vscode.Terminal,
	terminalName: string,
	timeoutMs: number,
): Promise<net.Socket> {
	return new Promise((resolve, reject) => {
		const cleanup = () => {
			clearTimeout(timer);
			closeListener.dispose();
			server.off("connection", onConnection);
			server.off("error", onError);
		};
		const onConnection = (socket: net.Socket) => {
			cleanup();
			// 受け取るまでの間に error が起きても拡張が落ちないようにする
			socket.on("error", () => {});
			resolve(socket);
		};
		const onError = (e: Error) => {
			cleanup();
			reject(e);
		};
		const timer = setTimeout(() => {
			cleanup();
			reject(
				new Error(
					`PlusPim did not connect within ${timeoutMs}ms. See the terminal "${terminalName}" for details.`,
				),
			);
		}, timeoutMs);
		const closeListener = vscode.window.onDidCloseTerminal((closed) => {
			if (closed === terminal) {
				cleanup();
				reject(new Error("PlusPim exited before connecting."));
			}
		});
		server.on("connection", onConnection);
		server.on("error", onError);
	});
}

/** トレース出力に必要な DAP メッセージの最小形 */
interface DapMessage {
	type?: string;
	command?: string;
	event?: string;
	success?: boolean;
}

class PlusPimTracker implements vscode.DebugAdapterTracker {
	private output: vscode.OutputChannel;

	constructor(output: vscode.OutputChannel) {
		this.output = output;
	}

	// VSCode → DA
	onWillReceiveMessage(message: DapMessage): void {
		this.output.appendLine(
			`>>> ${message.type}/${message.command ?? message.event ?? ""}`,
		);
		this.output.appendLine(JSON.stringify(message, null, 2));
		this.output.appendLine("");
	}

	// DA → VSCode
	onDidSendMessage(message: DapMessage): void {
		this.output.appendLine(
			`<<< ${message.type}/${message.command ?? message.event ?? ""}`,
		);
		this.output.appendLine(JSON.stringify(message, null, 2));
		this.output.appendLine("");
	}

	onError(error: Error): void {
		this.output.appendLine(`!!! Error: ${error.message}`);
	}

	onExit(code: number | undefined, signal: string | undefined): void {
		this.output.appendLine(`--- DA exited (code=${code}, signal=${signal})`);
	}
}
