using System;
using System.Threading;
using System.Threading.Tasks;

namespace CustomResolutionManager.Core.Interfaces;

public interface IProcessMonitorService
{
    event EventHandler<string>? GameStarted;
    event EventHandler<string>? GameExited;

    bool IsMonitoring { get; }

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task RegisterGameAsync(string exePath, CancellationToken cancellationToken = default);
    Task UnregisterGameAsync(string exePath, CancellationToken cancellationToken = default);
}
