// launch.json の起動構成の解析と，PlusPim に渡す引数の組み立て．vscode には依存しない
import * as path from "node:path";

/** 解析済みの起動構成．trace と stopOnEntry はトラッカーと PlusPim が読むのでここには含めない */
export interface PlusPimLaunchConfig {
	/** 1つ以上 */
	readonly programs: readonly string[];
	readonly args: readonly string[];
	readonly forceTerminalUtf8: boolean;
}

/** @throws Error 型が package.json の configurationAttributes に従っていないとき */
export function parseLaunchConfiguration(config: {
	readonly [key: string]: unknown;
}): PlusPimLaunchConfig {
	const programs =
		typeof config.program === "string"
			? [config.program]
			: parseStrings(config.program, "program");
	if (programs.length === 0) {
		throw new Error('"program" must not be empty');
	}
	const args =
		config.args === undefined ? [] : parseStrings(config.args, "args");
	const force = config.forceTerminalUtf8;
	if (force !== undefined && typeof force !== "boolean") {
		throw new Error('"forceTerminalUtf8" must be a boolean');
	}
	return { programs, args, forceTerminalUtf8: force ?? true };
}

function parseStrings(value: unknown, key: string): readonly string[] {
	if (!Array.isArray(value) || !value.every((v) => typeof v === "string")) {
		throw new Error(`"${key}" must be a string or an array of strings`);
	}
	return value;
}

/** `debug connect` で address へ接続させる PlusPim の引数 */
export function pluspimArgs(
	config: PlusPimLaunchConfig,
	address: string,
): string[] {
	// 端末の符号化の切り替えは PlusPim が判断する．拡張は全 OS で既定で依頼する
	const utf8 =
		config.forceTerminalUtf8 && !config.args.includes("--force-terminal-utf8")
			? ["--force-terminal-utf8"]
			: [];
	return ["debug", "connect", address, ...utf8, ...config.args];
}

export function terminalName(config: PlusPimLaunchConfig): string {
	return `Debug: ${config.programs.map((p) => path.basename(p)).join(", ")}`;
}
