// 拡張に同梱するPlusPimの実行ファイルの配置
// パッケージングと共有する
// vscodeに依存しないものとする

import * as fs from "node:fs";
import * as path from "node:path";

/** VS Codeのターゲットから.NETのRIDへの対応 */
export const RIDS = {
	"win32-x64": "win-x64",
	"win32-arm64": "win-arm64",
	"linux-x64": "linux-x64",
	"linux-arm64": "linux-arm64",
	"alpine-x64": "linux-musl-x64",
	"alpine-arm64": "linux-musl-arm64",
	"darwin-x64": "osx-x64",
	"darwin-arm64": "osx-arm64",
} as const;

export type Target = keyof typeof RIDS;

function isTarget(s: string): s is Target {
	return Object.hasOwn(RIDS, s);
}

/** 対応していないターゲットならundefined */
export function parseTarget(s: string): Target | undefined {
	return isTarget(s) ? s : undefined;
}

/**
 * リリースビルドを置くディレクトリ (拡張のルートからの相対パス)
 * VSIXはターゲットごとに分かれ，Marketplaceが利用者のプラットフォームに合うものを選ぶ．
 * 同梱するのは1つだけなので，RIDで分けない．
 * alpineとlinuxは`process.platform`で区別できないので，実行時にRIDを求めることもできない．
 */
export const RELEASE_DIR = "bin/release";

/** 開発ビルドが配置されるディレクトリ */
export const DEBUG_DIR = "bin/debug";

export interface ResolvedBinary {
	/** 実行権限のある実行ファイルの絶対パス */
	readonly path: string;
	/** 開発ビルドを望んだが見つからず，リリースビルドにしたか */
	readonly fellBack: boolean;
}

/**
 * 起動する実行ファイルを決め，LinuxとmacOSでは実行権限がなければ付与する．
 * @throws Error 実行ファイルがない，実行権限を付与できないとき
 */
export function resolveBinary(
	root: string,
	preferDebug: boolean,
	platform: NodeJS.Platform = process.platform,
): ResolvedBinary {
	const exe = platform === "win32" ? "pluspim.exe" : "pluspim";
	const debugPath = path.join(root, DEBUG_DIR, exe);
	if (preferDebug && fs.existsSync(debugPath)) {
		return { path: ensureExecutable(debugPath, platform), fellBack: false };
	}
	const releasePath = path.join(root, RELEASE_DIR, exe);
	return {
		path: ensureExecutable(releasePath, platform),
		fellBack: preferDebug,
	};
}

function ensureExecutable(binPath: string, platform: NodeJS.Platform): string {
	if (!fs.existsSync(binPath)) {
		throw new Error(`PlusPim binary not found: ${binPath}`);
	}
	if (platform === "win32") {
		return binPath;
	}
	try {
		fs.accessSync(binPath, fs.constants.X_OK);
		return binPath;
	} catch {
		// 実行権限がないだけなので付与を試みる
	}
	try {
		fs.chmodSync(binPath, 0o755);
		return binPath;
	} catch {
		throw new Error(
			`PlusPim binary is not executable and could not be fixed automatically. Run: chmod +x "${binPath}"`,
		);
	}
}
