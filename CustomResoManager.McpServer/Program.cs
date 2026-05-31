using CustomResoManager.Core;
using CustomResoManager.McpServer;

// Real MCP tooling server (C#) — drop-in replacement for mock_tooling_mcp_server/server.py.
// The Python agent connects to http://127.0.0.1:7777/mcp/ (see agent/config.py MCP_SERVER_URL).

var builder = WebApplication.CreateBuilder(args);

// Bind address comes from config.yaml (next to the exe); defaults to 127.0.0.1:7777.
// Set host: 0.0.0.0 or auto in config.yaml to serve the agent over the LAN.
var serverConfig = AppConfig.Load(AppContext.BaseDirectory).McpServer;
builder.WebHost.UseUrls(serverConfig.BindUrl);

// Win32-backed resolution manager + server-side revert state, both singletons.
builder.Services.AddSingleton<IResolutionManager, ResolutionManager>();
builder.Services.AddSingleton<IProfileManager>(_ => new ProfileManager());
builder.Services.AddSingleton<IAppEngine, AppEngine>();
builder.Services.AddSingleton<ServerState>();

// Standalone console has no UI message pump, so engine focus-tracking is inert here —
// the DirectUiInvoker just runs inline. Full engine control lives in the WPF app's in-process server.
builder.Services.AddSingleton<IUiInvoker, DirectUiInvoker>();

builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<ResolutionTools>()
    .WithTools<ProfileTools>()
    .WithTools<EngineTools>()
    .WithTools<ProcessTools>();

var app = builder.Build();

// Eagerly capture the native resolution at startup (before any change happens).
app.Services.GetRequiredService<ServerState>();

// Streamable-HTTP MCP endpoint at the configured path (default /mcp).
app.MapMcp(serverConfig.McpPath);

app.Logger.LogInformation("[mcp-csharp] starting, clients connect to {Url}", serverConfig.DisplayUrl);

app.Run();
