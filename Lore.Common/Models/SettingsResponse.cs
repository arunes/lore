namespace Lore.Common.Models;

public sealed record SettingsResponse(
    IReadOnlyList<SettingsGroup> Groups);
