using Lore.Common.Models;

namespace Lore.Core.Settings;

public interface IUserSettingsService
{
    T GetSetting<T>(UserSettingsType settingsType);

    Task InitializeAsync(CancellationToken cancellationToken);
}