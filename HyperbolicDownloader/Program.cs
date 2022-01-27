
using HyperbolicDownloader.FileProcessing;
using HyperbolicDownloader.Networking;
using HyperbolicDownloader.UserInterface;

using Open.Nat;

using Stone_Red_Utilities.ConsoleExtentions;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace HyperbolicDownloader;

internal class Program
{
    private const int BroadcastPort = 2155;
    public const string HostsFilePath = "Hosts.json";
    public const string FilesInfoPath = "Files.json";

    private static int publicPort;
    private static readonly int privatePort = 3055;
    private static NatDevice? device;
    private static Mapping? portMapping;
    private static readonly HostsManager hostsManager = new();
    private static readonly FilesManager filesManager = new FilesManager();
    private static readonly NetworkClient networkClient = new(filesManager);
    private static readonly Random random = new Random();

    private static async Task Main()
    {
        Console.CancelKeyPress += Console_CancelKeyPress;

        if (File.Exists(HostsFilePath))
        {
            string hostsJson = await File.ReadAllTextAsync(HostsFilePath);
            hostsManager.AddRange(JsonSerializer.Deserialize<List<NetworkSocket>>(hostsJson) ?? new());
        }

        if (File.Exists(FilesInfoPath))
        {
            string filesJson = await File.ReadAllTextAsync(FilesInfoPath);
            filesManager.AddRange(JsonSerializer.Deserialize<List<HyperFileInfo>>(filesJson) ?? new());
        }

        Console.WriteLine("Searching for a UPnP/NAT-PMP device...");
        _ = await OpenPorts();

        ConsoleExt.WriteLine($"The private IP Address is: {NetworkUtilities.GetIP4Adress()} ", ConsoleColor.Green);
        ConsoleExt.WriteLine($"The private port is: {privatePort}", ConsoleColor.Green);

        Console.WriteLine("Starting TCP listener...");

        try
        {
            networkClient.ListenTo<NetworkSocket>("GetHostsList", GetHostList);
            networkClient.ListenTo<List<NetworkSocket>>("DiscoverAnswer", DiscoverAnswer);
            networkClient.ListenTo<string>("Message", ReciveMessage);
            networkClient.ListenTo<string>("HasFile", HasFile);
            networkClient.StartListening(privatePort);
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"An error occurred while starting the TCP listener! Error message: {ex.Message}");
            return;
        }

        BroadcastClient broadcastClient = new BroadcastClient();

        Console.WriteLine("Running local discovery routine...");
        broadcastClient.Send(BroadcastPort, privatePort.ToString());
        await Task.Delay(5000);

        if (hostsManager.Count > 0)
        {
            Console.WriteLine("Checking if hosts are active...");
            hostsManager.RemoveInactiveHosts();
        }

        if (hostsManager.Count == 0)
        {
            hostsManager.AddRange(await UserInterface.Setup.ConfigureHost());
            Console.WriteLine("Checking if hosts are active...");
            hostsManager.RemoveInactiveHosts();
        }

        Console.WriteLine($"{hostsManager.Count} active host(s).");
        Console.WriteLine("Starting broadcast listener...");
        broadcastClient.StartListening(BroadcastPort);
        broadcastClient.OnBroadcastRecived += BroadcastClient_OnBroadcastRecived;
        ConsoleExt.WriteLine("Done", ConsoleColor.Green);

        new InputHandler(hostsManager, filesManager).ReadInput();
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
            hostsManager.Add(new NetworkSocket(recivedEventArgs.IPEndPoint.Address.ToString(), remotePort));
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
            publicPort = random.Next(1000, 6000);

            NatDiscoverer? discoverer = new NatDiscoverer();
            device = await discoverer.DiscoverDeviceAsync();

            IPAddress? ip = await device.GetExternalIPAsync();
            ConsoleExt.WriteLine($"The public IP Address is: {ip} ", ConsoleColor.Green);

            portMapping = new Mapping(Protocol.Tcp, privatePort, publicPort, "HyperbolicDowloader");

            await device.CreatePortMapAsync(portMapping);

            ConsoleExt.WriteLine($"The public port is: {publicPort}", ConsoleColor.Green);
            return true;
        }
        catch (NatDeviceNotFoundException)
        {
            ConsoleExt.WriteLine($"Could not find a UPnP or NAT-PMP device!", ConsoleColor.Red);
            return false;
        }
        catch (MappingException ex)
        {
            ConsoleExt.WriteLine($"An error occurred while mapping the private port ({privatePort}) to the public port ({publicPort})! Error message: {ex.Message}", ConsoleColor.Red);
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
    }

    public static NetworkSocket? GetLocalSocket()
    {
        int port = publicPort;
        string? ipAddress = null;

        if (device is not null)
        {
            ipAddress = (device.GetExternalIPAsync()).GetAwaiter().GetResult()?.ToString();
        }

        if (ipAddress is null || ipAddress == "0.0.0.0")
        {
            ipAddress = NetworkUtilities.GetIP4Adress()?.ToString();
            port = privatePort;
        }

        if (ipAddress is null)
        {
            return null;
        }

        return new NetworkSocket(ipAddress, port);
    }
}