using HyperbolicDownloader.FileProcessing;
using HyperbolicDownloader.Networking;

using Stone_Red_Utilities.ConsoleExtentions;
using Stone_Red_Utilities.StringExtentions;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace HyperbolicDownloader.UserInterface.Commands;

internal class DownloadCommands
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
            ConsoleExt.WriteLine("Path is empty!", ConsoleColor.Red);
            return;
        }

        string fullPath = Path.GetFullPath(path);

        if (!File.Exists(fullPath))
        {
            ConsoleExt.WriteLine("Invalid file path!", ConsoleColor.Red);
        }

        string json = File.ReadAllText(fullPath);

        PublicHyperFileInfo? publicHyperFileInfo = JsonSerializer.Deserialize<PublicHyperFileInfo>(json);
        if (publicHyperFileInfo == null)
        {
            ConsoleExt.WriteLine("Parsing file failed!", ConsoleColor.Red);
            return;
        }

        hostsManager.AddRange(publicHyperFileInfo.Hosts);
        GetFile(publicHyperFileInfo.Hash);
    }

    public void GetFile(string hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            ConsoleExt.WriteLine("No hash value specified!", ConsoleColor.Red);
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
            Console.WriteLine("Requesting file...");

            using TcpClient tcpClient = new TcpClient();
            tcpClient.Connect(ipAddress!, host.Port);
            tcpClient.ReceiveBufferSize = 64000;

            NetworkStream nwStream = tcpClient.GetStream();
            byte[] buffer = new byte[tcpClient.ReceiveBufferSize];
            byte[] reciveBuffer = new byte[64000];

            byte[] bytesToSend = Encoding.ASCII.GetBytes($"Download {hash}");
            nwStream.Write(bytesToSend);
            nwStream.ReadTimeout = 30000;

            int bytesRead;
            try
            {
                bytesRead = nwStream.Read(buffer, 0, tcpClient.ReceiveBufferSize);
            }
            catch (IOException)
            {
                Console.WriteLine();
                ConsoleExt.WriteLine("Lost connection to other host!", ConsoleColor.Red);
                continue;
            }

            string dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);

            string[] parts = dataReceived.Split('/');

            if (parts.Length == 2) //If received data does not contain 2 parts -> error
            {
                bool validFileSize = int.TryParse(parts[0], out int fileSize);

                if (!validFileSize || fileSize <= 0)
                {
                    ConsoleExt.WriteLine("Invalid file size!", ConsoleColor.Red);
                    continue;
                }

                string fileName = parts[1].ToFileName();
                string directoryPath = Path.Combine(Program.BasePath, "Downloads");
                string filePath = Path.Combine(directoryPath, fileName);

                Console.WriteLine($"File name: {fileName}");
                Console.WriteLine($"Starting download...");

                int totalBytesRead = 0;

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                using FileStream? fileStream = new FileStream(filePath, FileMode.Create);

                int bytesInOneSecond = 0;
                int unitsPerSecond = 0;
                string unit = "Kb";

                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                while (totalBytesRead < fileSize)
                {
                    try
                    {
                        bytesRead = nwStream.Read(reciveBuffer, 0, reciveBuffer.Length);
                    }
                    catch (IOException)
                    {
                        Console.WriteLine();
                        ConsoleExt.WriteLine("Lost connection to other host!", ConsoleColor.Red);
                        break;
                    }

                    bytesRead = Math.Min(bytesRead, fileSize - totalBytesRead);

                    fileStream.Write(reciveBuffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    bytesInOneSecond += bytesRead;

                    if (stopWatch.ElapsedMilliseconds >= 1000)
                    {
                        unitsPerSecond = (unitsPerSecond + bytesInOneSecond) / 2;
                        if (unitsPerSecond > 125000)
                        {
                            unitsPerSecond /= 125000;
                            unit = "Mb";
                        }
                        else
                        {
                            unitsPerSecond /= 125;
                            unit = "Kb";
                        }
                        bytesInOneSecond = 0;
                        stopWatch.Restart();
                    }

                    Console.CursorLeft = 0;

                    Console.Out.WriteAsync($"Downloading: {Math.Clamp(Math.Ceiling(100d / fileSize * totalBytesRead), 0, 100)}% {totalBytesRead / 1000}/{fileSize / 1000}KB [{unitsPerSecond}{unit}/s]    ");
                }

                fileStream.Close();

                if (totalBytesRead < fileSize)
                {
                    continue;
                }

                Console.WriteLine();

                Console.WriteLine("Validating file...");
                if (FileValidator.ValidateHash(filePath, hash))
                {
                    _ = filesManager.TryAdd(filePath, out _, out _);
                }
                else
                {
                    ConsoleExt.WriteLine("Warning: File hash does not match! File might me corrupted or manipulated!", ConsoleColor.DarkYellow);
                }

                Console.WriteLine($"File saved at: {Path.GetFullPath(filePath)}");
                ConsoleExt.WriteLine("Done", ConsoleColor.Green);
                stopWatch.Stop();
                hostsManager.SaveHosts();
                return;
            }
            else
            {
                ConsoleExt.WriteLine(dataReceived, ConsoleColor.Red);
            }
        }
        ConsoleExt.WriteLine("None of the available hosts have the requested file!", ConsoleColor.Red);
        hostsManager.SaveHosts();
    }
}