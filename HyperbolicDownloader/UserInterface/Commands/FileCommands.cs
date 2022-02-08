using HyperbolicDownloader.FileProcessing;
using HyperbolicDownloader.Networking;

using Stone_Red_Utilities.ConsoleExtentions;

using System.Net;
using System.Text.Json;

namespace HyperbolicDownloader.UserInterface.Commands;

internal class FileCommands
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
            ConsoleExt.WriteLine($"Added file: {fileInfo!.FilePath}", ConsoleColor.Green);
            Console.WriteLine($"Hash: {fileInfo.Hash}");
        }
        else
        {
            ConsoleExt.WriteLine(message, ConsoleColor.Red);
        }
    }

    public void RemoveFile(string hash)
    {
        hash = hash.Trim().ToLower();

        if (filesManager.TryRemove(hash))
        {
            ConsoleExt.WriteLine($"Removed file successfully!", ConsoleColor.Green);
        }
        else
        {
            ConsoleExt.WriteLine("The file is not being tracked!", ConsoleColor.Red);
        }
    }

    public void ListFiles(string _)
    {
        int index = 0;
        List<PrivateHyperFileInfo> fileInfos = filesManager.ToList();

        if (fileInfos.Count == 0)
        {
            ConsoleExt.WriteLine("No tracked files!", ConsoleColor.DarkYellow);
            return;
        }

        foreach (PrivateHyperFileInfo fileInfo in fileInfos)
        {
            index++;
            Console.WriteLine($"{index}) {fileInfo.FilePath}");
            Console.WriteLine($"Hash: {fileInfo.Hash}");
            Console.WriteLine();
        }
        Console.CursorTop--;
    }

    public void GenerateFileSingle(string hash)
    {
        string directoryPath = Path.Combine(Program.BasePath, "GeneratedFiles");
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        hash = hash.Trim().ToLower();

        if (!filesManager.TryGet(hash, out PrivateHyperFileInfo? localHyperFileInfo))
        {
            ConsoleExt.WriteLine("The file is not being tracked!", ConsoleColor.Red);
            return;
        }

        string fileName = Path.GetFileName(localHyperFileInfo!.FilePath);
        string filePath = Path.Combine(directoryPath, $"{fileName}.hyper");

        PublicHyperFileInfo publicHyperFileInfo = new PublicHyperFileInfo(hash);

        NetworkSocket? localHost = Program.GetLocalSocket();
        if (localHost is null)
        {
            ConsoleExt.WriteLine("Network error!", ConsoleColor.Red);
            return;
        }

        publicHyperFileInfo.Hosts.Add(localHost);

        JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(publicHyperFileInfo, options);

        File.WriteAllText(filePath, json);

        ConsoleExt.WriteLine("Done", ConsoleColor.Green);
        Console.WriteLine($"File saved at: {Path.GetFullPath(filePath)}");
    }

    public void GenerateFileFull(string hash)
    {
        string directoryPath = Path.Combine(Program.BasePath, "GeneratedFiles");
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        hash = hash.Trim().ToLower();

        if (!filesManager.TryGet(hash, out PrivateHyperFileInfo? localHyperFileInfo))
        {
            ConsoleExt.WriteLine("The file is not being tracked!", ConsoleColor.Red);
            return;
        }

        string fileName = Path.GetFileName(localHyperFileInfo!.FilePath);
        string filePath = Path.Combine(directoryPath, $"{fileName}.hyper");

        PublicHyperFileInfo publicHyperFileInfo = new PublicHyperFileInfo(hash);

        NetworkSocket? localHost = Program.GetLocalSocket();
        if (localHost is null)
        {
            ConsoleExt.WriteLine("Network error!", ConsoleColor.Red);
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

            ConsoleExt.Write($"{host.IPAddress}:{host.Port} > ???", ConsoleColor.DarkYellow);

            Console.CursorLeft = 0;

            Task<bool> sendTask = NetworkClient.SendAsync<bool>(ipAddress!, host.Port, "HasFile", hash);

            _ = sendTask.Wait(1000);

            if (!sendTask.IsCompletedSuccessfully)
            {
                Console.CursorLeft = 0;
                ConsoleExt.WriteLine($"{host.IPAddress}:{host.Port} > Inactive", ConsoleColor.Red);

                hostsManager.Remove(host);
                continue;
            }
            else if (!sendTask.Result)
            {
                host.LastActive = DateTime.Now;
                ConsoleExt.WriteLine($"{host.IPAddress}:{host.Port} > Does not have the requested file", ConsoleColor.Red);
                continue;
            }

            host.LastActive = DateTime.Now;

            ConsoleExt.WriteLine($"{host.IPAddress}:{host.Port} > Has the requested file", ConsoleColor.Green);
            publicHyperFileInfo.Hosts.Add(host);
        }

        publicHyperFileInfo.Hosts.Add(localHost);

        JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(publicHyperFileInfo, options);

        File.WriteAllText(filePath, json);

        ConsoleExt.WriteLine("Done", ConsoleColor.Green);
        Console.WriteLine($"File saved at: {Path.GetFullPath(filePath)}");
    }
}