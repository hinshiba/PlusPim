// PlusPim の標準入出力を表示し，デバッグセッションの終了後も残る端末
import * as vscode from "vscode";
import { LineDiscipline, toTerminalNewlines } from "./lineDiscipline";

export interface DebuggeeIo {
	writeStdin(text: string): void;
	endStdin(): void;
	kill(): void;
}

type State = "running" | "exited" | "closed";

export class DebuggeeTerminal implements vscode.Pseudoterminal {
	private readonly writeEmitter = new vscode.EventEmitter<string>();
	private readonly closeEmitter = new vscode.EventEmitter<number | void>();
	readonly onDidWrite = this.writeEmitter.event;
	readonly onDidClose = this.closeEmitter.event;

	// open() より前に発生したイベントは VS Code に捨てられるため，それまで溜めておく
	private opened = false;
	private backlog: string[] = [];
	private state: State = "running";
	private io: DebuggeeIo | undefined;
	private readonly line = new LineDiscipline({
		echo: t => this.emit(t),
		submit: l => this.io?.writeStdin(l),
		eof: () => this.io?.endStdin(),
		interrupt: () => this.io?.kill(),
	});

	/** 端末が閉じられたとき (ユーザが閉じた，終了後にキーを押した，dispose した) に発生する */
	readonly onUserClosed = new vscode.EventEmitter<void>();

	/** 起動した PlusPim とつなぐ */
	attach(io: DebuggeeIo): void { this.io = io; }

	/** PlusPim が終了した，または端末が閉じられた */
	get hasExited(): boolean { return this.state !== "running"; }

	/** 端末が閉じられた */
	get isClosed(): boolean { return this.state === "closed"; }

	open(): void {
		this.opened = true;
		for (const t of this.backlog) { this.writeEmitter.fire(t); }
		this.backlog = [];
	}

	/** VS Code が端末を閉じたときに呼ばれる */
	close(): void {
		if (this.state === "closed") { return; }
		// 実行中に閉じられたら，PlusPim を終了してセッションも終える
		if (this.state === "running") { this.io?.kill(); }
		this.state = "closed";
		this.onUserClosed.fire();
	}

	handleInput(data: string): void {
		if (this.state !== "running") {
			this.dispose();
			return;
		}
		this.line.input(data);
	}

	/** PlusPim の出力を書く．LF は CRLF にする */
	write(text: string): void { this.emit(toTerminalNewlines(text)); }

	/** 出力を残したまま，ユーザがキーを押して閉じるのを待つ */
	markExited(code: number | null): void {
		if (this.state !== "running") { return; }
		this.state = "exited";
		const status = code === null ? "was terminated" : `exited with code ${code}`;
		this.emit(`\r\n\x1b[2m[PlusPim ${status}. Press any key to close this terminal.]\x1b[0m\r\n`);
	}

	/** 端末を閉じる．実行中なら PlusPim も終了する */
	dispose(): void {
		this.close();
		this.closeEmitter.fire();
	}

	private emit(text: string): void {
		if (this.opened) { this.writeEmitter.fire(text); } else { this.backlog.push(text); }
	}
}
