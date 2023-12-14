using HyperbolicDownloaderApi.FileProcessing;
using HyperbolicDownloaderApi.Managment;
using HyperbolicDownloaderApi.Networking;
using HyperbolicDownloaderApi.Utilities;

using Stone_Red_Utilities.StringExtentions;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace HyperbolicDownloaderApi.Commands;

public class DownloadCommands
{
    private readonly HostsManager hostsManager;
    private readonly FilesManager filesManager;

    public DownloadCommands(HostsManager hostsManager, FilesManager filesManager)
    {
        this.hostsManager = hostsManager;
        this.filesManager = filesManager;
    }

    public void GetFileFrom(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            ApiManager.SendNotificationMessageNewLine("Path is empty!", NotificationMessageType.Error);
            return;
        }

        string fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            ApiManager.SendNotificationMessageNewLine("Invalid file path!", NotificationMessageType.Error);
        }

        string json = File.ReadAllText(fullPath);

        PublicHyperFileInfo? publicHyperFileInfo = JsonSerializer.Deserialize<PublicHyperFileInfo>(json);
        if (publicHyperFileInfo == null)
        {
            ApiManager.SendNotificationMessageNewLine("Parsing file failed!", NotificationMessageType.Error);
            return;
        }

        _ = hostsManager.AddRange(publicHyperFileInfo.Hosts);
        GetFile(publicHyperFileInfo.Hash);
    }

    public void GetFile(string hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            ApiManager.SendNotificationMessageNewLine("No hash value specified!", NotificationMessageType.Error);
            return;
        }

        hash = hash.Trim().ToLower();

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
            ApiManager.SendNotificationMessageNewLine("Requesting file...");

            using TcpClient tcpClient = new TcpClient();
            tcpClient.Connect(ipAddress!, host.Port);
            tcpClient.ReceiveBufferSize = 64000;

            NetworkStream nwStream = tcpClient.GetStream();
            byte[] buffer = new byte[tcpClient.ReceiveBufferSize];
            byte[] reciveBuffer = new byte[64000];

            byte[] bytesToSend = Encoding.ASCII.GetBytes($"Download {hash}");
            nwStream.Write(bytesToSend);
            nwStream.ReadTimeout = 5000;

            int bytesRead;
            try
            {
                bytesRead = nwStream.Read(buffer, 0, 1000);
            }
            catch (IOException)
            {
                ApiManager.SendNotificationMessageNewLine(string.Empty);
                ApiManager.SendNotificationMessageNewLine("Lost connection to other host!", NotificationMessageType.Error);
                continue;
            }

            string dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);

            string[] parts = dataReceived.Split('/');

            if (parts.Length != 2) //If received data does not contain 2 parts -> error
            {
                ApiManager.SendNotificationMessageNewLine(dataReceived, NotificationMessageType.Error);
                continue;
            }

            bool validFileSize = int.TryParse(parts[0], out int fileSize);

            if (!validFileSize || fileSize <= 0)
            {
                ApiManager.SendNotificationMessageNewLine("Invalid file size!", NotificationMessageType.Error);
                continue;
            }

            string fileName = parts[1].ToFileName();
            string directoryPath = Path.Combine(ApiConfiguration.BasePath, "Downloads");
            string filePath = Path.Combine(directoryPath, fileName);

            ApiManager.SendNotificationMessageNewLine($"File name: {fileName}");
            ApiManager.SendNotificationMessageNewLine($"Starting download...");

            int totalBytesRead = 0;

            if (!Directory.Exists(directoryPath))
            {
                _ = Directory.CreateDirectory(directoryPath);
            }

            using FileStream? fileStream = new FileStream(filePath, FileMode.Create);

            int bytesPerSecond = 0;
            int transferRate = 0;

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            while (totalBytesRead < fileSize)
            {
                try
                {
                    bytesRead = nwStream.Read(reciveBuffer, 0, reciveBuffer.Length);
                }
                catch (IOException ex)
                {
                    Debug.WriteLine(ex);
                    ApiManager.SendNotificationMessageNewLine(string.Empty);
                    ApiManager.SendNotificationMessageNewLine("Lost connection to other host!", NotificationMessageType.Error);
                    break;
                }

                bytesRead = Math.Min(bytesRead, fileSize - totalBytesRead);

                fileStream.Write(reciveBuffer, 0, bytesRead);
                totalBytesRead += bytesRead;

                bytesPerSecond += bytesRead;

                if (stopWatch.Elapsed.TotalSeconds >= 1)
                {
                    transferRate = bytesPerSecond;
                    host.DownloadSpeed = (host.DownloadSpeed + bytesPerSecond) / 2;
                    bytesPerSecond = 0;
                    stopWatch.Restart();
                }

                ApiManager.SendNotificationMessage($"\rDownloading: {Math.Clamp(Math.Ceiling(100d / fileSize * totalBytesRead), 0, 100)}% {UnitFormatter.FileSize(totalBytesRead)}/{UnitFormatter.FileSize(fileSize)} [{UnitFormatter.TransferRate(transferRate)}]      ");
            }

            fileStream.Close();

            if (totalBytesRead < fileSize)
            {
                continue;
            }

            ApiManager.SendNotificationMessageNewLine(string.Empty);

            ApiManager.SendNotificationMessageNewLine("Validating file...");
            if (FileValidator.ValidateHash(filePath, hash))
            {
                _ = filesManager.TryAdd(filePath, out _, out _);
            }
            else
            {
                ApiManager.SendNotificationMessageNewLine("Warning: File hash does not match! File might me corrupted or manipulated! Trying next host...", NotificationMessageType.Warning);
                continue;
            }

            ApiManager.SendNotificationMessageNewLine($"File saved at: {Path.GetFullPath(filePath)}");
            ApiManager.SendNotificationMessageNewLine("Done", NotificationMessageType.Success);
            stopWatch.Stop();
            hostsManager.SaveHosts();
            return;
        }
        ApiManager.SendNotificationMessageNewLine("None of the available hosts have the requested file!", NotificationMessageType.Error);
        hostsManager.SaveHosts();
    }
}