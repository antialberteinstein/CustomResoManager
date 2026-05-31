using System.Windows;
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

            // Rút ngắn thời gian shutdown để StopAsync không chờ drain các kết nối
            // MCP (SSE long-lived) khi đóng app.
            builder.Services.Configure<HostOptions>(o =>
                o.ShutdownTimeout = TimeSpan.FromMilliseconds(500));

            builder.Services
                .AddMcpServer()
                .WithHttpTransport()
                .WithTools<ResolutionTools>();

            _mcpHost = builder.Build();
            _mcpHost.Services.GetRequiredService<ServerState>();
            _mcpHost.MapMcp("/mcp");

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
        // An toàn: dừng engine + trả về độ phân giải gốc dù app đóng theo đường nào.
        // (Stop là idempotent — MainWindow.OnClosed thường đã gọi trước đó.)
        AppEngine?.Stop();

        // Dừng MCP server NGAY khi Close (timeout ngắn, không chờ drain kết nối).
        try
        {
            _mcpHost?.StopAsync(TimeSpan.FromMilliseconds(500)).GetAwaiter().GetResult();
            (_mcpHost as IDisposable)?.Dispose();
        }
        catch { /* best-effort: vẫn thoát dù MCP dừng lỗi */ }

        base.OnExit(e);

        // Bảo đảm tiến trình thoát ngay lập tức kể cả khi Kestrel/kết nối MCP còn
        // luồng nền giữ tiến trình sống.
        Environment.Exit(0);
    }
}
