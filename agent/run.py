import subprocess
import sys

from config import CHAT_HOST, CHAT_PORT

def main() -> int:
    module = "main:app"
    host = CHAT_HOST
    port = CHAT_PORT

    cmd = [
        sys.executable,
        "-m",
        "uvicorn",
        module,
        "--host",
        host,
        "--port",
        str(port),
    ]

    return subprocess.call(cmd)


if __name__ == "__main__":
    raise SystemExit(main())
