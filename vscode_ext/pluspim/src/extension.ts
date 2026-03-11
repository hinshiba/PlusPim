import * as vscode from "vscode";
import * as net from "net";
import * as path from "path";

export function activate(context: vscode.ExtensionContext) {

	// console.log
	// console.error
	console.log("PlsuPim Extension was loaded.");
	// わかりやすいように追加
	vscode.window.showInformationMessage("Hello World from pluspim!");

	// The command has been defined in the package.json file
	// Now provide the implementation of the command with registerCommand
	// The commandId parameter must match the command field in package.json
	let disposable = vscode.commands.registerCommand("pluspim.helloWorld", () => {
		vscode.window.showInformationMessage("Hello World from pluspim!");
	});
	context.subscriptions.push(disposable);

	// 情報を設定
	context.subscriptions.push(
		vscode.debug.registerDebugAdapterDescriptorFactory(
			"pluspim",
			new PlusPimDescriptorFactory()
		));


	const output = vscode.window.createOutputChannel("PlusPim DAP Trace");

	context.subscriptions.push(
		vscode.debug.registerDebugAdapterTrackerFactory("PlusPim Trace", {
			createDebugAdapterTracker(session) {
				// trace有効時のみ
				if (session.configuration.trace) {
					output.show(true);
					return new PlusPimTracker(output);
				}
				return undefined; // トラッキングしない
			}
		}))
}

export function deactivate() { }


class PlusPimDescriptorFactory implements vscode.DebugAdapterDescriptorFactory {
	async createDebugAdapterDescriptor(
		session: vscode.DebugSession
	): Promise<vscode.DebugAdapterDescriptor> {
		const port = session.configuration.port ?? 4711;
		// vscode.DebugConfigurationの[key: string]: any
		const program = session.configuration.program;

		// ターミナルで呼んでもらう
		const terminal = vscode.window.createTerminal({
			name: `Debug: ${path.basename(program)}`,
			shellPath: "D:\\dev\\_univ\\g2t4\\OOP10\\PlusPim\\bin\\Debug\\net10.0\\PlusPim.exe",
			shellArgs: ["-v", "-d", "--port", String(port), program],
		})
		terminal.show()

		await waitForPort(port, 5000);

		// TCPであることも設定
		return new vscode.DebugAdapterServer(port);
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
				if (deadline < Date.now()) {
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