namespace Lore.Common.Models;

public sealed record SettingMetadata(
    string Key,
    string DisplayName,
    string Description,
    string Group,
    string Widget,
    bool IsSecret,
    bool IsRequired,
    bool IsNullable,
    double? Min,
    double? Max,
    double? Step,
    string? Value,
    string? DefaultValue,
    IReadOnlyList<string> ValidValues,
    bool HasOverride);

public sealed record SettingsGroup(
    string Group,
    IReadOnlyList<SettingMetadata> Settings);

public sealed record SettingsResponse(
    IReadOnlyList<SettingsGroup> Groups);

public sealed record SettingValue(
    string Key,
    string? Value);

public sealed record SettingsRequest(
    IReadOnlyList<SettingValue> Settings);
