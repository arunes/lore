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