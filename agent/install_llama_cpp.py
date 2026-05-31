"""Bootstrap llama-cpp-python and its Windows runtime dependency.

Two failure modes this script handles:

1. Missing VC++ runtime. The prebuilt llama.dll links against the Microsoft Visual
   C++ runtime (vcruntime140.dll / msvcp140.dll). On a clean Windows box those are
   missing, so `from llama_cpp import Llama` fails with:
       Failed to load shared library '...\\llama_cpp\\lib\\llama.dll':
       Could not find module ... (or one of its dependencies).
   -> we install the VC++ Redistributable first.

2. CPU too old for the prebuilt wheel. abetlen's CPU wheel is compiled with AVX2/FMA.
   On a CPU without those, loading works but the first compute kernel crashes with:
       OSError: [WinError -1073741795] Windows Error 0xC000001D   (ILLEGAL_INSTRUCTION)
   -> when AVX2 is absent (or LLAMA_NO_AVX=1 is set) we rebuild llama-cpp-python from
   source with every AVX/FMA/F16C extension disabled, which runs on any x86-64 CPU.
   That source build needs a C++ toolchain (Visual Studio Build Tools, C++ workload).
"""
import os
import subprocess
import sys
import tempfile
import urllib.request


VC_REDIST_URL = "https://aka.ms/vs/17/release/vc_redist.x64.exe"
CPU_WHEEL_INDEX = "https://abetlen.github.io/llama-cpp-python/whl/cpu"

# Fully generic build: no -march=native and no AVX/AVX2/FMA/F16C, so the binary runs
# on any x86-64 CPU. Slower than the AVX2 wheel, but it won't throw ILLEGAL_INSTRUCTION.
NO_AVX_CMAKE_ARGS = (
    "-DGGML_NATIVE=OFF -DGGML_AVX=OFF -DGGML_AVX2=OFF -DGGML_FMA=OFF -DGGML_F16C=OFF"
)

# Installer exit codes that mean "we're fine to continue":
#   0    = installed successfully
#   1638 = a newer version is already installed
#   3010 = installed successfully, reboot required
_VC_REDIST_OK_CODES = {0, 1638, 3010}


def install_vc_redist() -> int:
    """Download and silently install the VC++ x64 Redistributable. Windows only.

    Returns 0 on success (or if it's already present / not on Windows), non-zero on
    a real failure.
    """
    if sys.platform != "win32":
        print("[install] Not on Windows, skipping VC++ Redistributable.")
        return 0

    dest = os.path.join(tempfile.gettempdir(), "vc_redist.x64.exe")
    print(f"[install] Downloading VC++ Redistributable from {VC_REDIST_URL}")
    try:
        urllib.request.urlretrieve(VC_REDIST_URL, dest)
    except Exception as ex:  # network / URL issues shouldn't be fatal to the whole script
        print(f"[install] WARNING: could not download VC++ Redistributable: {ex}")
        print("[install] Install it manually from https://aka.ms/vs/17/release/vc_redist.x64.exe")
        return 0

    print("[install] Installing VC++ Redistributable (a UAC prompt may appear)...")
    # /install /quiet /norestart -> silent, no reboot. The installer self-elevates via UAC.
    result = subprocess.run(
        [dest, "/install", "/quiet", "/norestart"],
        check=False,
    )

    if result.returncode in _VC_REDIST_OK_CODES:
        suffix = " (reboot recommended)" if result.returncode == 3010 else ""
        print(f"[install] VC++ Redistributable ready{suffix}.")
        return 0

    print(
        f"[install] WARNING: VC++ Redistributable installer exited with code "
        f"{result.returncode}. If llama_cpp still fails to load, install it "
        f"manually from {VC_REDIST_URL}"
    )
    return result.returncode


def cpu_has_avx2() -> bool:
    """Best-effort check for AVX2 support.

    On Windows we ask the OS via IsProcessorFeaturePresent(PF_AVX2_INSTRUCTIONS_AVAILABLE).
    Elsewhere (or if the call fails) we assume AVX2 is available and let the prebuilt
    wheel be used.
    """
    if sys.platform != "win32":
        return True
    try:
        import ctypes

        PF_AVX2_INSTRUCTIONS_AVAILABLE = 40
        return bool(
            ctypes.windll.kernel32.IsProcessorFeaturePresent(
                PF_AVX2_INSTRUCTIONS_AVAILABLE
            )
        )
    except Exception as ex:
        print(f"[install] WARNING: could not probe CPU features ({ex}); assuming AVX2.")
        return True


def install_prebuilt_llama() -> int:
    """Install the prebuilt CPU (AVX2) wheel of llama-cpp-python (no compiler needed)."""
    cmd = [
        sys.executable, "-m", "pip", "install",
        "llama-cpp-python",
        "--extra-index-url", CPU_WHEEL_INDEX,
    ]
    print("[install] Installing prebuilt llama-cpp-python (CPU/AVX2 wheel)...")
    return subprocess.run(cmd, check=False).returncode


def build_llama_cpp_no_avx() -> int:
    """Build llama-cpp-python from source with all AVX/FMA/F16C extensions disabled.

    Needs a C++ toolchain (Visual Studio Build Tools, "Desktop development with C++").
    We pip-install cmake + ninja so the build generators are on PATH.
    """
    print("[install] AVX2 not available (or LLAMA_NO_AVX set) -> building from source "
          "without AVX. This needs Visual Studio C++ Build Tools and takes a few minutes.")

    print("[install] Ensuring cmake + ninja build tools...")
    bootstrap = subprocess.run(
        [sys.executable, "-m", "pip", "install", "cmake", "ninja"],
        check=False,
    )
    if bootstrap.returncode != 0:
        print("[install] WARNING: failed to install cmake/ninja; the build may fail.")

    env = os.environ.copy()
    env["CMAKE_ARGS"] = NO_AVX_CMAKE_ARGS
    # Force a real source build, ignoring any cached/prebuilt wheel.
    env.setdefault("FORCE_CMAKE", "1")

    cmd = [
        sys.executable, "-m", "pip", "install",
        "llama-cpp-python",
        "--no-binary", "llama-cpp-python",
        "--force-reinstall", "--no-cache-dir", "--verbose",
    ]
    print(f"[install] Building with CMAKE_ARGS={NO_AVX_CMAKE_ARGS}")
    code = subprocess.run(cmd, check=False, env=env).returncode
    if code != 0:
        print(
            "[install] ERROR: source build failed. Most likely the C++ toolchain is "
            "missing. Install 'Visual Studio Build Tools' with the 'Desktop development "
            "with C++' workload, then rerun:  python install_llama_cpp.py"
        )
    return code


def install_llama_cpp() -> int:
    """Pick the right install path for this CPU (prebuilt AVX2 wheel vs no-AVX source build)."""
    force_no_avx = os.environ.get("LLAMA_NO_AVX") == "1"
    if force_no_avx or not cpu_has_avx2():
        return build_llama_cpp_no_avx()
    return install_prebuilt_llama()


def main() -> int:
    # Runtime dependency first so the freshly installed wheel can actually load.
    vc_code = install_vc_redist()
    if vc_code not in _VC_REDIST_OK_CODES:
        return vc_code
    return install_llama_cpp()


if __name__ == "__main__":
    raise SystemExit(main())
