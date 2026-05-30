using System.ComponentModel;
using CustomResoManager.Core;
using CustomResoManager.Models;
using ModelContextProtocol.Server;

namespace CustomResoManager.McpServer;

/// <summary>
/// Real MCP tools backed by Win32 (Core/ResolutionManager.cs), replacing the Python
/// mock in mock_tooling_mcp_server/server.py. Tool names and JSON shapes match the
/// mock exactly so the existing agent works without changes.
/// </summary>
[McpServerToolType]
public sealed class ResolutionTools
{
    private readonly IResolutionManager _manager;
    private readonly ServerState _state;
    private readonly ILogger<ResolutionTools> _logger;

    public ResolutionTools(IResolutionManager manager, ServerState state, ILogger<ResolutionTools> logger)
    {
        _manager = manager;
        _state = state;
        _logger = logger;
    }

    [McpServerTool(Name = "list_resolutions")]
    [Description("Return the list of supported screen resolutions for the primary display.")]
    public IReadOnlyList<ResolutionDto> ListResolutions()
    {
        _logger.LogInformation("list_resolutions() called");
        var supported = _manager.GetSupportedResolutions();
        return supported
            .Select(r => new ResolutionDto(r.Width, r.Height, r.RefreshRate, IsNative(r)))
            .ToList();
    }

    [McpServerTool(Name = "get_current_resolution")]
    [Description("Return the current screen resolution of the primary display.")]
    public CurrentResolutionDto GetCurrentResolution()
    {
        var current = _manager.GetCurrentResolution();
        _logger.LogInformation("get_current_resolution() -> {Width}x{Height}", current.Width, current.Height);
        return ToCurrentDto(current);
    }

    [McpServerTool(Name = "change_resolution")]
    [Description("Change the primary display resolution to the requested width x height.")]
    public ChangeResultDto ChangeResolution(
        [Description("Target width in pixels (e.g. 1920)")] int width,
        [Description("Target height in pixels (e.g. 1080)")] int height)
    {
        _logger.LogInformation("change_resolution(width={Width}, height={Height}) called", width, height);
        return Apply(width, height);
    }

    [McpServerTool(Name = "revert_resolution")]
    [Description("Revert the primary display back to the resolution it had before the last change. "
        + "State is kept on the server, so this works even after the agent restarts.")]
    public ChangeResultDto RevertResolution()
    {
        _logger.LogInformation("revert_resolution() called");
        var previous = _state.Previous;
        if (previous is null)
        {
            var current = ToCurrentDto(_manager.GetCurrentResolution());
            _logger.LogInformation("revert_resolution rejected: no previous resolution recorded");
            return new ChangeResultDto(
                Success: false,
                Applied: null,
                Previous: current,
                Error: "Không có độ phân giải trước đó để khôi phục.");
        }

        return Apply(previous.Width, previous.Height);
    }

    /// <summary>
    /// Shared change path used by both change_resolution and revert_resolution.
    /// Validates against the supported list (matching the mock), captures the
    /// pre-change resolution as the new "previous", and wraps Win32 errors as
    /// success:false instead of throwing out of the tool call.
    /// </summary>
    private ChangeResultDto Apply(int width, int height)
    {
        var previous = ToCurrentDto(_manager.GetCurrentResolution());

        var supported = _manager.GetSupportedResolutions();
        if (!supported.Any(r => r.Width == width && r.Height == height))
        {
            _logger.LogInformation("change rejected: {Width}x{Height} not supported", width, height);
            return new ChangeResultDto(
                Success: false,
                Applied: null,
                Previous: previous,
                Error: $"Resolution {width}x{height} is not in the supported list");
        }

        try
        {
            _manager.ChangeResolution(width, height);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ChangeResolution({Width}x{Height}) failed", width, height);
            return new ChangeResultDto(
                Success: false,
                Applied: null,
                Previous: previous,
                Error: ex.Message);
        }

        // Only record "previous" after a successful change, so revert points at the
        // resolution we were actually on before this call.
        _state.Previous = previous;
        _logger.LogInformation(
            "change applied: {Width}x{Height} (previous={PrevW}x{PrevH})",
            width, height, previous.Width, previous.Height);

        return new ChangeResultDto(
            Success: true,
            Applied: new AppliedDto(width, height),
            Previous: previous,
            Error: null);
    }

    private bool IsNative(ResolutionModel r) =>
        r.Width == _state.Native.Width
        && r.Height == _state.Native.Height
        && r.RefreshRate == _state.Native.RefreshRate;

    private static CurrentResolutionDto ToCurrentDto(ResolutionModel r) =>
        new(r.Width, r.Height, r.RefreshRate);
}
