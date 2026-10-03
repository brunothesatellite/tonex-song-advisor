using System.Security.Cryptography;

namespace TonexAdvisor.Core.Data;

/// <summary>
/// Hashes a library file so tests can prove nothing was written to it.
/// </summary>
public static class FileIntegrity
{
    /// <summary>Returns the lowercase hexadecimal SHA-256 of <paramref name="path"/>.</summary>
    public static string Sha256(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
