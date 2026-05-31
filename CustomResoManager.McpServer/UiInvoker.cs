namespace CustomResoManager.McpServer;

/// <summary>
/// Marshals an action onto the thread that owns the app's WinEvent message pump.
///
/// AppEngine.Start() registers a SetWinEventHook with WINEVENT_OUTOFCONTEXT, whose
/// callbacks are delivered to the *calling thread's* message queue. MCP tool calls run
/// on Kestrel thread-pool threads (no message loop), so engine start/stop must be
/// marshalled onto the WPF UI (dispatcher) thread. This abstraction keeps EngineTools
/// free of any WPF dependency: the in-process server registers a dispatcher-backed
/// implementation, while the standalone dev server registers <see cref="DirectUiInvoker"/>.
/// </summary>
public interface IUiInvoker
{
    /// <summary>Run <paramref name="action"/> on the UI thread and block until it completes.</summary>
    void Invoke(Action action);
}

/// <summary>
/// No-op marshaller used by the standalone console server, which has no UI message pump.
/// Engine focus-tracking won't receive WinEvent callbacks here, but tool calls won't crash —
/// the standalone project is a developer harness; full engine control lives in the WPF app.
/// </summary>
public sealed class DirectUiInvoker : IUiInvoker
{
    public void Invoke(Action action) => action();
}
