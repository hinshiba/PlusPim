// PlusPim との TCP 接続を VS Code の DebugAdapter として見せる
import type * as net from "node:net";
import * as vscode from "vscode";
import {
	DapFramingError,
	DapMessageDecoder,
	encodeDapMessage,
} from "./dapFraming.js";

/** このアダプタが中身を見る DAP メッセージの最小形 */
interface DapMessage {
	seq?: number;
	type?: string;
	command?: string;
	event?: string;
}

/**
 * 接続済みのソケットを介して DAP を中継する．
 * 接続が予期せず切れたときは，VS Code がセッションを終えられるように terminated を自前で送る
 */
export class SocketDebugAdapter implements vscode.DebugAdapter {
	private readonly emitter =
		new vscode.EventEmitter<vscode.DebugProtocolMessage>();
	private readonly decoder: DapMessageDecoder;
	/** disconnect の応答か terminated を VS Code に渡したら true．以後の切断は想定内 */
	private ended = false;
	private closed = false;
	/** 自前で送るメッセージの seq */
	private nextSeq = 1;

	readonly onDidSendMessage: vscode.Event<vscode.DebugProtocolMessage> =
		this.emitter.event;

	constructor(
		private readonly socket: net.Socket,
		private readonly onDispose: () => void,
	) {
		this.decoder = new DapMessageDecoder((message) => this.forward(message));
		socket.on("data", (chunk: Buffer) => {
			try {
				this.decoder.push(chunk);
			} catch (e) {
				if (!(e instanceof DapFramingError)) {
					throw e;
				}
				this.socket.destroy();
				this.onConnectionLost(`invalid DAP message: ${e.message}`);
			}
		});
		socket.on("error", (e) => this.onConnectionLost(e.message));
		socket.on("end", () => this.onConnectionLost("connection ended"));
		socket.on("close", () => this.onConnectionLost("connection closed"));
		// 接続してから受け取るまでの間に切れていた場合は，上のイベントを取りこぼしている
		if (socket.destroyed) {
			setImmediate(() => this.onConnectionLost("connection closed"));
		}
	}

	handleMessage(message: vscode.DebugProtocolMessage): void {
		const request = message as DapMessage;
		if (this.closed) {
			this.answerAfterClose(request);
			return;
		}
		this.socket.write(encodeDapMessage(message));
	}

	dispose(): void {
		this.closed = true;
		this.socket.removeAllListeners();
		// 後から来る error で例外にならないようにする
		this.socket.on("error", () => {});
		this.socket.destroy();
		this.emitter.dispose();
		this.onDispose();
	}

	private forward(message: unknown): void {
		const m = message as DapMessage;
		if (
			(m.type === "response" && m.command === "disconnect") ||
			(m.type === "event" && m.event === "terminated")
		) {
			this.ended = true;
		}
		this.emitter.fire(message as vscode.DebugProtocolMessage);
	}

	private onConnectionLost(reason: string): void {
		if (this.closed) {
			return;
		}
		this.closed = true;
		if (this.ended) {
			return;
		}
		this.ended = true;
		this.sendSynthetic({
			type: "event",
			event: "output",
			body: {
				category: "stderr",
				output: `PlusPim connection closed unexpectedly (${reason})\n`,
			},
		});
		this.sendSynthetic({ type: "event", event: "terminated", body: {} });
	}

	/** 切断後に来た要求へ応える．disconnect は成功にして，セッションを終えられるようにする */
	private answerAfterClose(request: DapMessage): void {
		if (request.type !== "request") {
			return;
		}
		const isDisconnect = request.command === "disconnect";
		this.sendSynthetic({
			type: "response",
			request_seq: request.seq,
			command: request.command,
			success: isDisconnect,
			...(isDisconnect ? {} : { message: "PlusPim connection is closed" }),
		});
	}

	private sendSynthetic(message: Record<string, unknown>): void {
		this.emitter.fire({
			seq: this.nextSeq++,
			...message,
		} as vscode.DebugProtocolMessage);
	}
}
