import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { RIDS, parseTarget, releaseDir } from "../src/binaryLayout.js";

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

const args = process.argv.slice(2);
const publishOnly = args.includes("--publish-only");
const positional = args.filter((a) => a !== "--publish-only");
const target = parseTarget(positional[0] ?? "");
if (positional.length !== 1 || target === undefined) {
	console.error(
		`Usage: bun scripts/vsix.ts [--publish-only] <${Object.keys(RIDS).join("|")}>`,
	);
	process.exit(1);
}
const rid = RIDS[target];

// 以前のビルドの残骸を同梱しない
rmSync(join(root, releaseDir(rid)), { recursive: true, force: true });

const published = run("dotnet", [
	"publish",
	"../../src/PlusPim/PlusPim.csproj",
	"-c",
	"Release",
	"-r",
	rid,
	"--self-contained",
	"-o",
	`./${releaseDir(rid)}`,
	"-p:DebugType=none",
	"-p:DebugSymbols=false",
]);

if (published && !publishOnly) {
	// vsceの否定パターンは他の除外より優先されるので，対象ディレクトリはpublish前に空にしてある
	const ignore =
		readFileSync(join(root, ".vscodeignore"), "utf8").trimEnd() +
		`\n!${releaseDir(rid)}/**\n`;

	const tempDir = mkdtempSync(join(tmpdir(), "pluspim-vsix-"));
	try {
		const ignoreFile = join(tempDir, ".vscodeignore");
		writeFileSync(ignoreFile, ignore);
		console.log(`[vsix] packaging ${target} (included: ${releaseDir(rid)})`);

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
