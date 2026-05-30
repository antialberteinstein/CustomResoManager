"""Mock MCP server simulating the C# resolution tooling layer.

Exposes two tools (list_resolutions, change_resolution) over streamable HTTP.
Calls are logged to stderr and return fake data — no real Win32 access.

Run independently in its own terminal:
    python mock_tooling_mcp_server/server.py

The Python agent connects via http://127.0.0.1:7777/mcp/.
"""
import sys

from mcp.server.fastmcp import FastMCP

HOST = "127.0.0.1"
PORT = 7777

mcp = FastMCP("ResolutionToolingMock")
mcp.settings.host = HOST
mcp.settings.port = PORT


FAKE_RESOLUTIONS = [
    {"width": 3840, "height": 2160, "refreshRate": 60, "isNative": True},
    {"width": 2560, "height": 1440, "refreshRate": 60, "isNative": False},
    {"width": 1920, "height": 1080, "refreshRate": 144, "isNative": False},
    {"width": 1920, "height": 1080, "refreshRate": 60, "isNative": False},
    {"width": 1680, "height": 1050, "refreshRate": 60, "isNative": False},
    {"width": 1600, "height": 900, "refreshRate": 60, "isNative": False},
    {"width": 1366, "height": 768, "refreshRate": 60, "isNative": False},
    {"width": 1280, "height": 720, "refreshRate": 60, "isNative": False},
]

_NATIVE = next(r for r in FAKE_RESOLUTIONS if r.get("isNative"))
_current_state = {
    "width": _NATIVE["width"],
    "height": _NATIVE["height"],
    "refreshRate": _NATIVE["refreshRate"],
}


def _log(message: str) -> None:
    print(f"[mock-mcp] {message}", file=sys.stderr, flush=True)


@mcp.tool()
def list_resolutions() -> list[dict]:
    """Return the list of supported screen resolutions for the primary display."""
    _log("list_resolutions() called")
    return FAKE_RESOLUTIONS


@mcp.tool()
def get_current_resolution() -> dict:
    """Return the current screen resolution of the primary display."""
    _log(f"get_current_resolution() called -> {_current_state}")
    return dict(_current_state)


@mcp.tool()
def change_resolution(width: int, height: int) -> dict:
    """Change the primary display resolution to the requested width x height.

    Args:
        width: target width in pixels (e.g. 1920)
        height: target height in pixels (e.g. 1080)
    """
    global _current_state
    _log(f"change_resolution(width={width}, height={height}) called")

    target = next(
        (r for r in FAKE_RESOLUTIONS if r["width"] == width and r["height"] == height),
        None,
    )
    if target is None:
        _log(f"change_resolution rejected: {width}x{height} not supported")
        return {
            "success": False,
            "applied": None,
            "previous": dict(_current_state),
            "error": f"Resolution {width}x{height} is not in the supported list",
        }

    previous = dict(_current_state)
    _current_state = {
        "width": target["width"],
        "height": target["height"],
        "refreshRate": target["refreshRate"],
    }
    _log(
        f"change_resolution applied: {width}x{height} "
        f"(previous={previous['width']}x{previous['height']})"
    )
    return {
        "success": True,
        "applied": {"width": width, "height": height},
        "previous": previous,
        "error": None,
    }


if __name__ == "__main__":
    print(
        f"[mock-mcp] starting on http://{HOST}:{PORT}/mcp/",
        file=sys.stderr,
        flush=True,
    )
    mcp.run(transport="streamable-http")
