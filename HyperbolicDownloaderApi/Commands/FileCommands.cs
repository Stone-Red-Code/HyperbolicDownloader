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
            ApiManager.SendMessageNewLine($"Added file: {fileInfo!.FilePath}", NotificationMessageType.Success);
            ApiManager.SendMessageNewLine($"Hash: {fileInfo.Hash}");
        }
        else
        {
            ApiManager.SendMessageNewLine(message, NotificationMessageType.Error);
        }
    }

    public void RemoveFile(string hash)
    {
        hash = hash.Trim().ToLower();

        if (filesManager.TryRemove(hash))
        {
            ApiManager.SendMessageNewLine($"Successfully removed file!", NotificationMessageType.Success);
        }
        else
        {
            ApiManager.SendMessageNewLine("The file is not being tracked!", NotificationMessageType.Error);
        }
    }

    public void ListFiles(string _)
    {
        int index = 0;
        List<PrivateHyperFileInfo> fileInfos = filesManager.ToList();

        if (fileInfos.Count == 0)
        {
            ApiManager.SendMessageNewLine("No tracked files!", NotificationMessageType.Warning);
            return;
        }

        foreach (PrivateHyperFileInfo fileInfo in fileInfos)
        {
            index++;
            ApiManager.SendMessageNewLine($"{index}) {fileInfo.FilePath}");
            ApiManager.SendMessageNewLine($"Hash: {fileInfo.Hash}");
            ApiManager.SendMessageNewLine(string.Empty);
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
            ApiManager.SendMessageNewLine("The file is not being tracked!", NotificationMessageType.Error);
            return;
        }

        string fileName = Path.GetFileName(localHyperFileInfo!.FilePath);
        string filePath = Path.Combine(directoryPath, $"{fileName}.hyper");

        PublicHyperFileInfo publicHyperFileInfo = new PublicHyperFileInfo(hash);

        NetworkSocket? localHost = ApiManager.GetLocalSocket();
        if (localHost is null)
        {
            ApiManager.SendMessageNewLine("Network error!", NotificationMessageType.Error);
            return;
        }

        publicHyperFileInfo.Hosts.Add(localHost);

        JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(publicHyperFileInfo, options);

        File.WriteAllText(filePath, json);

        ApiManager.SendMessageNewLine("Done", NotificationMessageType.Success);
        ApiManager.SendMessageNewLine($"File saved at: {Path.GetFullPath(filePath)}");
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
            ApiManager.SendMessageNewLine("The file is not being tracked!", NotificationMessageType.Error);
            return;
        }

        string fileName = Path.GetFileName(localHyperFileInfo!.FilePath);
        string filePath = Path.Combine(directoryPath, $"{fileName}.hyper");

        PublicHyperFileInfo publicHyperFileInfo = new PublicHyperFileInfo(hash);

        NetworkSocket? localHost = ApiManager.GetLocalSocket();
        if (localHost is null)
        {
            ApiManager.SendMessageNewLine("Network error!", NotificationMessageType.Error);
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

            ApiManager.SendMessage($"{host.IPAddress}:{host.Port} > ???", NotificationMessageType.Warning);

            Console.CursorLeft = 0;

            Task<bool> sendTask = NetworkClient.SendAsync<bool>(ipAddress!, host.Port, "HasFile", hash);

            _ = sendTask.Wait(1000);

            if (!sendTask.IsCompletedSuccessfully)
            {
                Console.CursorLeft = 0;
                ApiManager.SendMessageNewLine($"{host.IPAddress}:{host.Port} > Inactive", NotificationMessageType.Error);

                hostsManager.Remove(host);
                continue;
            }
            else if (!sendTask.Result)
            {
                host.LastActive = DateTime.Now;
                ApiManager.SendMessageNewLine($"{host.IPAddress}:{host.Port} > Does not have the requested file", NotificationMessageType.Error);
                continue;
            }

            host.LastActive = DateTime.Now;

            ApiManager.SendMessageNewLine($"{host.IPAddress}:{host.Port} > Has the requested file", NotificationMessageType.Success);
            publicHyperFileInfo.Hosts.Add(host);
        }

        publicHyperFileInfo.Hosts.Add(localHost);

        JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(publicHyperFileInfo, options);

        File.WriteAllText(filePath, json);

        ApiManager.SendMessageNewLine("Done", NotificationMessageType.Success);
        ApiManager.SendMessageNewLine($"File saved at: {Path.GetFullPath(filePath)}");
    }
}