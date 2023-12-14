namespace HyperbolicDownloaderApi.FileProcessing;

public class PrivateHyperFileInfo(string hash, string filePath)
{
    public string Hash { get; set; } = hash;
    public string FilePath { get; set; } = filePath;
}