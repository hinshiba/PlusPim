import { spawnSync } from "node:child_process";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");

const ridMap: Record<string, string> = {
	"win32-x64": "win-x64",
	"linux-x64": "linux-x64",
};

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
const positional = args.filter(a => a !== "--publish-only");
const target = positional[0];
const rid = target === undefined ? undefined : ridMap[target];
if (positional.length !== 1 || target === undefined || rid === undefined) {
	console.error(`Usage: bun scripts/vsix.ts [--publish-only] <${Object.keys(ridMap).join("|")}>`);
	process.exit(1);
}

const published = run("dotnet", [
	"publish", "../../PlusPim/PlusPim.csproj",
	"-c", "Release",
	"-r", rid,
	"--self-contained",
	"-o", `./bin/${rid}`,
	"-p:DebugType=none",
	"-p:DebugSymbols=false",
]);

if (published && !publishOnly) {
	const otherRids = Object.values(ridMap).filter(r => r !== rid);
	const ignore =
		readFileSync(join(root, ".vscodeignore"), "utf8").trimEnd() + "\n" +
		"# Other platforms' binaries\n" +
		otherRids.map(r => `bin/${r}/**\n`).join("");

	const tempDir = mkdtempSync(join(tmpdir(), "pluspim-vsix-"));
	try {
		const ignoreFile = join(tempDir, ".vscodeignore");
		writeFileSync(ignoreFile, ignore);
		console.log(`[vsix] packaging ${target} (excluded: ${otherRids.map(r => `bin/${r}`).join(", ")})`);

		run(process.execPath, ["run", "compile"]) &&
			run(process.execPath, [
				"x", "--no-install", "-p", "@vscode/vsce", "vsce", "package",
				"--target", target,
				"--no-dependencies",
				"--ignoreFile", ignoreFile,
			]);
	} finally {
		rmSync(tempDir, { recursive: true, force: true });
	}
}
