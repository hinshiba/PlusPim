import { afterEach, beforeEach, describe, expect, test } from "bun:test";
import * as fs from "node:fs";
import * as os from "node:os";
import * as path from "node:path";
import {
	DEBUG_DIR,
	parseTarget,
	releaseDir,
	resolveBinary,
} from "../src/binaryLayout.js";

test("parseTarget は対応表にないターゲットを拒む", () => {
	expect(parseTarget("darwin-arm64")).toBe("darwin-arm64");
	expect(parseTarget("linux-arm64")).toBeUndefined();
	expect(parseTarget("toString")).toBeUndefined();
});

describe("resolveBinary", () => {
	let root = "";
	beforeEach(() => {
		root = fs.mkdtempSync(path.join(os.tmpdir(), "pluspim-bin-"));
	});
	afterEach(() => {
		fs.rmSync(root, { recursive: true, force: true });
	});

	function place(dir: string, exe: string): string {
		const p = path.join(root, dir, exe);
		fs.mkdirSync(path.dirname(p), { recursive: true });
		fs.writeFileSync(p, "");
		return p;
	}

	test("RID のディレクトリからプラットフォームの実行ファイルを選ぶ", () => {
		const p = place(releaseDir("osx-arm64"), "pluspim");
		expect(resolveBinary(root, false, "darwin", "arm64")).toEqual({
			path: p,
			fellBack: false,
		});
	});

	test("開発ビルドがあれば優先する", () => {
		place(releaseDir("win-x64"), "pluspim.exe");
		const p = place(DEBUG_DIR, "pluspim.exe");
		expect(resolveBinary(root, true, "win32", "x64")).toEqual({
			path: p,
			fellBack: false,
		});
	});

	test("開発ビルドがなければリリースビルドにして，そのことを返す", () => {
		const p = place(releaseDir("win-x64"), "pluspim.exe");
		expect(resolveBinary(root, true, "win32", "x64")).toEqual({
			path: p,
			fellBack: true,
		});
	});

	test("実行ファイルがなければ失敗する", () => {
		expect(() => resolveBinary(root, false, "win32", "x64")).toThrow(
			"not found",
		);
	});

	test("対応していないプラットフォームでは失敗する", () => {
		expect(() => resolveBinary(root, false, "linux", "arm64")).toThrow(
			"linux-arm64",
		);
	});
});
