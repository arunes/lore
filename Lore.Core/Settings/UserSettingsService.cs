using Lore.Common.Models;
using Lore.Core.Logging;
using Lore.Data;
using Lore.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lore.Core.Settings;

public class UserSettingsService(
    ILogger<UserSettingsService> logger,
    IDbContextFactory<LoreDbContext> dbContextFactory)
    : IUserSettingsService
{
    private readonly Dictionary<UserSettingsType, string?> _settings = [];
    private readonly Lock _settingsLock = new();
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public T GetSetting<T>(UserSettingsType settingsType)
    {
        string? value = GetResolvedValue(settingsType);

        if (string.IsNullOrWhiteSpace(value))
        {
            var definition = SettingsCatalog.ByKey(settingsType);
            if (definition.IsRequired)
            {
                throw new MissingRequiredSettingException(settingsType, definition.DisplayName);
            }

            return default!;
        }

        return ConvertValue<T>(value);
    }

    public string? GetResolvedValue(UserSettingsType settingsType)
    {
        lock (_settingsLock)
        {
            if (_settings.TryGetValue(settingsType, out string? value))
            {
                return value;
            }

            return SettingsCatalog.ByKey(settingsType).DefaultValue?.ToString();
        }
    }

    public async Task SaveAsync(IReadOnlyDictionary<UserSettingsType, string?> values, CancellationToken cancellationToken)
    {
        await _saveLock.WaitAsync(cancellationToken);
        try
        {
            await using LoreDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            foreach (var (settingKey, value) in values)
            {
                var definition = SettingsCatalog.ByKey(settingKey);
                if (definition.IsRequired && string.IsNullOrWhiteSpace(value))
                {
                    throw new ArgumentException(
                        $"Setting '{definition.DisplayName}' ({settingKey}) is required and cannot be empty.",
                        nameof(values));
                }

                string key = settingKey.ToString();
                Setting? existing = await dbContext.Settings.FindAsync([key], cancellationToken);
                if (existing is null)
                {
                    dbContext.Settings.Add(new Setting { Key = key, Value = value });
                }
                else
                {
                    existing.Value = value;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            lock (_settingsLock)
            {
                foreach (var (settingKey, value) in values)
                {
                    _settings[settingKey] = value;
                }
            }
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using LoreDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var allSettings = await dbContext.Settings.ToListAsync(cancellationToken);

        lock (_settingsLock)
        {
            foreach (var setting in allSettings)
            {
                if (!Enum.TryParse(setting.Key, out UserSettingsType settingKey))
                {
                    continue;
                }

                _settings[settingKey] = setting.Value;
            }
        }

        int settingsCount;
        lock (_settingsLock)
        {
            settingsCount = _settings.Count;
        }

        logger.SettingsLoaded(settingsCount);
    }

    public static T ConvertValue<T>(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        if (targetType.IsEnum)
        {
            if (value is string stringValue)
            {
                return (T)Enum.Parse(targetType, stringValue, ignoreCase: true);
            }

            return (T)Enum.ToObject(targetType, value);
        }

        return (T)Convert.ChangeType(value, targetType);
    }
}
