import * as vscode from "vscode";
import * as fs from "fs";
import * as path from "path";
import { AdapterLaunchError, AdapterProcess, launchAdapter } from "./adapterProcess";
import { DebuggeeTerminal } from "./debuggeeTerminal";

export function activate(context: vscode.ExtensionContext) {
	console.log("PlusPim Extension was loaded.");

	// 情報を設定
	const factory = new PlusPimDescriptorFactory(context);
	context.subscriptions.push(
		vscode.debug.registerDebugAdapterDescriptorFactory("pluspim", factory),
		// 無効化されたときに実行中の PlusPim を終了する
		factory,
		vscode.debug.onDidTerminateDebugSession((session) => {
			if (session.type === "pluspim") {
				factory.onSessionTerminated(session);
			}
		})
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
			}
		}));
}

export function deactivate() { }

// 実行権限がなければ付与する (VSIXをWindowsで作ると実行ビットが落ちるため)
// 失敗時は理由を文字列で返す．成功時は undefined
function ensureExecutable(binPath: string): string | undefined {
	if (process.platform === "win32") { return undefined; }
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


/** 1回の起動で使う端末と PlusPim のプロセス */
interface SessionRun {
	sessionId: string;
	terminal: vscode.Terminal;
	pty: DebuggeeTerminal;
	adapter?: AdapterProcess;
}

/** セッションの終了後，PlusPim が自分で終了するのを待つ時間 */
const EXIT_GRACE_MS = 3000;
/** PlusPim が待ち受けのポートを通知するまで待つ時間 */
const HANDSHAKE_TIMEOUT_MS = 15000;

class PlusPimDescriptorFactory implements vscode.DebugAdapterDescriptorFactory, vscode.Disposable {
	// 再起動では同じセッション ID で再び呼ばれることがあるため，起動ごとの番号で管理する
	private readonly runs = new Map<number, SessionRun>();
	private nextRunId = 0;

	constructor(private readonly context: vscode.ExtensionContext) { }

	async createDebugAdapterDescriptor(
		session: vscode.DebugSession
	): Promise<vscode.DebugAdapterDescriptor> {
		// 明示されたときだけ固定のポート．既定は 0 (OS が空いているポートを選ぶ)
		const port = Number(session.configuration.port ?? 0);
		// vscode.DebugConfigurationの[key: string]: any
		// Normalize program paths defensively
		const programInput = session.configuration.program;
		const programs: string[] = (Array.isArray(programInput) ? programInput : [programInput])
			.filter(p => p !== null && p !== undefined)
			.map(p => String(p));

		const extraArgsInput: any[] = session.configuration.args ?? [];
		const extraArgs: string[] = extraArgsInput
			.filter(a => a !== null && a !== undefined)
			.map(a => String(a));

		const rid = process.platform === "win32" ? "win-x64" : "linux-x64";
		const exe = process.platform === "win32" ? "PlusPim.exe" : "PlusPim";
		// 開発モードのときだけ開発用(dotnet build)を優先，なければリリース用(dotnet publish)にフォールバック
		const preferDebug = this.context.extensionMode === vscode.ExtensionMode.Development;
		const debugBinPath = this.context.asAbsolutePath(`bin/debug/${exe}`);
		const releaseBinPath = this.context.asAbsolutePath(`bin/${rid}/${exe}`);
		const useDebug = preferDebug && fs.existsSync(debugBinPath);
		if (preferDebug && !useDebug) {
			vscode.window.showWarningMessage("Debugビルドが見つからないため，リリースビルドを使用します．リポジトリのルートで `dotnet build` を実行するとビルドできます．");
		}
		const binPath = useDebug ? debugBinPath : releaseBinPath;
		const execError = ensureExecutable(binPath);
		if (execError) {
			vscode.window.showErrorMessage(execError);
			throw new Error(execError);
		}

		// 終了したセッションの端末は，新しいセッションを始めるときに閉じる
		for (const [id, old] of this.runs) {
			if (old.pty.hasExited) {
				old.terminal.dispose();
				this.runs.delete(id);
			}
		}

		// PlusPim の標準入出力を表示する端末．プロセスは拡張機能が持つ
		const runId = this.nextRunId++;
		const pty = new DebuggeeTerminal();
		const terminal = vscode.window.createTerminal({
			name: `Debug: ${programs.length > 0 ? programs.map(p => path.basename(p)).join(", ") : "PlusPim"}`,
			pty,
			// 疑似端末はウィンドウの再読み込みで復元できない
			isTransient: true,
		});
		const run: SessionRun = { sessionId: session.id, terminal, pty };
		this.runs.set(runId, run);
		pty.onUserClosed.event(() => this.runs.delete(runId));
		terminal.show();

		try {
			const adapter = await launchAdapter({
				binPath,
				args: ["-d", "--port", String(port), ...extraArgs, ...programs],
				cwd: session.workspaceFolder?.uri.fsPath,
				timeoutMs: HANDSHAKE_TIMEOUT_MS,
				onStdout: t => pty.write(t),
				onStderr: t => pty.write(t),
			});
			run.adapter = adapter;
			const stdin = adapter.child.stdin;
			pty.attach({
				writeStdin: t => { if (stdin.writable) { stdin.write(t); } },
				endStdin: () => stdin.end(),
				kill: () => adapter.child.kill(),
			});
			void adapter.exited.then(code => pty.markExited(code));
			if (pty.isClosed) {
				// 起動を待つ間に端末が閉じられた
				adapter.child.kill();
				throw new AdapterLaunchError("The PlusPim terminal was closed.");
			}
			// 待ち受けは IPv4 のみ．localhost は ::1 に解決されうるため 127.0.0.1 を明示する
			return new vscode.DebugAdapterServer(adapter.port, "127.0.0.1");
		} catch (e) {
			const msg = e instanceof Error ? e.message : String(e);
			pty.write(`\n${msg}\n`);
			pty.markExited(null);
			vscode.window.showErrorMessage(msg);
			throw e;
		}
	}

	/** セッションの終了後，PlusPim が終了しなければ強制終了する */
	onSessionTerminated(session: vscode.DebugSession): void {
		for (const run of this.runs.values()) {
			const adapter = run.adapter;
			if (run.sessionId !== session.id || !adapter || run.pty.hasExited) { continue; }
			// PlusPim は disconnect の後に自分で終了する．終了しないときだけ強制終了する
			const timer = setTimeout(() => adapter.child.kill(), EXIT_GRACE_MS);
			void adapter.exited.then(() => clearTimeout(timer));
		}
	}

	dispose(): void {
		for (const run of this.runs.values()) {
			run.adapter?.child.kill();
		}
		this.runs.clear();
	}
}

class PlusPimTracker implements vscode.DebugAdapterTracker {
	private output: vscode.OutputChannel;

	constructor(output: vscode.OutputChannel) {
		this.output = output;
	}

	// VSCode → DA
	onWillReceiveMessage(message: any): void {
		this.output.appendLine(`>>> ${message.type}/${message.command ?? message.event ?? ""}`);
		this.output.appendLine(JSON.stringify(message, null, 2));
		this.output.appendLine("");
	}

	// DA → VSCode
	onDidSendMessage(message: any): void {
		this.output.appendLine(`<<< ${message.type}/${message.command ?? message.event ?? ""}`);
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