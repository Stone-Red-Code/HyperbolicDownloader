using Commander_Net;

using HyperbolicDownloader.FileProcessing;
using HyperbolicDownloader.Networking;

using Stone_Red_Utilities.ConsoleExtentions;

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
        commander.Register(AddFile, "add");
        commander.Register(ListFiles, "list");
        this.filesManager = filesManager;
    }

    public void ReadInput()
    {
        while (!exit)
        {
            Console.Write("> ");
            string input = Console.ReadLine() ?? string.Empty;

            if (!commander.Execute(input))
            {
                ConsoleExt.WriteLine("Unknown command!", ConsoleColor.Red);
            }
        }
    }

    private void GetFile(string hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            Console.WriteLine("No hash value specified!");
        }

        foreach (NetworkSocket host in hostsManager.ToList())
        {
            bool validIpAdress = IPAddress.TryParse(host.IPAddress, out IPAddress? ipAddress);

            if (validIpAdress)
            {
                ConsoleExt.Write($"{host.IPAddress}:{host.Port} > ???", ConsoleColor.DarkYellow);
                try
                {
                    Console.CursorLeft = 0;

                    Task<bool> sendTask = NetworkClient.SendAsync<bool>(ipAddress!, host.Port, "HasFile", hash);

                    _ = sendTask.Wait(1000);

                    if (sendTask.Result)
                    {
                        ConsoleExt.WriteLine($"{host.IPAddress}:{host.Port} > Has the requested file", ConsoleColor.Green);
                        Console.WriteLine("Requesting file...");

                        TcpClient tcpClient = new TcpClient();
                        tcpClient.Connect(ipAddress!, host.Port);

                        NetworkStream nwStream = tcpClient.GetStream();
                        byte[] buffer = new byte[tcpClient.ReceiveBufferSize];
                        byte[] reciveBuffer = new byte[1000];

                        byte[] bytesToSend = Encoding.ASCII.GetBytes($"Download {hash}");
                        nwStream.Write(bytesToSend);
                        int bytesRead = nwStream.Read(buffer, 0, tcpClient.ReceiveBufferSize);

                        string dataReceived = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                        if (int.TryParse(dataReceived, out int fileSize)) //If received data is not a size an error occurred
                        {
                            Console.WriteLine($"Starting download...");

                            int totalBytesRead = 0;

                            using FileStream? fileStream = new FileStream($"{hash}.txt", FileMode.Create);

                            while (totalBytesRead < fileSize)
                            {
                                bytesRead = nwStream.Read(reciveBuffer, 0, reciveBuffer.Length);

                                bytesRead = Math.Min(reciveBuffer.Length, fileSize - totalBytesRead);

                                fileStream.Write(reciveBuffer, 0, bytesRead);
                                totalBytesRead += bytesRead;

                                Console.CursorLeft = 0;
                                Console.Out.WriteAsync($"Downloading: {Math.Ceiling(100d / fileSize * totalBytesRead)}% {totalBytesRead}/{fileSize}");
                            }

                            fileStream.Close();
                            //FileCompressor.DecompressFile($"{hash}.gz", $"result.txt");
                            Console.WriteLine();
                            ConsoleExt.WriteLine("Done", ConsoleColor.Green);
                            return;
                        }
                        else
                        {
                            ConsoleExt.WriteLine(dataReceived, ConsoleColor.Red);
                        }
                    }
                    else
                    {
                        ConsoleExt.WriteLine($"{host.IPAddress}:{host.Port} > Does not have the requested file", ConsoleColor.Red);
                    }
                }
                catch (SocketException)
                {
                    Console.CursorLeft = 0;
                    ConsoleExt.WriteLine($"{host.IPAddress}:{host.Port} > Inactive", ConsoleColor.Red);
                }
            }
            else
            {
                hostsManager.Remove(host);
            }
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
}