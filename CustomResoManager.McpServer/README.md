# CustomResoManager.McpServer

Real C# MCP tooling server — a drop-in replacement for the Python mock in
`../mock_tooling_mcp_server/`. It exposes the same tools over the same
Streamable-HTTP MCP endpoint, but backs them with real Win32 calls via
`Core/ResolutionManager.cs` instead of fake data.

## Requirements

- **Windows** (the tools P/Invoke `user32.dll` for `EnumDisplaySettings` /
  `ChangeDisplaySettings`).
- .NET 8 SDK.

## Run

```powershell
dotnet run --project CustomResoManager.McpServer
```

Listens on `http://127.0.0.1:7777/mcp/` — the exact URL the agent already uses
(`agent/config.py` → `MCP_SERVER_URL`). Stop the Python mock first so the port
is free; the agent then talks to this server with **no config change**.

## Tools (contract matches the mock exactly)

| Tool | Input | Output |
|------|-------|--------|
| `list_resolutions` | `{}` | `[{width, height, refreshRate, isNative}, ...]` |
| `get_current_resolution` | `{}` | `{width, height, refreshRate}` |
| `change_resolution` | `{width, height}` | `{success, applied, previous, error}` |
| `revert_resolution` | `{}` | `{success, applied, previous, error}` |

`revert_resolution` is new: the "previous" resolution is held **server-side**
(`ServerState`), so revert keeps working even after the Python agent restarts —
this fixes the bug where the agent lost its in-memory revert target.

## NuGet note

The MCP SDK package (`ModelContextProtocol.AspNetCore`) is in preview. If the
pinned version in the `.csproj` fails to restore, pull the latest with:

```powershell
dotnet add CustomResoManager.McpServer package ModelContextProtocol.AspNetCore --prerelease
```

## Endpoint path

The agent connects to `.../mcp/` (trailing slash). This server maps MCP at
`/mcp` via `app.MapMcp("/mcp")`. If you hit a 404 on the trailing-slash form,
adjust the mapped path in `Program.cs` to match your SDK version.
