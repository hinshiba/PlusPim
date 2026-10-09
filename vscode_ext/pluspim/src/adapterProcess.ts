// PlusPim を起動し，待ち受けているポートを標準出力の1行から受け取る．vscode に依存しない
import { ChildProcessWithoutNullStreams, spawn } from "child_process";

/** C# 側の Program.ListeningMarker と一致させること */
export const LISTENING_MARKER = "PLUSPIM_DAP_LISTENING 127.0.0.1:";

/**
 * 標準出力から待ち受けの行を探す．その行以外はすべてそのまま返す (デバッギの出力を捨てないため)
 */
export class HandshakeScanner {
	private pending = "";
	private _port: number | undefined;

	/** 待ち受けの行で通知されたポート．まだなら undefined */
	get port(): number | undefined { return this._port; }

	/** 受け取った文字列を渡し，端末に流す文字列を返す */
	feed(text: string): string {
		if (this._port !== undefined) { return text; }
		this.pending += text;
		let out = "";
		for (; ;) {
			const nl = this.pending.indexOf("\n");
			if (nl < 0) {
				// 待ち受けの行の先頭と一致しなければ，もう待ち受けの行にはならない
				if (!LISTENING_MARKER.startsWith(this.pending.slice(0, LISTENING_MARKER.length))) {
					out += this.pending;
					this.pending = "";
				}
				return out;
			}
			const line = this.pending.slice(0, nl).replace(/\r$/, "");
			this.pending = this.pending.slice(nl + 1);
			const m = line.startsWith(LISTENING_MARKER) ? /^(\d{1,5})$/.exec(line.slice(LISTENING_MARKER.length)) : null;
			if (m) {
				this._port = Number(m[1]);
				out += this.pending;
				this.pending = "";
				return out;
			}
			out += line + "\n";
		}
	}

	/** 改行で終わらずに残っている文字列を返す (プロセスの終了時に使う) */
	flush(): string {
		const rest = this.pending;
		this.pending = "";
		return rest;
	}
}

export interface AdapterProcess {
	readonly port: number;
	readonly child: ChildProcessWithoutNullStreams;
	/** 標準入出力が閉じて終了したら終了コードで解決する (シグナルで終了したときは null) */
	readonly exited: Promise<number | null>;
}

export interface LaunchOptions {
	binPath: string;
	args: string[];
	cwd?: string;
	timeoutMs: number;
	/** 待ち受けの行を除いた標準出力 (UTF-8 で復号済み) */
	onStdout(text: string): void;
	/** 標準エラー出力 (UTF-8 で復号済み) */
	onStderr(text: string): void;
}

export class AdapterLaunchError extends Error { }

/** PlusPim を起動し，待ち受けているポートが通知されたら解決する */
export function launchAdapter(opts: LaunchOptions): Promise<AdapterProcess> {
	const child = spawn(opts.binPath, opts.args, { cwd: opts.cwd, stdio: "pipe", windowsHide: true });
	// 終了後の書き込み (EPIPE など) で拡張機能ホストが落ちないようにする
	child.stdin.on("error", () => { });
	// close は標準出力を読み終えてから発生するため，出力の後に終了を扱える
	const exited = new Promise<number | null>(resolve => child.once("close", code => resolve(code)));
	const scanner = new HandshakeScanner();
	const outDecoder = new TextDecoder("utf-8");
	const errDecoder = new TextDecoder("utf-8");
	let stderrTail = "";

	return new Promise<AdapterProcess>((resolve, reject) => {
		const timer = setTimeout(() => {
			child.kill();
			reject(new AdapterLaunchError(`PlusPim did not report a DAP port within ${opts.timeoutMs}ms.`));
		}, opts.timeoutMs);

		child.stdout.on("data", (chunk: Buffer) => {
			const text = scanner.feed(outDecoder.decode(chunk, { stream: true }));
			if (text) { opts.onStdout(text); }
			if (scanner.port !== undefined) {
				clearTimeout(timer);
				resolve({ port: scanner.port, child, exited });
			}
		});
		child.stderr.on("data", (chunk: Buffer) => {
			const text = errDecoder.decode(chunk, { stream: true });
			stderrTail = (stderrTail + text).slice(-2000);
			opts.onStderr(text);
		});
		child.once("error", err => {
			clearTimeout(timer);
			reject(new AdapterLaunchError(`Failed to start PlusPim (${opts.binPath}): ${err.message}`));
		});
		child.once("close", code => {
			// 解決済みなら reject は無視される
			clearTimeout(timer);
			const rest = scanner.port === undefined ? scanner.flush() + outDecoder.decode() : "";
			if (rest) { opts.onStdout(rest); }
			reject(new AdapterLaunchError(`PlusPim exited with code ${code} before listening.${stderrTail ? `\n${stderrTail.trimEnd()}` : ""}`));
		});
	});
}
