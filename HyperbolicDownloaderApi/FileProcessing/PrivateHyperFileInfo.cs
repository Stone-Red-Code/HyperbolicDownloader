namespace HyperbolicDownloaderApi.FileProcessing;

public class PrivateHyperFileInfo
{
    public string Hash { get; set; }
    public string FilePath { get; set; }

    public PrivateHyperFileInfo(string hash, string filePath)
    {
        Hash = hash;
        FilePath = filePath;
    }
}