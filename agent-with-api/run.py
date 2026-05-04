import subprocess
import sys

from config import CHAT_HOST, CHAT_PORT


def main() -> int:
    module = "main:app"
    cmd = [
        sys.executable,
        "-m",
        "uvicorn",
        module,
        "--host",
        CHAT_HOST,
        "--port",
        str(CHAT_PORT),
    ]
    return subprocess.call(cmd)


if __name__ == "__main__":
    raise SystemExit(main())
