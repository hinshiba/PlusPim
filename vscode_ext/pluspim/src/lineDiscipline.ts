// Pseudoterminal 向けの最小限の行編集 (cooked mode)．vscode に依存しない

export interface LineDisciplineActions {
	/** 端末にエコーする文字列 */
	echo(text: string): void;
	/** 確定した1行 (末尾に "\n" を含む)．子プロセスの標準入力に送る */
	submit(line: string): void;
	/** 空行での Ctrl+D */
	eof(): void;
	/** Ctrl+C */
	interrupt(): void;
}

/** xterm.js 向けに LF を CRLF にする．既存の CRLF はそのまま */
export function toTerminalNewlines(text: string): string {
	return text.replace(/\r?\n/g, "\r\n");
}

/** Backspace で消す桁数のための，おおまかな East Asian Width */
function columns(ch: string): number {
	const cp = ch.codePointAt(0) ?? 0;
	return (cp >= 0x1100 && (cp <= 0x115f || (cp >= 0x2e80 && cp <= 0xa4cf) || (cp >= 0xac00 && cp <= 0xd7a3) ||
		(cp >= 0xf900 && cp <= 0xfaff) || (cp >= 0xfe30 && cp <= 0xfe4f) || (cp >= 0xff00 && cp <= 0xff60) ||
		(cp >= 0xffe0 && cp <= 0xffe6) || (cp >= 0x1f300 && cp <= 0x1faff) || (cp >= 0x20000 && cp <= 0x3fffd))) ? 2 : 1;
}

/**
 * 端末からの入力を行単位にまとめる．PlusPim は標準入力を行単位で読むため，これで十分である
 *
 * 矢印キーによる編集と履歴には対応しない
 */
export class LineDiscipline {
	private buffer: string[] = [];

	constructor(private readonly actions: LineDisciplineActions) { }

	/** Pseudoterminal.handleInput からの入力を渡す (貼り付けで複数文字のこともある) */
	input(data: string): void {
		// エスケープシーケンス (矢印キー，ファンクションキー) は捨てる
		data = data.replace(/\x1b\[[0-9;?]*[ -/]*[@-~]|\x1bO./g, "");
		for (const ch of data) {
			switch (ch) {
				case "\r":
				case "\n": {
					const line = this.buffer.join("");
					this.buffer = [];
					this.actions.echo("\r\n");
					this.actions.submit(line + "\n");
					break;
				}
				case "\x7f":
				case "\b": {
					const last = this.buffer.pop();
					if (last !== undefined) {
						const w = columns(last);
						this.actions.echo("\b".repeat(w) + " ".repeat(w) + "\b".repeat(w));
					}
					break;
				}
				case "\x03":
					this.buffer = [];
					this.actions.echo("^C\r\n");
					this.actions.interrupt();
					break;
				case "\x04":
					if (this.buffer.length === 0) { this.actions.eof(); }
					break;
				default:
					if (ch >= " ") {
						this.buffer.push(ch);
						this.actions.echo(ch);
					}
			}
		}
	}
}
