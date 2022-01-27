using Commander_Net;

using HyperbolicDownloader.FileProcessing;
using HyperbolicDownloader.Networking;

using Stone_Red_Utilities.ConsoleExtentions;
using Stone_Red_Utilities.StringExtentions;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace HyperbolicDownloader.UserInterface;

internal class InputHandler
{
    private readonly HostsManager hostsManager;
    private readonly FilesManager filesManager;
    private readonly Commander commander = new Commander();
    private bool exit = false;

    public InputHandler(HostsManager hostsManager, FilesManager filesManager)
    {
        this.hostsManager = hostsManager;
        commander.Register((_) => Console.Clear(), "clear");
        commander.Register(Exit, "exit");
        commander.Register(GetFile, "get");
        Command addCommand = commander.Register(AddFile, "add");
        addCommand.Register(AddHost, "host");
        addCommand.Register(AddFile, "file");
        commander.Register(RemoveFile, "remove");
        commander.Register(ListFiles, "list");

        this.filesManager = filesManager;
    }

    public void ReadInput()
    {
        while (!exit)
        {
            Console.Write("> ");
            Console.CursorVisible = true;

            string input = Console.ReadLine()?.Trim() ?? string.Empty;

            Console.CursorVisible = false;
            if (!commander.Execute(input))
            {
                ConsoleExt.WriteLine("Unknown command!", ConsoleColor.Red);
            }
        }
    }

    private void AddHost(string args)
    {
        string[] parts = args.Split(":");

        if (parts.Length != 2)
        {
            ConsoleExt.WriteLine("Invalid format! Use this format: (xxx.xxx.xxx.xxx:yyyy)", ConsoleColor.Red);
            return;
        }

        string ipAddressInput = parts[0];
        string portInput = parts[1];

        _ = int.TryParse(portInput, out int port);
        if (port < 1000 || port >= 6000)
        {
            ConsoleExt.WriteLine("Invalid port number!", ConsoleColor.Red);
        }
        else if (IPAddress.TryParse(ipAddressInput, out IPAddress? ipAddress))
        {
            try
            {
                Console.WriteLine("Waiting for response...");
                NetworkSocket? localSocket = Program.GetLocalSocket() ?? new NetworkSocket("0.0.0.0", 0);
                List<NetworkSocket>? recivedHosts = NetworkClient.Send<List<NetworkSocket>>(ipAddress, port, "GetHostsList", localSocket);

                if (recivedHosts is not null)
                {
                    ConsoleExt.WriteLine($"Success! Added {recivedHosts.Count} new host(s).", ConsoleColor.Green);
                    hostsManager.AddRange(recivedHosts);
                }
                else
                {
                    ConsoleExt.WriteLine($"Invalid response!", ConsoleColor.Red);
                }
            }
            catch (SocketException ex)
            {
                ConsoleExt.WriteLine($"Invalid host! Error message: {ex.Message}", ConsoleColor.Red);
            }
        }
        else
        {
            ConsoleExt.WriteLine("Invalid IP address!", ConsoleColor.Red);
        }
    }

    private void AddFile(string args)
    {
        if (filesManager.TryAdd(args, out HyperFileInfo? fileInfo, out string? message))
        {
            ConsoleExt.WriteLine($"Added file: {fileInfo!.FilePath}", ConsoleColor.Green);
            Console.WriteLine($"Hash: {fileInfo.Hash}");
        }
        else
        {
            ConsoleExt.WriteLine(message, ConsoleColor.Red);
        }
    }

    private void RemoveFile(string args)
    {
        if (filesManager.TryRemove(args))
        {
            ConsoleExt.WriteLine($"Removed file successfully!", ConsoleColor.Green);
        }
        else
        {
            ConsoleExt.WriteLine("The file is not being tracked!", ConsoleColor.Red);
        }
    }

    private void ListFiles(string _)
    {
        int index = 0;
        foreach (HyperFileInfo fileInfo in filesManager.ToList())
        {
            index++;
            Console.WriteLine($"{index}) {fileInfo.FilePath}");
            Console.WriteLine($"Hash: {fileInfo.Hash}");
            Console.WriteLine();
        }
    }

    private void Exit(string _)
    {
        exit = true;
    }

    private void GetFile(string hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            Console.WriteLine("No hash value specified!");
        }

        hash = hash.Trim();

        foreach (NetworkSocket host in hostsManager.ToList())
        {
            bool validIpAdress = IPAddress.TryParse(host.IPAddress, out IPAddress? ipAddress);

            if (!validIpAdress)
            {
                hostsManager.Remove(host);
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
                ConsoleExt.WriteLine($"{host.IPAddress}:{host.Port} > Does not have the requested file", ConsoleColor.Red);
                continue;
            }

            ConsoleExt.WriteLine($"{host.IPAddress}:{host.Port} > Has the requested file", ConsoleColor.Green);
            Console.WriteLine("Requesting file...");

            TcpClient tcpClient = new TcpClient();
            tcpClient.Connect(ipAddress!, host.Port);
            tcpClient.ReceiveBufferSize = 64000;

            NetworkStream nwStream = tcpClient.GetStream();
            byte[] buffer = new byte[tcpClient.ReceiveBufferSize];
            byte[] reciveBuffer = new byte[64000];

            byte[] bytesToSend = Encoding.ASCII.GetBytes($"Download {hash}");
            nwStream.Write(bytesToSend);
            nwStream.ReadTimeout = 5000;
            int bytesRead = nwStream.Read(buffer, 0, tcpClient.ReceiveBufferSize);

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

                Console.WriteLine($"File name: {fileName}");
                Console.WriteLine($"Starting download...");

                int totalBytesRead = 0;

                if (!Directory.Exists("./Downloads"))
                {
                    Directory.CreateDirectory("./Downloads");
                }

                using FileStream? fileStream = new FileStream($"./Downloads/{fileName}", FileMode.Create);

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
                            unitsPerSecond = unitsPerSecond / 125000;
                            unit = "Mb";
                        }
                        else
                        {
                            unitsPerSecond = unitsPerSecond / 125;
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
                if (!FileValidator.ValidateHash($"./Downloads/{fileName}", hash))
                {
                    ConsoleExt.WriteLine("Warning: File hash does not match! File might me corrupted or manipulated!", ConsoleColor.DarkYellow);
                }

                Console.WriteLine($"File saved at: {Path.GetFullPath($"./Downloads/{fileName}")}");
                ConsoleExt.WriteLine("Done", ConsoleColor.Green);
                stopWatch.Stop();
                return;
            }
            else
            {
                ConsoleExt.WriteLine(dataReceived, ConsoleColor.Red);
            }
        }
        ConsoleExt.WriteLine("None of the available hosts have the requested file!", ConsoleColor.Red);
    }
}