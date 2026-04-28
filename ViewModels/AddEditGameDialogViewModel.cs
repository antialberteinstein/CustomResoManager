using System.Collections.ObjectModel;
using CustomResolutionManager.Models;
using CustomResolutionManager.ViewModels.Base;

namespace CustomResolutionManager.ViewModels;

public sealed partial class AddEditGameDialogViewModel : ViewModelBase
{
    public GameProfile? EditingProfile { get; set; }
    public ObservableCollection<ResolutionOption> AvailableResolutions { get; } = new();
}
