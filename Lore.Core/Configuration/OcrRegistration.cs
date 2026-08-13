using Lore.Common;
using Lore.Common.Models;
using Lore.Core.Settings;
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
            LorePaths.EnsureOcrModels();
            string modelsDir = LorePaths.OcrModelsDir;

            var detFileName = userSettings.GetSetting<string>(UserSettingsType.OCRModelsDetFileName);
            var clsFileName = userSettings.GetSetting<string>(UserSettingsType.OCRModelsClsFileName);
            var recFileName = userSettings.GetSetting<string>(UserSettingsType.OCRModelsRecFileName);
            var keysFileName = userSettings.GetSetting<string>(UserSettingsType.OCRModelsKeysFileName);

            var ocr = new RapidOcr();
            ocr.InitModels(
                detPath: Path.Combine(modelsDir, detFileName),
                clsPath: Path.Combine(modelsDir, clsFileName),
                recPath: Path.Combine(modelsDir, recFileName),
                keysPath: Path.Combine(modelsDir, keysFileName)
            );

            return ocr;
        });
    }
}