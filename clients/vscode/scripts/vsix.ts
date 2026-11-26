import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { RELEASE_DIR, RIDS, parseTarget } from "../src/binaryLayout.js";

const root = join(import.meta.dirname, "..");

function run(command: string, args: string[]): boolean {
	console.log(`[vsix] ${command} ${args.join(" ")}`);
	const result = spawnSync(command, args, { stdio: "inherit", cwd: root });
	if (result.error) {
		console.error(result.error);
		process.exitCode = 1;
		return false;
	}
	if (result.status !== 0) {
		process.exitCode = result.status ?? 1;
		return false;
	}
	return true;
}

// --publish-only: バイナリのpublishだけ
// --package-only: RELEASE_DIRに置いたバイナリでVSIXを作るだけ (CIでは別ジョブがビルドする)
const MODES = ["--publish-only", "--package-only"];
const args = process.argv.slice(2);
const mode = args.find((a) => MODES.includes(a));
const positional = args.filter((a) => !MODES.includes(a));
const target = parseTarget(positional[0] ?? "");
if (
	positional.length !== 1 ||
	target === undefined ||
	positional.length + 1 < args.length
) {
	console.error(
		`Usage: bun scripts/vsix.ts [${MODES.join("|")}] <${Object.keys(RIDS).join("|")}>`,
	);
	process.exit(1);
}
const rid = RIDS[target];

function publish(): boolean {
	// 以前のビルドの残骸を同梱しない
	rmSync(join(root, RELEASE_DIR), { recursive: true, force: true });
	// linux-x64はリリースと同じglibcの下限にするため, コンテナでビルドするスクリプトに任せる
	return rid === "linux-x64"
		? run("sh", [
				"../../scripts/build/publish.sh",
				rid,
				`clients/vscode/${RELEASE_DIR}`,
			])
		: run("dotnet", [
				"publish",
				"../../src/PlusPim/PlusPim.csproj",
				"-c",
				"Release",
				"-r",
				rid,
				"-o",
				`./${RELEASE_DIR}`,
			]);
}

if ((mode === "--package-only" || publish()) && mode !== "--publish-only") {
	// vsceの否定パターンは他の除外より優先されるので，対象ディレクトリはpublish前に空にしてある
	const ignore =
		readFileSync(join(root, ".vscodeignore"), "utf8").trimEnd() +
		`\n!${RELEASE_DIR}/**\n`;

	const tempDir = mkdtempSync(join(tmpdir(), "pluspim-vsix-"));
	try {
		const ignoreFile = join(tempDir, ".vscodeignore");
		writeFileSync(ignoreFile, ignore);
		console.log(`[vsix] packaging ${target} (${rid})`);

		if (run(process.execPath, ["run", "compile"])) {
			run(process.execPath, [
				"x",
				"--no-install",
				"-p",
				"@vscode/vsce",
				"vsce",
				"package",
				"--target",
				target,
				"--no-dependencies",
				"--ignoreFile",
				ignoreFile,
			]);
		}
	} finally {
		rmSync(tempDir, { recursive: true, force: true });
	}
}
