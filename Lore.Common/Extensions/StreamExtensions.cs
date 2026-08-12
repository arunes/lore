using System.Security.Cryptography;

namespace Lore.Common.Extensions;

public static class StreamExtensions
{
    public static async Task<string> ComputeSha256HexAsync(this Stream stream)
    {
        using SHA256 sha256 = SHA256.Create();

        byte[] hashBytes = await sha256.ComputeHashAsync(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}