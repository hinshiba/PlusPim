#!/usr/bin/env node
/**
 * Platform-specific VSIX packaging script.
 * Usage: node scripts/vsix.mjs <win32-x64|linux-x64>
 *
 * Writes a target-specific .vscodeignore (excluding other platforms' binaries)
 * then runs dotnet publish and vsce package.
 */
import { execSync } from 'child_process';
import { writeFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const root = join(__dirname, '..');

const ridMap = {
    'win32-x64': 'win-x64',
    'linux-x64': 'linux-x64',
};

const target = process.argv[2];
if (!ridMap[target]) {
    console.error(`Usage: node scripts/vsix.mjs <${Object.keys(ridMap).join('|')}>`);
    process.exit(1);
}

const rid = ridMap[target];
const excludeRids = Object.values(ridMap).filter(r => r !== rid);

// Write a target-specific .vscodeignore
const ignoreLines = [
    '.vscode/**',
    '.vscode-test/**',
    'src/**',
    '.gitignore',
    '.yarnrc',
    'vsc-extension-quickstart.md',
    '**/tsconfig.json',
    '**/.eslintrc.json',
    '**/*.ts',
    'node_modules/**',
    'pnpm-lock.yaml',
    'testfolder/**',
    'scripts/**',
    'out/**',
    '!out/extension.js',
    // Exclude other platforms' binaries
    ...excludeRids.map(r => `bin/${r}/**`),
    // Exclude PDB files for the target platform
    `bin/${rid}/*.pdb`,
];
writeFileSync(join(root, '.vscodeignore'), ignoreLines.join('\n') + '\n');
console.log(`[vsix] .vscodeignore updated for ${target} (excluded: ${excludeRids.map(r => `bin/${r}`).join(', ')})`);

// Build the target binary
execSync(
    `dotnet publish ../../PlusPim/PlusPim.csproj -c Release -r ${rid} --self-contained` +
    ` -o ./bin/${rid} -p:DebugType=none -p:DebugSymbols=false`,
    { stdio: 'inherit', cwd: root }
);

// Package VSIX (vscode:prepublish compiles TypeScript)
execSync(`pnpm exec vsce package --target ${target}`, { stdio: 'inherit', cwd: root });
