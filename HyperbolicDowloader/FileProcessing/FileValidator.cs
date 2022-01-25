using System.Security.Cryptography;

namespace HyperbolicDowloader.FileProcessing;

internal class FileValidator
{
    public static async Task<string> CalculateHashAsync(string filePath)
    {
        using SHA512 sha = SHA512.Create();
        using FileStream? stream = File.OpenRead(filePath);
        byte[]? hash = await sha.ComputeHashAsync(stream);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public static async Task<bool> ValidateHashAsync(string filePath, string hash)
    {
        return await CalculateHashAsync(filePath) == hash;
    }
}