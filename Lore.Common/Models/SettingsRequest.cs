namespace Lore.Common.Models;

public sealed record SettingsRequest(
    IReadOnlyList<SettingValue> Settings);
