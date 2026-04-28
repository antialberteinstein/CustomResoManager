using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CustomResolutionManager.Core.Interfaces;
using CustomResolutionManager.Models;

namespace CustomResolutionManager.Core.Services;

public sealed class MockGameProfileService : IGameProfileService
{
    private readonly List<GameProfile> _profiles = new()
    {
        new GameProfile
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            DisplayName = "Cyber Drift",
            ExePath = @"C:\Games\CyberDrift\CyberDrift.exe",
            TargetWidth = 2560,
            TargetHeight = 1440,
            IsEnabled = true,
            IconPath = null
        },
        new GameProfile
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            DisplayName = "Skyforge Arena",
            ExePath = @"D:\SteamLibrary\SkyforgeArena\Arena.exe",
            TargetWidth = 1920,
            TargetHeight = 1080,
            IsEnabled = false,
            IconPath = null
        },
        new GameProfile
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            DisplayName = "Void Harbor",
            ExePath = @"E:\Games\VoidHarbor\VoidHarbor-Win64-Shipping.exe",
            TargetWidth = 3440,
            TargetHeight = 1440,
            IsEnabled = true,
            IconPath = null
        }
    };

    public Task<IReadOnlyList<GameProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GameProfile> result = _profiles.Select(profile => profile with { }).ToArray();
        return Task.FromResult(result);
    }

    public Task<GameProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        GameProfile? profile = _profiles.FirstOrDefault(item => item.Id == id);
        return Task.FromResult(profile is null ? null : profile with { });
    }

    public Task<GameProfile> CreateAsync(GameProfile profile, CancellationToken cancellationToken = default)
    {
        GameProfile createdProfile = profile.Id == Guid.Empty
            ? profile with { Id = Guid.NewGuid() }
            : profile with { };

        _profiles.Add(createdProfile);
        return Task.FromResult(createdProfile with { });
    }

    public Task UpdateAsync(GameProfile profile, CancellationToken cancellationToken = default)
    {
        int index = _profiles.FindIndex(item => item.Id == profile.Id);
        if (index >= 0)
        {
            _profiles[index] = profile with { };
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _profiles.RemoveAll(profile => profile.Id == id);
        return Task.CompletedTask;
    }

    public Task ToggleEnabledAsync(Guid id, bool isEnabled, CancellationToken cancellationToken = default)
    {
        int index = _profiles.FindIndex(profile => profile.Id == id);
        if (index >= 0)
        {
            GameProfile profile = _profiles[index];
            _profiles[index] = profile with { IsEnabled = isEnabled };
        }

        return Task.CompletedTask;
    }
}