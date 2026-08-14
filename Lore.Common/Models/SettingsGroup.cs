namespace Lore.Common.Models;

public sealed record SettingsGroup(
    string Group,
    IReadOnlyList<SettingMetadata> Settings);