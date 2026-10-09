// 疑似命令 (la，li など) の展開先の機械命令をインレイヒントで表示する
import * as vscode from "vscode";

/** DAP のカスタム要求の名前 (doc/debugger.md「疑似命令の展開先」) */
export const EXPANSIONS_REQUEST = "pluspimPseudoExpansions";

export interface PseudoExpansionsArguments {
	source: { path: string };
}

export interface ExpandedInstruction {
	/** "0x%08X" の形式のアドレス */
	address: string;
	/** 即値を解決した命令の表記．表記に未対応の命令は "?" */
	text: string;
}

export interface PseudoExpansion {
	/** 疑似命令のソースの行 (1始まり) */
	line: number;
	/** 疑似命令のニーモニック (小文字) */
	mnemonic: string;
	/** 展開先の機械命令 (実行順) */
	instructions: ExpandedInstruction[];
}

export interface PseudoExpansionsResponseBody {
	lines: PseudoExpansion[];
}

/** 展開先の取得元．今は DAP のみ．CLI や LSP で実装することもできる */
export interface ExpansionSource {
	/** このファイルのデータがなければ (セッションがない，未対応，プログラムに含まれない) undefined */
	fetch(fsPath: string): Promise<PseudoExpansion[] | undefined>;
}

/** 実行中の pluspim セッションにカスタム要求を送る */
export class DapExpansionSource implements ExpansionSource {
	// launch に成功して要求に答えられるセッション
	private readonly ready = new Set<string>();
	// 要求に失敗したセッション (古いアダプタなど)．以後は問い合わせない
	private readonly unsupported = new Set<string>();

	/** launch の応答が成功したら呼ぶ．それより前の要求は失敗するため送らない */
	markReady(sessionId: string): void { this.ready.add(sessionId); }

	/** セッションが終わったら呼ぶ */
	forget(sessionId: string): void {
		this.ready.delete(sessionId);
		this.unsupported.delete(sessionId);
	}

	async fetch(fsPath: string): Promise<PseudoExpansion[] | undefined> {
		const session = vscode.debug.activeDebugSession;
		if (session?.type !== "pluspim" || !this.ready.has(session.id) || this.unsupported.has(session.id)) { return undefined; }
		try {
			const args: PseudoExpansionsArguments = { source: { path: fsPath } };
			const body = await session.customRequest(EXPANSIONS_REQUEST, args) as PseudoExpansionsResponseBody | undefined;
			return body?.lines;
		} catch {
			this.unsupported.add(session.id);
			return undefined;
		}
	}
}

interface CacheEntry {
	version: number;
	expansions: Promise<PseudoExpansion[] | undefined>;
}

export class PseudoExpansionHintsProvider implements vscode.InlayHintsProvider {
	private readonly changeEmitter = new vscode.EventEmitter<void>();
	readonly onDidChangeInlayHints = this.changeEmitter.event;
	// キーは文書の URI．行番号はアセンブルした版の文書でだけ正しい
	private readonly cache = new Map<string, CacheEntry>();

	constructor(private readonly source: ExpansionSource) { }

	/** pluspim セッションの launch の応答の後と，セッションの終了時に呼ぶ */
	invalidate(): void {
		this.cache.clear();
		this.changeEmitter.fire();
	}

	async provideInlayHints(document: vscode.TextDocument, range: vscode.Range): Promise<vscode.InlayHint[]> {
		if (!vscode.workspace.getConfiguration("pluspim").get<boolean>("inlayHints.pseudoInstructions", true)) {
			return [];
		}
		if (document.isDirty) {
			// アダプタがアセンブルしたのは保存されたファイルで，この編集中の内容ではない
			return [];
		}
		const key = document.uri.toString();
		let entry = this.cache.get(key);
		if (!entry) {
			// セッションで最初の要求のときの版を，アセンブルした版とみなす
			entry = { version: document.version, expansions: this.source.fetch(document.uri.fsPath) };
			this.cache.set(key, entry);
		}
		if (entry.version !== document.version) {
			// アセンブルした後に編集されたため，行番号がずれている可能性がある
			return [];
		}
		const expansions = await entry.expansions ?? [];
		const hints: vscode.InlayHint[] = [];
		for (const e of expansions) {
			const line = e.line - 1;
			if (line < range.start.line || range.end.line < line || document.lineCount <= line || e.instructions.length === 0) { continue; }
			const texts = e.instructions.map(i => i.text);
			const hint = new vscode.InlayHint(document.lineAt(line).range.end, `=> ${texts.join("; ")}`);
			hint.paddingLeft = true;
			hint.tooltip = new vscode.MarkdownString().appendCodeblock(
				e.instructions.map(i => `${i.text}  # ${i.address}`).join("\n"), "mips");
			hints.push(hint);
		}
		return hints;
	}
}
