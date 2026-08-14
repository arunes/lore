using Lore.Common.Models;

namespace Lore.Core.Settings;

public interface IUserSettingsService
{
    T GetSetting<T>(UserSettingsType settingsType);

    string? GetResolvedValue(UserSettingsType settingsType);

    Task SaveAsync(IReadOnlyDictionary<UserSettingsType, string?> values, CancellationToken cancellationToken);

    Task InitializeAsync(CancellationToken cancellationToken);
}
