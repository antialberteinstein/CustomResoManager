using System.Collections.ObjectModel;
using CustomResolutionManager.Models;
using CustomResolutionManager.ViewModels.Base;

namespace CustomResolutionManager.ViewModels;

public sealed partial class MainDashboardViewModel : ViewModelBase
{
    public ObservableCollection<GameProfile> GameProfiles { get; } = new();
}
