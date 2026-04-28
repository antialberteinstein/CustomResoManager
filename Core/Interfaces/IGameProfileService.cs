using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CustomResolutionManager.Models;

namespace CustomResolutionManager.Core.Interfaces;

public interface IGameProfileService
{
    Task<IReadOnlyList<GameProfile>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GameProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GameProfile> CreateAsync(GameProfile profile, CancellationToken cancellationToken = default);
    Task UpdateAsync(GameProfile profile, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task ToggleEnabledAsync(Guid id, bool isEnabled, CancellationToken cancellationToken = default);
}
