import type * as net from "node:net";
import * as vscode from "vscode";
import { resolveBinary } from "./binaryLayout.js";
import { establish } from "./connection.js";
import {
	DapExpansionSource,
	PseudoExpansionHintsProvider,
} from "./inlayHints.js";
import { parseLaunchConfiguration } from "./launchConfig.js";
import { SocketDebugAdapter } from "./pluspimAdapter.js";

export function activate(context: vscode.ExtensionContext) {
	console.log("PlusPim Extension was loaded.");

	const log = vscode.window.createOutputChannel("PlusPim", { log: true });
	const factory = new PlusPimDescriptorFactory(context, log);
	context.subscriptions.push(
		log,
		factory,
		vscode.debug.registerDebugAdapterDescriptorFactory("pluspim", factory),
		vscode.debug.onDidTerminateDebugSession((session) => {
			if (session.type === "pluspim") {
				factory.endSession(session.id);
			}
		}),
		vscode.window.onDidCloseTerminal((terminal) =>
			factory.onTerminalClosed(terminal),
		),
	);

	// 疑似命令の展開先のインレイヒントをデバッグセッション中だけ表示
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
							message.success === true
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
	context.subscriptions.push(
		output,
		vscode.debug.registerDebugAdapterTrackerFactory("pluspim", {
			createDebugAdapterTracker(session) {
				return session.configuration.trace === true
					? new PlusPimTracker(output)
					: undefined;
			},
		}),
	);
}

export function deactivate() {}

/** PlusPim が接続してくるのを待つ時間 */
const CONNECT_TIMEOUT_MS = 5000;

class PlusPimDescriptorFactory
	implements vscode.DebugAdapterDescriptorFactory, vscode.Disposable
{
	/** 接続 ID ごとの，確立中なら中止の手段，確立後ならその接続 */
	private readonly sessions = new Map<string, AbortController | net.Socket>();
	/** 閉じられていないターミナルと，その終了を伝える関数．セッションが終わっても残し，拡張の終了時にだけ破棄する */
	private readonly terminals = new Map<vscode.Terminal, () => void>();

	constructor(
		private readonly context: vscode.ExtensionContext,
		private readonly log: vscode.LogOutputChannel,
	) {}

	async createDebugAdapterDescriptor(
		session: vscode.DebugSession,
	): Promise<vscode.DebugAdapterDescriptor> {
		const abort = new AbortController();
		this.sessions.set(session.id, abort);
		try {
			const config = parseLaunchConfiguration(session.configuration);
			// 開発モードのときだけ開発ビルドを優先
			const binary = resolveBinary(
				this.context.extensionPath,
				this.context.extensionMode === vscode.ExtensionMode.Development,
			);
			if (binary.fellBack) {
				vscode.window.showWarningMessage(
					"The extension was launched in development mode, but no debug build was found. Using the release build instead.",
				);
			}
			const socket = await establish({
				binPath: binary.path,
				config,
				spawn: (binPath, args, name) => this.spawn(binPath, args, name),
				signal: abort.signal,
				timeoutMs: CONNECT_TIMEOUT_MS,
			});
			// establish の解決からここまではマイクロタスクだけなので，その間に endSession は割り込まない
			this.sessions.set(session.id, socket);
			return new vscode.DebugAdapterInlineImplementation(
				new SocketDebugAdapter(socket, () => this.endSession(session.id)),
			);
		} catch (e) {
			// 記述子の作成に失敗すると終了のイベントが来ないことがある．通知の表示は VS Code に任せる
			this.sessions.delete(session.id);
			this.log.error(
				`Failed to connect to PlusPim (${session.name}):`,
				e instanceof Error ? e.message : String(e),
			);
			throw e;
		}
	}

	/** ターミナルで PlusPim を起動する */
	private spawn(
		binPath: string,
		args: readonly string[],
		name: string,
	): Promise<void> {
		this.log.info(`Launching: ${binPath} ${args.join(" ")}`);
		const terminal = vscode.window.createTerminal({
			name,
			shellPath: binPath,
			shellArgs: [...args],
		});
		const exited = new Promise<void>((resolve) => {
			this.terminals.set(terminal, resolve);
		});
		terminal.show();
		return exited;
	}

	/** 確立中なら中止し，確立後なら接続を閉じる．ターミナルは出力を読めるように残す */
	endSession(sessionId: string): void {
		const entry = this.sessions.get(sessionId);
		this.sessions.delete(sessionId);
		if (entry instanceof AbortController) {
			entry.abort();
		} else {
			entry?.destroy();
		}
	}

	onTerminalClosed(terminal: vscode.Terminal): void {
		this.terminals.get(terminal)?.();
		this.terminals.delete(terminal);
	}

	/** 拡張の終了時にだけ呼ばれる．すべての接続とターミナルを破棄する */
	dispose(): void {
		for (const sessionId of this.sessions.keys()) {
			this.endSession(sessionId);
		}
		for (const terminal of this.terminals.keys()) {
			terminal.dispose();
		}
		this.terminals.clear();
	}
}

/** トレース出力に必要な DAP メッセージの最小形 */
interface DapMessage {
	type?: string;
	command?: string;
	event?: string;
	success?: boolean;
}

class PlusPimTracker implements vscode.DebugAdapterTracker {
	private readonly output: vscode.OutputChannel;

	/** trace を有効にした起動でだけ作る．作ると出力を表示する */
	constructor(output: vscode.OutputChannel) {
		this.output = output;
		output.show(true);
	}

	// VS Code -> DA
	onWillReceiveMessage(message: DapMessage): void {
		this.output.appendLine(
			`>>> ${message.type}/${message.command ?? message.event ?? ""}`,
		);
		this.output.appendLine(JSON.stringify(message, null, 2));
		this.output.appendLine("");
	}

	// VS Code <- DA
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
