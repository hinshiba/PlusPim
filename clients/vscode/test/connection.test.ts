import { describe, expect, test } from "bun:test";
import * as net from "node:net";
import { type Spawn, establish } from "../src/connection.js";

const config = { programs: ["a.asm"], args: [], forceTerminalUtf8: false };

/** 起動されたら引数の接続先へ接続する PlusPim の代わり．終了はしない */
const connecting: Spawn = (_binPath, args) => {
	const [host, port] = (args[2] ?? "").split(":");
	net.connect(Number(port), host).on("error", () => {});
	return new Promise(() => {});
};

const silent: Spawn = () => new Promise(() => {});

function options(spawn: Spawn, signal = new AbortController().signal) {
	return { binPath: "pluspim", config, spawn, signal, timeoutMs: 200 };
}

describe("establish", () => {
	test("PlusPim からの最初の接続を返し，引数に接続先を渡す", async () => {
		let received: readonly string[] = [];
		const socket = await establish(
			options((binPath, args, name) => {
				received = args;
				expect([binPath, name]).toEqual(["pluspim", "Debug: a.asm"]);
				return connecting(binPath, args, name);
			}),
		);
		expect(received.slice(0, 2)).toEqual(["debug", "connect"]);
		expect(received[2]).toMatch(/^127\.0\.0\.1:\d+$/u);
		socket.destroy();
	});

	test("時間内に接続しなければ失敗する", async () => {
		await expect(establish(options(silent))).rejects.toThrow(
			"did not connect within 200ms",
		);
	});

	test("接続前に PlusPim が終了したら失敗する", async () => {
		await expect(establish(options(() => Promise.resolve()))).rejects.toThrow(
			"exited before connecting",
		);
	});

	test("中止されたら接続を待たずに失敗する", async () => {
		const abort = new AbortController();
		const pending = establish(
			options(() => {
				abort.abort();
				return new Promise(() => {});
			}, abort.signal),
		);
		await expect(pending).rejects.toThrow();
		await expect(pending).rejects.not.toThrow("did not connect");
	});
});
