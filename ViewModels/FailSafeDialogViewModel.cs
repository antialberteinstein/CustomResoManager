using CustomResolutionManager.ViewModels.Base;

namespace CustomResolutionManager.ViewModels;

public sealed partial class FailSafeDialogViewModel : ViewModelBase
{
    public int RemainingSeconds { get; set; } = 15;
    public int ProgressPercentage { get; set; }
}
