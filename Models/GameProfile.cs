using System;

namespace CustomResolutionManager.Models;

public sealed record GameProfile
{
    public Guid Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string ExePath { get; init; } = string.Empty;
    public int TargetWidth { get; init; }
    public int TargetHeight { get; init; }
    public bool IsEnabled { get; init; }
    public string? IconPath { get; init; }

    public string TargetResolutionText => $"{TargetWidth} x {TargetHeight}";

    public string IconInitial
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                return "?";
            }

            return char.ToUpperInvariant(DisplayName.Trim()[0]).ToString();
        }
    }
}
