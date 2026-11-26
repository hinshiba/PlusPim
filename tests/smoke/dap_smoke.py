# /// script
# requires-python = ">=3.10"
# ///
"""publishしたpluspimをDAPクライアントとして起動から終了まで駆動する．

Usage:
    uv run tests/smoke/dap_smoke.py <pluspim>
"""

import json
import socket
import subprocess
import sys
from pathlib import Path


def main(exe: str) -> None:
    """``exe`` に ``exit.asm`` を読ませて最後まで実行させる．

    Args:
        exe: publish した pluspim の実行ファイル．

    Raises:
        AssertionError: 応答が想定と異なるとき．
    """
    srv = socket.create_server(("127.0.0.1", 0))
    srv.settimeout(30)
    port = srv.getsockname()[1]
    proc = subprocess.Popen([Path(exe).resolve(), "debug", "connect", f"127.0.0.1:{port}"])
    conn, _ = srv.accept()
    conn.settimeout(30)
    f = conn.makefile("rwb")
    seq = 0

    def send(command: str, arguments: dict | None = None) -> None:
        nonlocal seq
        seq += 1
        body = json.dumps({"seq": seq, "type": "request", "command": command, "arguments": arguments or {}}).encode()
        f.write(b"Content-Length: %d\r\n\r\n%s" % (len(body), body))
        f.flush()

    def until(pred) -> dict:
        while True:
            length = 0
            while line := f.readline().strip():
                name, _, value = line.partition(b":")
                if name.lower() == b"content-length":
                    length = int(value)
            assert length, "connection closed"
            message = json.loads(f.read(length))
            if pred(message):
                return message

    def request(command: str, arguments: dict | None = None) -> dict:
        send(command, arguments)
        response = until(lambda m: m.get("type") == "response" and m.get("request_seq") == seq)
        assert response["success"], response
        return response

    request("initialize", {"adapterID": "pluspim"})
    asm = Path(__file__).with_name("exit.asm").resolve()
    request("launch", {"program": [str(asm)], "stopOnEntry": True})
    request("configurationDone")
    until(lambda m: m.get("event") == "stopped")
    frames = request("stackTrace", {"threadId": 1})["body"]["stackFrames"]
    assert frames, "no stack frames on entry"
    request("continue", {"threadId": 1})
    end = until(lambda m: m.get("event") in ("stopped", "terminated"))
    assert end["event"] == "terminated", end
    request("disconnect")
    assert proc.wait(30) == 0, f"exit code {proc.returncode}"
    print(f"ok: {exe}")


if __name__ == "__main__":
    main(sys.argv[1])
