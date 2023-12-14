namespace HyperbolicDownloaderApi.Managment;

public static class ApiConfiguration
{
    public const int BroadcastPort = 2155;
    public const int PrivatePort = 3055;
    public static int PublicPort { get; set; }
    public static string BasePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StoneRed", "HyperbolicDownloader");
    public static string HostsFilePath { get; } = Path.Combine(BasePath, "Hosts.json");
    public static string FilesInfoPath { get; } = Path.Combine(BasePath, "Files.json");
    public static string DirectoriesInfoPath { get; } = Path.Combine(BasePath, "Directories.json");
}