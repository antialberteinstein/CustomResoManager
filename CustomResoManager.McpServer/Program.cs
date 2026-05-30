using CustomResoManager.Core;
using CustomResoManager.McpServer;

// Real MCP tooling server (C#) — drop-in replacement for mock_tooling_mcp_server/server.py.
// The Python agent connects to http://127.0.0.1:7777/mcp/ (see agent/config.py MCP_SERVER_URL).

var builder = WebApplication.CreateBuilder(args);

// Match the mock's host/port exactly so the agent needs no config change.
builder.WebHost.UseUrls("http://127.0.0.1:7777");

// Win32-backed resolution manager + server-side revert state, both singletons.
builder.Services.AddSingleton<IResolutionManager, ResolutionManager>();
builder.Services.AddSingleton<ServerState>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<ResolutionTools>();

var app = builder.Build();

// Eagerly capture the native resolution at startup (before any change happens).
app.Services.GetRequiredService<ServerState>();

// Streamable-HTTP MCP endpoint at /mcp (agent URL is http://127.0.0.1:7777/mcp/).
app.MapMcp("/mcp");

app.Logger.LogInformation("[mcp-csharp] starting on http://127.0.0.1:7777/mcp/");

app.Run();
