using HyperbolicDownloaderApi.FileProcessing;
using HyperbolicDownloaderApi.Managment;
using HyperbolicDownloaderApi.Networking;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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

    public void RemoveFile(string args)
    {
        args = args.Trim().ToLower();

        if (int.TryParse(args, out int index))
        {
            List<PrivateHyperFileInfo> fileInfos = filesManager.ToList();

            if (index < 1 || index > fileInfos.Count)
            {
                ApiManager.SendNotificationMessageNewLine("Invalid index!", NotificationMessageType.Error);
                return;
            }

            args = fileInfos[index - 1].Hash;
        }

        if (filesManager.TryRemove(args))
        {
            ApiManager.SendNotificationMessageNewLine($"Successfully removed file!", NotificationMessageType.Success);
        }
        else
        {
            ApiManager.SendNotificationMessageNewLine("The file is not being tracked!", NotificationMessageType.Error);
        }
    }

    public void ListFiles(string searchString)
    {
        int index = 0;
        int fileCount = 0;
        List<PrivateHyperFileInfo> fileInfos = filesManager.ToList();

        if (fileInfos.Count == 0)
        {
            ApiManager.SendNotificationMessageNewLine("No tracked files!", NotificationMessageType.Warning);
            return;
        }

        foreach (PrivateHyperFileInfo fileInfo in fileInfos)
        {
            index++;

            if (!string.IsNullOrWhiteSpace(searchString) && !fileInfo.FilePath.Contains(searchString, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            fileCount++;

            ApiManager.SendNotificationMessageNewLine($"{index}) {fileInfo.FilePath}");
            ApiManager.SendNotificationMessageNewLine($"Hash: {fileInfo.Hash}");
            ApiManager.SendNotificationMessageNewLine(string.Empty);
        }

        if (fileCount == 0)
        {
            ApiManager.SendNotificationMessage($"No files found containing \"{searchString}\".", NotificationMessageType.Warning);
        }
        else
        {
            Console.CursorTop--;
        }
    }

    public void ListFilesRemote(string args)
    {
        args = args.Trim();

        string searchString = string.Empty;

        if (args.Contains(' '))
        {
            searchString = args.Substring(args.IndexOf(' ') + 1, args.Length - args.IndexOf(' ') - 1);
            args = args[..args.IndexOf(' ')];
        }

        if (int.TryParse(args, out int index))
        {
            List<NetworkSocket> hosts = hostsManager.ToList();

            if (index < 1 || index > hosts.Count)
            {
                ApiManager.SendNotificationMessageNewLine("Invalid index!", NotificationMessageType.Error);
                return;
            }

            args = $"{hosts[index - 1].IPAddress}:{hosts[index - 1].Port}";
        }

        string[] parts = args.Split(":");

        if (parts.Length != 2)
        {
            ApiManager.SendNotificationMessageNewLine("Invalid format! Use this format: (xxx.xxx.xxx.xxx:yyyy)", NotificationMessageType.Error);
            return;
        }

        string ipAddressInput = parts[0];
        string portInput = parts[1];
        IPAddress? ipAddress;

        _ = int.TryParse(portInput, out int port);

        if (port is < 1000 or >= 6000)
        {
            ApiManager.SendNotificationMessageNewLine("Invalid port number!", NotificationMessageType.Error);
            return;
        }
        else if (!IPAddress.TryParse(ipAddressInput, out ipAddress))
        {
            try
            {
                ipAddress = Dns.GetHostAddresses(ipAddressInput).FirstOrDefault();
            }
            catch (SocketException ex)
            {
                Debug.WriteLine(ex);
            }
        }

        if (ipAddress is null)
        {
            ApiManager.SendNotificationMessageNewLine("Invalid IP address!", NotificationMessageType.Error);
            return;
        }

        Task<List<HyperFileDto>?> sendTask = NetworkClient.SendAsync<List<HyperFileDto>>(ipAddress, port, "GetFilesList", searchString);

        _ = sendTask.Wait(1000);

        if (!sendTask.IsCompletedSuccessfully)
        {
            ApiManager.SendNotificationMessageNewLine("Invalid host!", NotificationMessageType.Error);
            return;
        }

        List<HyperFileDto>? fileInfos = sendTask.Result;

        if (fileInfos?.Count == 0 && !string.IsNullOrEmpty(searchString))
        {
            ApiManager.SendNotificationMessageNewLine($"No files found containing \"{searchString}\".", NotificationMessageType.Warning);
        }
        else if (fileInfos is null || fileInfos.Count == 0)
        {
            ApiManager.SendNotificationMessageNewLine("No tracked files!", NotificationMessageType.Warning);
            return;
        }

        foreach (HyperFileDto fileInfo in fileInfos)
        {
            index++;

            ApiManager.SendNotificationMessageNewLine($"{index}) {fileInfo.Name}");
            ApiManager.SendNotificationMessageNewLine($"Hash: {fileInfo.Hash}");
            ApiManager.SendNotificationMessageNewLine(string.Empty);
        }
        Console.CursorTop--;
    }

    public void GenerateFileSingle(string args)
    {
        string directoryPath = Path.Combine(ApiConfiguration.BasePath, "GeneratedFiles");
        if (!Directory.Exists(directoryPath))
        {
            _ = Directory.CreateDirectory(directoryPath);
        }

        args = args.Trim().ToLower();

        PrivateHyperFileInfo? localHyperFileInfo;

        if (int.TryParse(args, out int index))
        {
            List<PrivateHyperFileInfo> fileInfos = filesManager.ToList();

            if (index < 1 || index > fileInfos.Count)
            {
                ApiManager.SendNotificationMessageNewLine("Invalid index!", NotificationMessageType.Error);
                return;
            }

            localHyperFileInfo = fileInfos[index - 1];
        }
        else if (!filesManager.TryGet(args, out localHyperFileInfo))
        {
            ApiManager.SendNotificationMessageNewLine("The file is not being tracked!", NotificationMessageType.Error);
            return;
        }

        string fileName = Path.GetFileName(localHyperFileInfo!.FilePath);
        string filePath = Path.Combine(directoryPath, $"{fileName}.hyper");

        PublicHyperFileInfo publicHyperFileInfo = new PublicHyperFileInfo(args);

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

    public void GenerateFileFull(string args)
    {
        string directoryPath = Path.Combine(ApiConfiguration.BasePath, "GeneratedFiles");
        if (!Directory.Exists(directoryPath))
        {
            _ = Directory.CreateDirectory(directoryPath);
        }

        args = args.Trim().ToLower();

        PrivateHyperFileInfo? localHyperFileInfo;

        if (int.TryParse(args, out int index))
        {
            List<PrivateHyperFileInfo> fileInfos = filesManager.ToList();

            if (index < 1 || index > fileInfos.Count)
            {
                ApiManager.SendNotificationMessageNewLine("Invalid index!", NotificationMessageType.Error);
                return;
            }

            localHyperFileInfo = fileInfos[index - 1];
        }
        else if (!filesManager.TryGet(args, out localHyperFileInfo))
        {
            ApiManager.SendNotificationMessageNewLine("The file is not being tracked!", NotificationMessageType.Error);
            return;
        }

        string fileName = Path.GetFileName(localHyperFileInfo!.FilePath);
        string filePath = Path.Combine(directoryPath, $"{fileName}.hyper");

        PublicHyperFileInfo publicHyperFileInfo = new PublicHyperFileInfo(args);

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

            Task<bool> sendTask = NetworkClient.SendAsync<bool>(ipAddress!, host.Port, "HasFile", args);

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