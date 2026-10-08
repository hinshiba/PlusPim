import * as vscode from "vscode";
import * as fs from "fs";
import * as net from "net";
import * as path from "path";

export function activate(context: vscode.ExtensionContext) {
	console.log("PlusPim Extension was loaded.");

	// 情報を設定
	const factory = new PlusPimDescriptorFactory(context);
	context.subscriptions.push(
		vscode.debug.registerDebugAdapterDescriptorFactory("pluspim", factory)
	);
	context.subscriptions.push(
		vscode.debug.onDidTerminateDebugSession((session) => {
			if (session.type === "pluspim") {
				factory.dispose();
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


class PlusPimDescriptorFactory implements vscode.DebugAdapterDescriptorFactory {
	private terminal: vscode.Terminal | undefined;

	constructor(private readonly context: vscode.ExtensionContext) { }

	async createDebugAdapterDescriptor(
		session: vscode.DebugSession
	): Promise<vscode.DebugAdapterDescriptor> {
		const port = session.configuration.port ?? 4711;
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
			vscode.window.showWarningMessage("Debugビルドが見つからないため，リリースビルドを使用します．`bun run dotnet:debug:win` でビルドできます．");
		}
		const binPath = useDebug ? debugBinPath : releaseBinPath;
		const execError = ensureExecutable(binPath);
		if (execError) {
			vscode.window.showErrorMessage(execError);
			throw new Error(execError);
		}

		// ターミナルで呼んでもらう
		const args = ["-d", "--port", String(port), ...extraArgs, ...programs];
		this.terminal = vscode.window.createTerminal({
			name: `Debug: ${programs.length > 0 ? programs.map(p => path.basename(p)).join(", ") : "PlusPim"}`,
			shellPath: binPath,
			shellArgs: args,
		});
		this.terminal.show();

		await waitForPort(port, 5000);

		// TCPであることも設定
		return new vscode.DebugAdapterServer(port);
	}

	dispose(): void {
		this.terminal?.dispose();
		this.terminal = undefined;
	}
}


function waitForPort(port: number, timeoutMs: number): Promise<void> {
	return new Promise((resolve, reject) => {
		const deadline = Date.now() + timeoutMs;

		function tryConnect() {
			const socket = net.createConnection(port, "127.0.0.1");
			socket.on("connect", () => {
				socket.destroy();
				resolve();
			});
			socket.on("error", () => {
				if (deadline <= Date.now()) {
					reject(new Error(`DA did not listen on port ${port} within ${timeoutMs}ms`));
				} else {
					setTimeout(tryConnect, 100);
				}
			});
		}
		tryConnect();
	});
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