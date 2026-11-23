import { describe, expect, test } from "bun:test";
import {
	parseLaunchConfiguration,
	pluspimArgs,
	terminalName,
} from "../src/launchConfig.js";

describe("parseLaunchConfiguration", () => {
	test("文字列の program を1要素の配列にし，既定値を補う", () => {
		expect(parseLaunchConfiguration({ program: "a.asm" })).toEqual({
			programs: ["a.asm"],
			args: [],
			forceTerminalUtf8: true,
		});
	});

	test("配列の program と args をそのまま受け取る", () => {
		expect(
			parseLaunchConfiguration({
				program: ["a.asm", "b.asm"],
				args: ["--verbose"],
				forceTerminalUtf8: false,
			}),
		).toEqual({
			programs: ["a.asm", "b.asm"],
			args: ["--verbose"],
			forceTerminalUtf8: false,
		});
	});

	test.each([
		[{}, "program"],
		[{ program: [] }, "program"],
		[{ program: [{}] }, "program"],
		[{ program: "a.asm", args: [1] }, "args"],
		[{ program: "a.asm", args: "--verbose" }, "args"],
		[{ program: "a.asm", forceTerminalUtf8: "yes" }, "forceTerminalUtf8"],
	])("不正な構成 %o は %s を名指しして失敗する", (config, key) => {
		expect(() => parseLaunchConfiguration(config)).toThrow(`"${key}"`);
	});
});

describe("pluspimArgs", () => {
	const base = {
		programs: ["dir/a.asm"],
		args: ["--verbose"],
		forceTerminalUtf8: true,
	};

	test("接続先，UTF-8 の依頼，追加の引数の順に並べる", () => {
		expect(pluspimArgs(base, "127.0.0.1:1234")).toEqual([
			"debug",
			"connect",
			"127.0.0.1:1234",
			"--force-terminal-utf8",
			"--verbose",
		]);
	});

	test("UTF-8 の依頼を重複させない", () => {
		const config = { ...base, args: ["--force-terminal-utf8"] };
		expect(
			pluspimArgs(config, "x").filter((a) => a === "--force-terminal-utf8"),
		).toHaveLength(1);
	});

	test("forceTerminalUtf8 が false なら依頼しない", () => {
		expect(
			pluspimArgs({ ...base, forceTerminalUtf8: false }, "x"),
		).not.toContain("--force-terminal-utf8");
	});
});

test("terminalName はプログラムのファイル名を並べる", () => {
	expect(
		terminalName({
			programs: ["dir/a.asm", "b.s"],
			args: [],
			forceTerminalUtf8: true,
		}),
	).toBe("Debug: a.asm, b.s");
});
