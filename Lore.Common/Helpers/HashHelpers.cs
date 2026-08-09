using System.Security.Cryptography;

namespace Lore.Common.Helpers;

public static class HashHelpers
{
    public static async Task<string> GetFileHashAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Target file could not be found.", filePath);
        }

        using FileStream stream = File.OpenRead(filePath);
        using SHA256 sha256 = SHA256.Create();

        byte[] hashBytes = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}