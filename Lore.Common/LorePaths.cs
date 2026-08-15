namespace Lore.Common;

public static class LorePaths
{
    public static string DataRoot =>
        Environment.GetEnvironmentVariable("LORE_DATA_ROOT")
        ?? Path.Combine(Directory.GetCurrentDirectory(), "lore");

    public static string DatabasePath => Path.Combine(DataRoot, "lore.db");

    public static string OcrModelsDir => Path.Combine(DataRoot, "ocr-models");

    public static string UserDataDir => Path.Combine(DataRoot, "data");

    public static string PresetsDir => Path.Combine(DataRoot, "presets");

    public static bool IsDocker =>
        Environment.GetEnvironmentVariable("LORE_IN_CONTAINER") is not null;

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(OcrModelsDir);
        Directory.CreateDirectory(UserDataDir);
        EnsurePresets();
        EnsureOcrModels();
    }

    private static void EnsurePresets()
    {
        string embeddedPresetsDir = Path.Combine(AppContext.BaseDirectory, "presets");
        if (!Directory.Exists(embeddedPresetsDir))
        {
            Directory.CreateDirectory(PresetsDir);
            return;
        }

        Directory.CreateDirectory(PresetsDir);
        foreach (string embeddedFile in Directory.EnumerateFiles(embeddedPresetsDir, "*.json"))
        {
            string targetPath = Path.Combine(PresetsDir, Path.GetFileName(embeddedFile));
            if (!File.Exists(targetPath))
            {
                File.Copy(embeddedFile, targetPath);
            }
        }
    }

    public static void EnsureOcrModels()
    {
        string embeddedModelsDir = Path.Combine(AppContext.BaseDirectory, "models", "v5");
        if (!Directory.Exists(embeddedModelsDir))
        {
            return;
        }

        Directory.CreateDirectory(OcrModelsDir);
        foreach (string embeddedFile in Directory.EnumerateFiles(embeddedModelsDir))
        {
            string targetPath = Path.Combine(OcrModelsDir, Path.GetFileName(embeddedFile));
            if (!File.Exists(targetPath))
            {
                File.Copy(embeddedFile, targetPath);
            }
        }
    }
}
