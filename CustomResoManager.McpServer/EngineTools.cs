using System.ComponentModel;
using CustomResoManager.Core;
using ModelContextProtocol.Server;

namespace CustomResoManager.McpServer;

/// <summary>
/// MCP tools for controlling the focus-tracking engine (the ▶ Start / ⏹ Stop button in the UI).
/// Start/Stop are marshalled onto the UI thread via <see cref="IUiInvoker"/> because the engine's
/// WinEvent hook must be installed on a thread that pumps Windows messages.
/// </summary>
[McpServerToolType]
public sealed class EngineTools
{
    private readonly IAppEngine _engine;
    private readonly IUiInvoker _ui;
    private readonly ILogger<EngineTools> _logger;

    public EngineTools(IAppEngine engine, IUiInvoker ui, ILogger<EngineTools> logger)
    {
        _engine = engine;
        _ui = ui;
        _logger = logger;
    }

    [McpServerTool(Name = "start_engine")]
    [Description("Start the focus-tracking engine. While running, the screen automatically switches to a "
        + "profile's resolution when its app is focused, and returns to the baseline resolution otherwise.")]
    public EngineStatusDto StartEngine()
    {
        _logger.LogInformation("start_engine() called");
        try
        {
            _ui.Invoke(() => _engine.Start());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "start_engine failed");
            return Status(ex.Message);
        }
        return Status();
    }

    [McpServerTool(Name = "stop_engine")]
    [Description("Stop the focus-tracking engine and restore the screen to its baseline (original) resolution.")]
    public EngineStatusDto StopEngine()
    {
        _logger.LogInformation("stop_engine() called");
        try
        {
            _ui.Invoke(() => _engine.Stop());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "stop_engine failed");
            return Status(ex.Message);
        }
        return Status();
    }

    [McpServerTool(Name = "get_engine_status")]
    [Description("Report whether the engine is running and which profile (if any) is currently applied.")]
    public EngineStatusDto GetEngineStatus()
    {
        _logger.LogInformation("get_engine_status() -> running={Running}", _engine.IsRunning);
        return Status();
    }

    private EngineStatusDto Status(string? error = null)
    {
        var active = _engine.CurrentActiveProfile;
        return new EngineStatusDto(
            Running: _engine.IsRunning,
            ActiveProfile: active is null ? null : ProfileTools.ToDto(active),
            Error: error);
    }
}
