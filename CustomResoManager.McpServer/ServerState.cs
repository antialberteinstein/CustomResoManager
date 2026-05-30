using CustomResoManager.Core;
using CustomResoManager.Models;

namespace CustomResoManager.McpServer;

/// <summary>
/// Process-wide state for the MCP tooling server, registered as a singleton.
///
/// This is the fix for the known bug: previously the "previous resolution" used for
/// revert lived inside the Python agent's memory and was lost whenever the agent was
/// restarted. Keeping it here means revert survives independently of the agent — as
/// long as the server process stays up.
/// </summary>
public sealed class ServerState
{
    private readonly object _lock = new();

    /// <summary>The resolution active when the server started, treated as "native".</summary>
    public ResolutionModel Native { get; }

    private CurrentResolutionDto? _previous;

    public ServerState(IResolutionManager manager)
    {
        Native = manager.GetCurrentResolution();
        Native.IsNative = true;
    }

    /// <summary>Last resolution we changed away from, used by revert_resolution. Null until a change happens.</summary>
    public CurrentResolutionDto? Previous
    {
        get { lock (_lock) { return _previous; } }
        set { lock (_lock) { _previous = value; } }
    }
}
