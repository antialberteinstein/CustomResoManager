namespace CustomResolutionManager.Models;

public sealed record ResolutionOption
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int RefreshRate { get; init; }
    public string DisplayText => $"{Width} x {Height} @ {RefreshRate} Hz";
}
