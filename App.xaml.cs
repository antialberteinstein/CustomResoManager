using System.Windows;
using System.Windows.Threading;
using CustomResoManager.Core;
using CustomResoManager.McpServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CustomResoManager;

public partial class App : Application
{
    public static IResolutionManager ResolutionManager { get; private set; } = null!;
    public static IProfileManager ProfileManager { get; private set; } = null!;
    public static IAppEngine AppEngine { get; private set; } = null!;

    /// <summary>App-wide config (MCP server bind + agent chat endpoint) from config.yaml.</summary>
    public static AppConfig Config { get; private set; } = new();

    private WebApplication? _mcpHost;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Load config first so MainWindow (created via StartupUri after this returns) can read it.
        Config = AppConfig.Load(AppContext.BaseDirectory);

        ResolutionManager = new ResolutionManager();
        ProfileManager = new ProfileManager();
        AppEngine = new AppEngine(ResolutionManager, ProfileManager);

        _ = StartMcpServerAsync();
    }

    private async Task StartMcpServerAsync()
    {
        try
        {
            // MCP bind address from config.yaml (mcpServer section); defaults to 127.0.0.1:7777.
            // Set host: 0.0.0.0 or auto to expose the MCP server to agents on the LAN.
            var cfg = Config.McpServer;

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls(cfg.BindUrl);

            builder.Services.AddSingleton<IResolutionManager>(_ => ResolutionManager);
            builder.Services.AddSingleton<IProfileManager>(_ => ProfileManager);
            builder.Services.AddSingleton<IAppEngine>(_ => AppEngine);
            builder.Services.AddSingleton<ServerState>();

            // Engine start/stop must run on the WPF UI thread (its WinEvent hook needs a
            // message pump); MCP tools call from Kestrel threads, so marshal via the dispatcher.
            builder.Services.AddSingleton<IUiInvoker>(new WpfUiInvoker(Dispatcher));

            // Rút ngắn thời gian shutdown để StopAsync không chờ drain các kết nối
            // MCP (SSE long-lived) khi đóng app.
            builder.Services.Configure<HostOptions>(o =>
                o.ShutdownTimeout = TimeSpan.FromMilliseconds(500));

            builder.Services
                .AddMcpServer()
                .WithHttpTransport()
                .WithTools<ResolutionTools>()
                .WithTools<ProfileTools>()
                .WithTools<EngineTools>()
                .WithTools<ProcessTools>();

            _mcpHost = builder.Build();
            _mcpHost.Services.GetRequiredService<ServerState>();
            _mcpHost.MapMcp(cfg.McpPath);
            System.Diagnostics.Debug.WriteLine($"[MCP Server] listening, clients connect to {cfg.DisplayUrl}");

            // StartAsync (thay vì RunAsync) để không gắn console lifetime và trả về
            // ngay sau khi khởi động — vòng đời do OnExit kiểm soát.
            await _mcpHost.StartAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MCP Server] Failed to start: {ex.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // Trả về độ phân giải gốc trước khi thoát (idempotent — OnClosed thường đã gọi).
        AppEngine?.Stop();

        base.OnExit(e);

        // Kết liễu tiến trình NGAY & dứt khoát. KHÔNG chờ MCP StopAsync (dễ treo trên
        // UI thread do kết nối SSE long-lived) — Environment.Exit hạ toàn bộ luồng nền
        // gồm Kestrel/MCP, bảo đảm không còn server chạy nền sau khi đóng app.
        Environment.Exit(0);
    }

    /// <summary>
    /// Runs engine start/stop on the WPF UI thread so AppEngine's WinEvent hook is installed
    /// on a thread that pumps Windows messages (MCP tools invoke from Kestrel threads).
    /// </summary>
    private sealed class WpfUiInvoker : IUiInvoker
    {
        private readonly Dispatcher _dispatcher;
        public WpfUiInvoker(Dispatcher dispatcher) => _dispatcher = dispatcher;
        public void Invoke(Action action) => _dispatcher.Invoke(action);
    }
}
