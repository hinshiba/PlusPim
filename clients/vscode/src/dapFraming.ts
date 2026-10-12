// DAP の基本プロトコル (Content-Length 付きの JSON) の符号化と復号．vscode には依存しない

/** ヘッダ部の上限．これを超えて区切りが現れなければ壊れた入力とみなす */
const MAX_HEADER_BYTES = 1024;
/** 本文の上限 */
const MAX_BODY_BYTES = 64 * 1024 * 1024;

const HEADER_END = Buffer.from("\r\n\r\n", "ascii");

/** 入力が DAP の形式に従っていないときに投げる */
export class DapFramingError extends Error {}

/** メッセージを Content-Length 付きのバイト列にする．長さは UTF-8 のバイト数で数える */
export function encodeDapMessage(message: unknown): Buffer {
	const body = Buffer.from(JSON.stringify(message), "utf8");
	const header = Buffer.from(`Content-Length: ${body.length}\r\n\r\n`, "ascii");
	return Buffer.concat([header, body]);
}

/**
 * ストリームから来たバイト列を DAP のメッセージに切り出す．
 * チャンクの区切りはメッセージの区切りと一致しないので，足りない分は次の push まで貯める
 */
export class DapMessageDecoder {
	private buffer: Buffer = Buffer.alloc(0);

	constructor(private readonly onMessage: (message: unknown) => void) {}

	/** @throws DapFramingError 入力が壊れているとき．以後このデコーダは使えない */
	push(chunk: Buffer): void {
		this.buffer =
			this.buffer.length === 0 ? chunk : Buffer.concat([this.buffer, chunk]);

		for (;;) {
			const headerEnd = this.buffer.indexOf(HEADER_END);
			if (headerEnd < 0) {
				if (this.buffer.length > MAX_HEADER_BYTES) {
					throw new DapFramingError("DAP header is too long");
				}
				return;
			}
			if (headerEnd > MAX_HEADER_BYTES) {
				throw new DapFramingError("DAP header is too long");
			}

			const length = parseContentLength(
				this.buffer.subarray(0, headerEnd).toString("ascii"),
			);
			const bodyStart = headerEnd + HEADER_END.length;
			const bodyEnd = bodyStart + length;
			if (this.buffer.length < bodyEnd) {
				return;
			}

			// 文字数ではなくバイト数で切り出してから復号する
			const text = this.buffer.subarray(bodyStart, bodyEnd).toString("utf8");
			this.buffer = this.buffer.subarray(bodyEnd);

			let message: unknown;
			try {
				message = JSON.parse(text);
			} catch {
				throw new DapFramingError("DAP body is not valid JSON");
			}
			this.onMessage(message);
		}
	}
}

function parseContentLength(header: string): number {
	for (const line of header.split("\r\n")) {
		const match = /^content-length:\s*(.*?)\s*$/i.exec(line);
		if (match === null) {
			continue;
		}
		const value = match[1] ?? "";
		if (!/^\d+$/.test(value)) {
			throw new DapFramingError(`Invalid Content-Length: ${value}`);
		}
		const length = Number(value);
		if (length > MAX_BODY_BYTES) {
			throw new DapFramingError(`DAP body is too large: ${length} bytes`);
		}
		return length;
	}
	throw new DapFramingError("Content-Length header is missing");
}
