using HyperbolicDownloaderApi.FileProcessing;
using HyperbolicDownloaderApi.Managment;
using HyperbolicDownloaderApi.Networking;

using System.Net;
using System.Text.Json;

namespace HyperbolicDownloaderApi.Commands;

public class FileCommands
{
    private readonly HostsManager hostsManager;

    private readonly FilesManager filesManager;

    public FileCommands(HostsManager hostsManager, FilesManager filesManager)
    {
        this.hostsManager = hostsManager;
        this.filesManager = filesManager;
    }

    public void AddFile(string path)
    {
        if (filesManager.TryAdd(path, out PrivateHyperFileInfo? fileInfo, out string? message))
        {
            ApiManager.SendNotificationMessageNewLine($"Added file: {fileInfo!.FilePath}", NotificationMessageType.Success);
            ApiManager.SendNotificationMessageNewLine($"Hash: {fileInfo.Hash}");
        }
        else
        {
            ApiManager.SendNotificationMessageNewLine(message!, NotificationMessageType.Error);
        }
    }

    public void RemoveFile(string hash)
    {
        hash = hash.Trim().ToLower();

        if (filesManager.TryRemove(hash))
        {
            ApiManager.SendNotificationMessageNewLine($"Successfully removed file!", NotificationMessageType.Success);
        }
        else
        {
            ApiManager.SendNotificationMessageNewLine("The file is not being tracked!", NotificationMessageType.Error);
        }
    }

    public void ListFiles(string _)
    {
        int index = 0;
        List<PrivateHyperFileInfo> fileInfos = filesManager.ToList();

        if (fileInfos.Count == 0)
        {
            ApiManager.SendNotificationMessageNewLine("No tracked files!", NotificationMessageType.Warning);
            return;
        }

        foreach (PrivateHyperFileInfo fileInfo in fileInfos)
        {
            index++;
            ApiManager.SendNotificationMessageNewLine($"{index}) {fileInfo.FilePath}");
            ApiManager.SendNotificationMessageNewLine($"Hash: {fileInfo.Hash}");
            ApiManager.SendNotificationMessageNewLine(string.Empty);
        }
        Console.CursorTop--;
    }

    public void GenerateFileSingle(string hash)
    {
        string directoryPath = Path.Combine(ApiConfiguration.BasePath, "GeneratedFiles");
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        hash = hash.Trim().ToLower();

        if (!filesManager.TryGet(hash, out PrivateHyperFileInfo? localHyperFileInfo))
        {
            ApiManager.SendNotificationMessageNewLine("The file is not being tracked!", NotificationMessageType.Error);
            return;
        }

        string fileName = Path.GetFileName(localHyperFileInfo!.FilePath);
        string filePath = Path.Combine(directoryPath, $"{fileName}.hyper");

        PublicHyperFileInfo publicHyperFileInfo = new PublicHyperFileInfo(hash);

        NetworkSocket? localHost = ApiManager.GetLocalSocket();
        if (localHost is null)
        {
            ApiManager.SendNotificationMessageNewLine("Network error!", NotificationMessageType.Error);
            return;
        }

        publicHyperFileInfo.Hosts.Add(localHost);

        JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(publicHyperFileInfo, options);

        File.WriteAllText(filePath, json);

        ApiManager.SendNotificationMessageNewLine("Done", NotificationMessageType.Success);
        ApiManager.SendNotificationMessageNewLine($"File saved at: {Path.GetFullPath(filePath)}");
    }

    public void GenerateFileFull(string hash)
    {
        string directoryPath = Path.Combine(ApiConfiguration.BasePath, "GeneratedFiles");
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        hash = hash.Trim().ToLower();

        if (!filesManager.TryGet(hash, out PrivateHyperFileInfo? localHyperFileInfo))
        {
            ApiManager.SendNotificationMessageNewLine("The file is not being tracked!", NotificationMessageType.Error);
            return;
        }

        string fileName = Path.GetFileName(localHyperFileInfo!.FilePath);
        string filePath = Path.Combine(directoryPath, $"{fileName}.hyper");

        PublicHyperFileInfo publicHyperFileInfo = new PublicHyperFileInfo(hash);

        NetworkSocket? localHost = ApiManager.GetLocalSocket();
        if (localHost is null)
        {
            ApiManager.SendNotificationMessageNewLine("Network error!", NotificationMessageType.Error);
            return;
        }

        foreach (NetworkSocket host in hostsManager.ToList())
        {
            bool validIpAdress = IPAddress.TryParse(host.IPAddress, out IPAddress? ipAddress);

            if (!validIpAdress)
            {
                hostsManager.Remove(host, true);
                continue;
            }

            ApiManager.SendNotificationMessage($"{host.IPAddress}:{host.Port} > ???", NotificationMessageType.Warning);

            Console.CursorLeft = 0;

            Task<bool> sendTask = NetworkClient.SendAsync<bool>(ipAddress!, host.Port, "HasFile", hash);

            _ = sendTask.Wait(1000);

            if (!sendTask.IsCompletedSuccessfully)
            {
                Console.CursorLeft = 0;
                ApiManager.SendNotificationMessageNewLine($"{host.IPAddress}:{host.Port} > Inactive", NotificationMessageType.Error);

                hostsManager.Remove(host);
                continue;
            }
            else if (!sendTask.Result)
            {
                host.LastActive = DateTime.Now;
                ApiManager.SendNotificationMessageNewLine($"{host.IPAddress}:{host.Port} > Does not have the requested file", NotificationMessageType.Error);
                continue;
            }

            host.LastActive = DateTime.Now;

            ApiManager.SendNotificationMessageNewLine($"{host.IPAddress}:{host.Port} > Has the requested file", NotificationMessageType.Success);
            publicHyperFileInfo.Hosts.Add(host);
        }

        publicHyperFileInfo.Hosts.Add(localHost);

        JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(publicHyperFileInfo, options);

        File.WriteAllText(filePath, json);

        ApiManager.SendNotificationMessageNewLine("Done", NotificationMessageType.Success);
        ApiManager.SendNotificationMessageNewLine($"File saved at: {Path.GetFullPath(filePath)}");
    }
}