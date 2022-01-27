using System.Security.Cryptography;

namespace HyperbolicDownloader.FileProcessing;

internal class FileValidator
{
    public static async Task<string> CalculateHashAsync(string filePath)
    {
        using SHA512 sha = SHA512.Create();
        using FileStream? stream = File.OpenRead(filePath);
        byte[]? hash = await sha.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public static string CalculateHash(string filePath)
    {
        return CalculateHashAsync(filePath).GetAwaiter().GetResult();
    }

    public static async Task<bool> ValidateHashAsync(string filePath, string hash)
    {
        return await CalculateHashAsync(filePath) == hash;
    }

    public static bool ValidateHash(string filePath, string hash)
    {
        return CalculateHash(filePath) == hash;
    }
}