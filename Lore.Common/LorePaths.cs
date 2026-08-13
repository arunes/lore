namespace Lore.Common;

public static class LorePaths
{
    public static string DataRoot =>
        Environment.GetEnvironmentVariable("LORE_DATA_ROOT")
        ?? Path.Combine(Directory.GetCurrentDirectory(), "lore");

    public static string DatabasePath => Path.Combine(DataRoot, "lore.db");

    public static string OcrModelsDir => Path.Combine(DataRoot, "ocr-models");

    public static string UserDataDir => Path.Combine(DataRoot, "data");

    public static bool IsDocker =>
        Environment.GetEnvironmentVariable("LORE_IN_CONTAINER") is not null;

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(OcrModelsDir);
        Directory.CreateDirectory(UserDataDir);
        EnsureOcrModels();
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
