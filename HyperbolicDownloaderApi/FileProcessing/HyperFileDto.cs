using System.Text.Json.Serialization;

namespace HyperbolicDownloaderApi.FileProcessing;

internal class HyperFileDto
{
    public string Hash { get; set; }

    public string Name { get; set; }

    [JsonConstructor]
    public HyperFileDto(string hash, string name)
    {
        Hash = hash;
        Name = name;
    }

    public HyperFileDto(PrivateHyperFileInfo privateHyperFileInfo)
    {
        Hash = privateHyperFileInfo.Hash;
        Name = Path.GetFileName(privateHyperFileInfo.FilePath);
    }
}