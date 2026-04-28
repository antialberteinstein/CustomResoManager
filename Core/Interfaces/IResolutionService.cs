using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CustomResolutionManager.Models;

namespace CustomResolutionManager.Core.Interfaces;

public interface IResolutionService
{
    Task<IReadOnlyList<ResolutionOption>> GetAvailableResolutionsAsync(CancellationToken cancellationToken = default);
    Task<ResolutionOption?> GetCurrentResolutionAsync(CancellationToken cancellationToken = default);
    Task<bool> IsSupportedAsync(ResolutionOption resolution, CancellationToken cancellationToken = default);
    Task ApplyAsync(ResolutionOption resolution, CancellationToken cancellationToken = default);
    Task RevertAsync(CancellationToken cancellationToken = default);
}
