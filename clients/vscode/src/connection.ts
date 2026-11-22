// PlusPim を起動し，PlusPimからのDAP経由をconnectモードで接続を受け入れる
// vscodeに依存しないものとする

import { once } from "node:events";
import * as net from "node:net";
import {
	type PlusPimLaunchConfig,
	pluspimArgs,
	terminalName,
} from "./launchConfig.js";

/** PlusPimを起動し，PlusPimが終了したら解決するPromiseを返す型 */
export type Spawn = (
	binPath: string,
	args: readonly string[],
	name: string,
) => Promise<void>;

export interface EstablishOptions {
	readonly binPath: string;
	readonly config: PlusPimLaunchConfig;
	readonly spawn: Spawn;
	/** 中止すると接続を待たずに失敗する．起動済みのPlusPimはそのまま残る */
	readonly signal: AbortSignal;
	readonly timeoutMs: number;
}

/**
 * ループバックの空きポートで待ち受け，そのポートを渡してPlusPimを起動し，最初に来た接続を返す．
 * 成功しても失敗しても待ち受けは閉じる．
 * @throws Error 中止されたとき，PlusPimが時間内に接続しないとき，接続前にPlusPimが終了したとき
 */
export async function establish(o: EstablishOptions): Promise<net.Socket> {
	const timeout = AbortSignal.timeout(o.timeoutMs);
	const signal = AbortSignal.any([o.signal, timeout]);
	const server = net.createServer();
	try {
		server.listen({ host: "127.0.0.1", port: 0, signal });
		await once(server, "listening", { signal });
		const address = server.address();
		if (address === null || typeof address === "string") {
			throw new Error("Failed to get the port to listen on");
		}

		const name = terminalName(o.config);
		const exited = o.spawn(
			o.binPath,
			pluspimArgs(o.config, `127.0.0.1:${address.port}`),
			name,
		);
		try {
			const args: unknown[] = await Promise.race([
				once(server, "connection", { signal }),
				exited.then(() => {
					throw new Error("PlusPim exited before connecting.");
				}),
			]);
			const socket = args[0];
			if (!(socket instanceof net.Socket)) {
				throw new TypeError("Unexpected connection event");
			}
			// 受け取るまでの間にerrorが起きても拡張が落ちないようにする
			socket.on("error", () => {});
			return socket;
		} catch (e) {
			if (timeout.aborted) {
				throw new Error(
					`PlusPim did not connect within ${o.timeoutMs}ms. See the terminal "${name}" for details.`,
					{ cause: e },
				);
			}
			throw e;
		}
	} finally {
		server.close();
	}
}
