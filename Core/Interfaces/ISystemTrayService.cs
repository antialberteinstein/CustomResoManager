using System.Threading.Tasks;

namespace CustomResolutionManager.Core.Interfaces;

public interface ISystemTrayService
{
    Task ShowIconAsync();
    Task HideIconAsync();
    Task MinimizeToTrayAsync();
    Task RestoreFromTrayAsync();
}
