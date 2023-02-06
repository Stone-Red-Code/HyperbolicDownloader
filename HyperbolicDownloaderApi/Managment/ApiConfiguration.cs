using System.Reflection;

namespace HyperbolicDownloaderApi.Managment;

public static class ApiConfiguration
{
    public const int BroadcastPort = 2155;
    public const int PrivatePort = 3055;
    public static int PublicPort { get; set; }
    public static string BasePath { get; } = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location) ?? string.Empty;
    public static string HostsFilePath { get; } = Path.Combine(BasePath, "Hosts.json");
    public static string FilesInfoPath { get; } = Path.Combine(BasePath, "Files.json");
}