using System.ComponentModel;
using Lore.Common.Extensions;
using Lore.Common.Models;
using Lore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lore.Core.Services;

public interface IUserSettingsService
{
    T GetSetting<T>(UserSettingsType settingsType);

    Task InitializeAsync(CancellationToken cancellationToken);
}

public class UserSettingsService(ILogger<UserSettingsService> logger, LoreDbContext dbContext) : IUserSettingsService
{
    private readonly Dictionary<UserSettingsType, string?> _settings = [];

    public T GetSetting<T>(UserSettingsType settingsType)
    {
        if (!_settings.TryGetValue(settingsType, out string? value))
        {
            value = settingsType.GetAttribute<DefaultValueAttribute>()!.Value!.ToString();
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new KeyNotFoundException($"The value of {settingsType} setting cannot be empty!");
        }

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(value, targetType)!;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var allSettings = await dbContext.Settings.ToListAsync(cancellationToken);

        foreach (var setting in allSettings)
        {
            if (!Enum.TryParse(setting.Key, out UserSettingsType settingKey))
            {
                continue;
            }

            _settings[settingKey] = setting.Value;
        }
    }
}