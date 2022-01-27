namespace HyperbolicDownloader.FileProcessing;

internal class HyperFileInfo
{
    public string Hash { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;

    public HyperFileInfo(string hash, string filePath)
    {
        Hash = hash;
        FilePath = filePath;
    }
}