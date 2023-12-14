using HyperbolicDownloaderApi.Networking;

namespace HyperbolicDownloaderApi.FileProcessing;

public class PublicHyperFileInfo
{
    public string Hash { get; set; } = string.Empty;
    public List<NetworkSocket> Hosts { get; set; } = [];

    public PublicHyperFileInfo()
    {
    }

    public PublicHyperFileInfo(string hash)
    {
        Hash = hash;
    }

    public PublicHyperFileInfo(string hash, List<NetworkSocket> hosts)
    {
        Hash = hash;
        Hosts = hosts;
    }
}