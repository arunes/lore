using Lore.Common.Models;
using Lore.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using RapidOcrNet;

namespace Lore.Core.Configuration;

public static class OcrRegistration
{
    public static IServiceCollection AddOcrServices(this IServiceCollection services)
    {
        return services.AddSingleton(sp =>
        {
            var userSettings = sp.GetRequiredService<IUserSettingsService>();
            var modelsRoot = userSettings.GetSetting<string>(UserSettingsType.OCRModelsRootPath);
            var modelsPath = Path.Combine(modelsRoot, "models", "v5");

            var detFileName = userSettings.GetSetting<string>(UserSettingsType.OCRModelsDetFileName);
            var clsFileName = userSettings.GetSetting<string>(UserSettingsType.OCRModelsClsFileName);
            var recFileName = userSettings.GetSetting<string>(UserSettingsType.OCRModelsRecFileName);
            var keysFileName = userSettings.GetSetting<string>(UserSettingsType.OCRModelsKeysFileName);

            var ocr = new RapidOcr();
            ocr.InitModels(
                detPath: Path.Combine(modelsPath, detFileName),
                clsPath: Path.Combine(modelsPath, clsFileName),
                recPath: Path.Combine(modelsPath, recFileName),
                keysPath: Path.Combine(modelsPath, keysFileName)
            );

            return ocr;
        });
    }
}