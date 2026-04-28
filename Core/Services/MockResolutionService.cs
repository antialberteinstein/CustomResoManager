using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CustomResolutionManager.Core.Interfaces;
using CustomResolutionManager.Models;

namespace CustomResolutionManager.Core.Services;

public sealed class MockResolutionService : IResolutionService
{
    private readonly List<ResolutionOption> _availableResolutions = new()
    {
        new ResolutionOption { Width = 1280, Height = 720, RefreshRate = 60 },
        new ResolutionOption { Width = 1600, Height = 900, RefreshRate = 60 },
        new ResolutionOption { Width = 1920, Height = 1080, RefreshRate = 60 },
        new ResolutionOption { Width = 2560, Height = 1440, RefreshRate = 144 },
        new ResolutionOption { Width = 3440, Height = 1440, RefreshRate = 120 }
    };

    private ResolutionOption _currentResolution = new() { Width = 1920, Height = 1080, RefreshRate = 60 };

    public Task<IReadOnlyList<ResolutionOption>> GetAvailableResolutionsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ResolutionOption> result = _availableResolutions.Select(resolution => resolution with { }).ToArray();
        return Task.FromResult(result);
    }

    public Task<ResolutionOption?> GetCurrentResolutionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ResolutionOption?>(_currentResolution with { });
    }

    public Task<bool> IsSupportedAsync(ResolutionOption resolution, CancellationToken cancellationToken = default)
    {
        bool isSupported = _availableResolutions.Any(item =>
            item.Width == resolution.Width &&
            item.Height == resolution.Height &&
            item.RefreshRate == resolution.RefreshRate);

        return Task.FromResult(isSupported);
    }

    public Task ApplyAsync(ResolutionOption resolution, CancellationToken cancellationToken = default)
    {
        _currentResolution = resolution with { };
        return Task.CompletedTask;
    }

    public Task RevertAsync(CancellationToken cancellationToken = default)
    {
        _currentResolution = new ResolutionOption { Width = 1920, Height = 1080, RefreshRate = 60 };
        return Task.CompletedTask;
    }
}