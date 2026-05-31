using System.Text.Json.Serialization;

namespace CustomResoManager.McpServer;

/// <summary>
/// Wire-format DTOs that mirror EXACTLY what the Python mock server returned, so the
/// existing agent (agent/chat_service.py) keeps working unchanged. The agent reads
/// camelCase keys (e.g. r["refreshRate"], r.get("isNative")) — the explicit
/// [JsonPropertyName] attributes guarantee those keys regardless of serializer policy.
/// </summary>
public sealed record ResolutionDto(
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("refreshRate")] int RefreshRate,
    [property: JsonPropertyName("isNative")] bool IsNative);

/// <summary>Current resolution shape: {"width", "height", "refreshRate"}.</summary>
public sealed record CurrentResolutionDto(
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("refreshRate")] int RefreshRate);

/// <summary>The "applied" sub-object on a change result: {"width", "height"}.</summary>
public sealed record AppliedDto(
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height);

/// <summary>
/// Result of change_resolution / revert_resolution:
/// {"success", "applied" (nullable), "previous", "error" (nullable)}.
/// </summary>
public sealed record ChangeResultDto(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("applied")] AppliedDto? Applied,
    [property: JsonPropertyName("previous")] CurrentResolutionDto Previous,
    [property: JsonPropertyName("error")] string? Error);

/// <summary>
/// A saved app→resolution profile, mirroring <c>Models/GameProfile.cs</c> on the wire:
/// {"processName", "width", "height", "refreshRate" (nullable), "aspectRatio", "enabled"}.
/// </summary>
public sealed record ProfileDto(
    [property: JsonPropertyName("processName")] string ProcessName,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("refreshRate")] int? RefreshRate,
    [property: JsonPropertyName("aspectRatio")] string AspectRatio,
    [property: JsonPropertyName("enabled")] bool Enabled);

/// <summary>
/// Result of a profile mutation (add/remove/enable):
/// {"success", "profile" (nullable), "error" (nullable)}.
/// </summary>
public sealed record ProfileResultDto(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("profile")] ProfileDto? Profile,
    [property: JsonPropertyName("error")] string? Error);

/// <summary>
/// State of the focus-tracking engine:
/// {"running", "activeProfile" (nullable), "error" (nullable)}.
/// </summary>
public sealed record EngineStatusDto(
    [property: JsonPropertyName("running")] bool Running,
    [property: JsonPropertyName("activeProfile")] ProfileDto? ActiveProfile,
    [property: JsonPropertyName("error")] string? Error);

/// <summary>A running process with a real window: {"processName", "title"}.</summary>
public sealed record RunningProcessDto(
    [property: JsonPropertyName("processName")] string ProcessName,
    [property: JsonPropertyName("title")] string Title);

/// <summary>An installed app from a Start Menu shortcut: {"name", "processName"}.</summary>
public sealed record InstalledAppDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("processName")] string ProcessName);
