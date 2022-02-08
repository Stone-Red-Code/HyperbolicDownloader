using HyperbolicDownloader.FileProcessing;
using HyperbolicDownloader.Networking;
using HyperbolicDownloader.UserInterface;

using Open.Nat;

using Stone_Red_Utilities.ConsoleExtentions;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;

namespace HyperbolicDownloader;

internal static class Program
{
    public const int BroadcastPort = 2155;

    public static string BasePath { get; } = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location) ?? string.Empty;
    public static string HostsFilePath { get; } = Path.Combine(BasePath, "Hosts.json");
    public static string FilesInfoPath { get; } = Path.Combine(BasePath, "Files.json");

    public static int PublicPort { get; private set; }
    public static int PrivatePort { get; } = 3055;

    public static IPAddress? PublicIpAddress => device?.GetExternalIPAsync().GetAwaiter().GetResult();

    private static NatDevice? device;
    private static Mapping? portMapping;
    private static readonly HostsManager hostsManager = new();
    private static readonly FilesManager filesManager = new();
    private static readonly NetworkClient networkClient = new(filesManager);
    private static readonly Random random = new();

    private static async Task Main(string[] args)
    {
        Console.CancelKeyPress += Console_CancelKeyPress;
        Console.CursorVisible = false;

        if (File.Exists(HostsFilePath))
        {
            string hostsJson = await File.ReadAllTextAsync(HostsFilePath);
            hostsManager.AddRange(JsonSerializer.Deserialize<List<NetworkSocket>>(hostsJson) ?? new());
        }

        if (File.Exists(FilesInfoPath))
        {
            string filesJson = await File.ReadAllTextAsync(FilesInfoPath);
            filesManager.AddRange(JsonSerializer.Deserialize<List<PrivateHyperFileInfo>>(filesJson) ?? new());
        }

        InputHandler inputHandler = new InputHandler(hostsManager, filesManager);

        if (args.Length > 0 && File.Exists(args[0]))
        {
            UserInterface.Commands.DownloadCommands downloadCommands = new UserInterface.Commands.DownloadCommands(hostsManager, filesManager);
            downloadCommands.GetFileFrom(args[0]);
            Console.WriteLine("Do you want to continue using this instance? [y/N]");
            if (char.ToLower(Console.ReadKey().KeyChar) != 'y')
            {
                return;
            }
            Console.WriteLine();
        }

        Console.WriteLine("Searching for a UPnP/NAT-PMP device...");
        _ = await OpenPorts();

        ConsoleExt.WriteLine($"The private IP address is: {NetworkUtilities.GetIP4Adress()} ", ConsoleColor.Green);
        ConsoleExt.WriteLine($"The private port is: {PrivatePort}", ConsoleColor.Green);

        Console.WriteLine("Starting TCP listener...");

        try
        {
            networkClient.ListenTo<NetworkSocket>("GetHostsList", GetHostList);
            networkClient.ListenTo<List<NetworkSocket>>("DiscoverAnswer", DiscoverAnswer);
            networkClient.ListenTo<string>("Message", ReciveMessage);
            networkClient.ListenTo<string>("HasFile", HasFile);
            networkClient.StartListening(PrivatePort);
        }
        catch (SocketException ex)
        {
            ConsoleExt.WriteLine($"An error occurred while starting the TCP listener! Error message: {ex.Message}", ConsoleColor.Red); // net stop hns && net start hns
            Console.ReadKey();
            return;
        }

        BroadcastClient broadcastClient = new BroadcastClient();

        Console.WriteLine("Running local discovery routine...");
        BroadcastClient.Send(BroadcastPort, PrivatePort.ToString());
        await Task.Delay(3000);

        int activeHostsCount = 0;
        if (hostsManager.Count > 0)
        {
            Console.WriteLine("Checking if hosts are active...");
            activeHostsCount = hostsManager.CheckHostsActivity();
        }

        if (activeHostsCount == 0)
        {
            ConsoleExt.WriteLine("No active hosts found!", ConsoleColor.Red);
            ConsoleExt.WriteLine("Use 'add host xxx.xxx.xxx.xxx:yyyy' to add a new host.", ConsoleColor.Red);
        }

        Console.WriteLine($"{hostsManager.Count} known host(s).");
        Console.WriteLine($"{activeHostsCount} active host(s).");
        Console.WriteLine("Starting broadcast listener...");
        broadcastClient.StartListening(BroadcastPort);
        broadcastClient.OnBroadcastRecived += BroadcastClient_OnBroadcastRecived;
        ConsoleExt.WriteLine("Done", ConsoleColor.Green);

        inputHandler.ReadInput();
        ClosePorts();
    }

    private static async void BroadcastClient_OnBroadcastRecived(object? sender, BroadcastRecivedEventArgs recivedEventArgs)
    {
        Debug.WriteLine($"Received broadcast \"{recivedEventArgs.Message}\" from {recivedEventArgs.IPEndPoint.Address}");
        List<NetworkSocket> hostsToSend = hostsManager.ToList();

        NetworkSocket? localSocket = GetLocalSocket();

        if (localSocket is null)
        {
            return;
        }

        hostsToSend.RemoveAll(x => x.IPAddress == recivedEventArgs.IPEndPoint.Address.ToString());
        hostsToSend.Add(localSocket);

        bool success = int.TryParse(recivedEventArgs.Message, out int remotePort);

        if (success)
        {
            hostsManager.Add(new NetworkSocket(recivedEventArgs.IPEndPoint.Address.ToString(), remotePort, DateTime.Now));
            try
            {
                await NetworkClient.SendAsync(recivedEventArgs.IPEndPoint.Address, remotePort, "DiscoverAnswer", hostsToSend);
            }
            catch (SocketException ex)
            {
                Debug.WriteLine(ex);
            }
        }
    }

    private static void DiscoverAnswer(object? sender, MessageRecivedEventArgs<List<NetworkSocket>> recivedEventArgs)
    {
        Console.WriteLine($"Received answer from {recivedEventArgs.IpAddress}. Returned {recivedEventArgs.Data.Count} host(s).");
        hostsManager.AddRange(recivedEventArgs.Data);
    }

    private static void ReciveMessage(object? sender, MessageRecivedEventArgs<string> recivedEventArgs)
    {
        Console.WriteLine($"Received \"{recivedEventArgs.Data}\" from {recivedEventArgs.IpAddress}.");
    }

    private static async void GetHostList(object? sender, MessageRecivedEventArgs<NetworkSocket> recivedEventArgs)
    {
        List<NetworkSocket> hostsToSend = hostsManager.ToList();

        NetworkSocket? localSocket = GetLocalSocket();

        if (localSocket is null)
        {
            return;
        }

        hostsToSend.RemoveAll(x => x.IPAddress == recivedEventArgs.IpAddress.ToString());
        hostsToSend.Add(localSocket);

        if (recivedEventArgs.Data.Port != 0)
        {
            hostsManager.Add(recivedEventArgs.Data);
        }

        await recivedEventArgs.SendResponseAsync(hostsToSend);
    }

    private static async void HasFile(object? sender, MessageRecivedEventArgs<string> recivedEventArgs)
    {
        await recivedEventArgs.SendResponseAsync(filesManager.Contains(recivedEventArgs.Data));
    }

    public static async Task<bool> OpenPorts()
    {
        try
        {
            PublicPort = random.Next(1000, 6000);

            NatDiscoverer? discoverer = new NatDiscoverer();
            device = await discoverer.DiscoverDeviceAsync();

            IPAddress? ip = await device.GetExternalIPAsync();
            ConsoleExt.WriteLine($"The public IP address is: {ip} ", ConsoleColor.Green);

            portMapping = new Mapping(Protocol.Tcp, PrivatePort, PublicPort, "HyperbolicDowloader");

            await device.CreatePortMapAsync(portMapping);

            ConsoleExt.WriteLine($"The public port is: {PublicPort}", ConsoleColor.Green);
            return true;
        }
        catch (NatDeviceNotFoundException)
        {
            ConsoleExt.WriteLine($"Could not find a UPnP or NAT-PMP device!", ConsoleColor.Red);
            return false;
        }
        catch (MappingException ex)
        {
            ConsoleExt.WriteLine($"An error occurred while mapping the private port ({PrivatePort}) to the public port ({PublicPort})! Error message: {ex.Message}", ConsoleColor.Red);
            return false;
        }
    }

    private static void ClosePorts()
    {
        Console.WriteLine("Closing ports...");
        if (device is not null)
        {
            try
            {
                IEnumerable<Mapping>? mappings = device.GetAllMappingsAsync().GetAwaiter().GetResult();
                foreach (Mapping? mapping in mappings)
                {
                    if (mapping.Description.Contains("HyperbolicDowloader") && mapping.PrivateIP.ToString() == portMapping?.PrivateIP.ToString())
                    {
                        device.DeletePortMapAsync(mapping).GetAwaiter().GetResult();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        ConsoleExt.WriteLine("Ports closed!", ConsoleColor.DarkYellow);
        Environment.Exit(0);
    }

    private static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        ClosePorts();
        hostsManager.SaveHosts();
    }

    public static NetworkSocket? GetLocalSocket()
    {
        int port = PublicPort;
        string? ipAddress = null;

        if (device is not null)
        {
            ipAddress = (device.GetExternalIPAsync()).GetAwaiter().GetResult()?.ToString();
        }

        if (ipAddress is null || ipAddress == "0.0.0.0")
        {
            ipAddress = NetworkUtilities.GetIP4Adress()?.ToString();
            port = PrivatePort;
        }

        if (ipAddress is null)
        {
            return null;
        }

        return new NetworkSocket(ipAddress, port, DateTime.Now);
    }
}