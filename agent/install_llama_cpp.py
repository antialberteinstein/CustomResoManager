import subprocess
import sys


def main() -> int:
    cmd = [
        sys.executable,
        "-m",
        "pip",
        "install",
        "llama-cpp-python",
        "--extra-index-url",
        "https://abetlen.github.io/llama-cpp-python/whl/cpu",
    ]
    result = subprocess.run(cmd, check=False)
    return result.returncode


if __name__ == "__main__":
    raise SystemExit(main())
