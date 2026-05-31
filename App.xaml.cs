using System.Windows;
using CustomResoManager.Core;
using CustomResoManager.McpServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CustomResoManager;

public partial class App : Application
{
    public static IResolutionManager ResolutionManager { get; private set; } = null!;
    public static IProfileManager ProfileManager { get; private set; } = null!;
    public static IAppEngine AppEngine { get; private set; } = null!;

    private WebApplication? _mcpHost;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ResolutionManager = new ResolutionManager();
        ProfileManager = new ProfileManager();
        AppEngine = new AppEngine(ResolutionManager, ProfileManager);

        _ = StartMcpServerAsync();
    }

    private async Task StartMcpServerAsync()
    {
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:7777");

            builder.Services.AddSingleton<IResolutionManager>(_ => ResolutionManager);
            builder.Services.AddSingleton<ServerState>();

            builder.Services
                .AddMcpServer()
                .WithHttpTransport()
                .WithTools<ResolutionTools>();

            _mcpHost = builder.Build();
            _mcpHost.Services.GetRequiredService<ServerState>();
            _mcpHost.MapMcp("/mcp");

            await _mcpHost.RunAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MCP Server] Failed to start: {ex.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mcpHost?.StopAsync().GetAwaiter().GetResult();
        base.OnExit(e);
    }
}
