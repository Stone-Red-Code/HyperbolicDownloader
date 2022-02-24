using HyperbolicDownloaderApi.FileProcessing;
using HyperbolicDownloaderApi.Networking;

using Open.Nat;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace HyperbolicDownloaderApi.Managment;

public class ApiManager
{
    public static event EventHandler<NotificationMessageEventArgs>? OnNotificationMessageRecived;

    public static IPAddress? PublicIpAddress => device?.GetExternalIPAsync().GetAwaiter().GetResult();
    public FilesManager FilesManager { get; } = new();
    public HostsManager HostsManager { get; } = new();

    private static readonly Random random = new();
    private static NatDevice? device;
    private static Mapping? portMapping;
    private readonly BroadcastClient broadcastClient = new BroadcastClient();
    private readonly NetworkClient networkClient;

    public ApiManager()
    {
        networkClient = new(FilesManager);
    }

    public static void ClosePorts()
    {
        ApiManager.SendMessageNewLine("Closing ports...", NotificationMessageType.Success);
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
                ApiManager.SendMessageNewLine(ex.ToString(), NotificationMessageType.Error);
            }
        }

        ApiManager.SendMessageNewLine("Ports closed!", NotificationMessageType.Warning);
        Environment.Exit(0);
    }

    public static NetworkSocket? GetLocalSocket()
    {
        int port = ApiConfiguration.PublicPort;
        string? ipAddress = null;

        if (device is not null)
        {
            ipAddress = (device.GetExternalIPAsync()).GetAwaiter().GetResult()?.ToString();
        }

        if (ipAddress is null || ipAddress == "0.0.0.0")
        {
            ipAddress = NetworkUtilities.GetIP4Adress()?.ToString();
            port = ApiConfiguration.PrivatePort;
        }

        if (ipAddress is null)
        {
            return null;
        }

        return new NetworkSocket(ipAddress, port, DateTime.Now);
    }

    public static async Task<bool> OpenPorts()
    {
        try
        {
            ApiConfiguration.PublicPort = random.Next(1000, 6000);

            NatDiscoverer? discoverer = new NatDiscoverer();
            device = await discoverer.DiscoverDeviceAsync();

            IPAddress? ip = await device.GetExternalIPAsync();
            ApiManager.SendMessageNewLine($"The public IP address is: {ip} ", NotificationMessageType.Success);

            portMapping = new Mapping(Protocol.Tcp, ApiConfiguration.PrivatePort, ApiConfiguration.PublicPort, "HyperbolicDowloader");

            await device.CreatePortMapAsync(portMapping);

            ApiManager.SendMessageNewLine($"The public port is: {ApiConfiguration.PublicPort}", NotificationMessageType.Success);
            return true;
        }
        catch (NatDeviceNotFoundException)
        {
            ApiManager.SendMessageNewLine($"Could not find a UPnP or NAT-PMP device!", NotificationMessageType.Error);
            return false;
        }
        catch (MappingException ex)
        {
            ApiManager.SendMessageNewLine($"An error occurred while mapping the private port ({ApiConfiguration.PrivatePort}) to the public port ({ApiConfiguration.PublicPort})! Error message: {ex.Message}", NotificationMessageType.Error);
            return false;
        }
    }

    public void StartBroadcastListener()
    {
        broadcastClient.StartListening(ApiConfiguration.BroadcastPort);
        broadcastClient.OnBroadcastRecived += BroadcastClient_OnBroadcastRecived;
    }

    public void StartTcpListener()
    {
        try
        {
            networkClient.ListenTo<NetworkSocket>("GetHostsList", GetHostList);
            networkClient.ListenTo<List<NetworkSocket>>("DiscoverAnswer", DiscoverAnswer);
            networkClient.ListenTo<string>("Message", ReciveMessage);
            networkClient.ListenTo<string>("HasFile", HasFile);
            networkClient.StartListening(ApiConfiguration.PrivatePort);
        }
        catch (SocketException ex)
        {
            ApiManager.SendMessageNewLine($"An error occurred while starting the TCP listener! Error message: {ex.Message}", NotificationMessageType.Error); // net stop hns && net start hns
            Console.ReadKey();
        }
    }

    private async void BroadcastClient_OnBroadcastRecived(object? sender, BroadcastRecivedEventArgs recivedEventArgs)
    {
        Debug.WriteLine($"Received broadcast \"{recivedEventArgs.Message}\" from {recivedEventArgs.IPEndPoint.Address}");
        List<NetworkSocket> hostsToSend = HostsManager.ToList();

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
            HostsManager.Add(new NetworkSocket(recivedEventArgs.IPEndPoint.Address.ToString(), remotePort, DateTime.Now));
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

    private void DiscoverAnswer(object? sender, MessageRecivedEventArgs<List<NetworkSocket>> recivedEventArgs)
    {
        ApiManager.SendMessageNewLine($"Received answer from {recivedEventArgs.IpAddress}. Returned {recivedEventArgs.Data.Count} host(s).", NotificationMessageType.Raw);
        HostsManager.AddRange(recivedEventArgs.Data);
    }

    private async void GetHostList(object? sender, MessageRecivedEventArgs<NetworkSocket> recivedEventArgs)
    {
        List<NetworkSocket> hostsToSend = HostsManager.ToList();

        NetworkSocket? localSocket = GetLocalSocket();

        if (localSocket is null)
        {
            return;
        }

        hostsToSend.RemoveAll(x => x.IPAddress == recivedEventArgs.IpAddress.ToString());
        hostsToSend.Add(localSocket);

        if (recivedEventArgs.Data.Port != 0)
        {
            HostsManager.Add(recivedEventArgs.Data);
        }

        await recivedEventArgs.SendResponseAsync(hostsToSend);
    }

    private async void HasFile(object? sender, MessageRecivedEventArgs<string> recivedEventArgs)
    {
        await recivedEventArgs.SendResponseAsync(FilesManager.Contains(recivedEventArgs.Data));
    }

    private void ReciveMessage(object? sender, MessageRecivedEventArgs<string> recivedEventArgs)
    {
        ApiManager.SendMessageNewLine($"Received \"{recivedEventArgs.Data}\" from {recivedEventArgs.IpAddress}.", NotificationMessageType.Raw);
    }

    internal static void SendMessage(string message, NotificationMessageType messageType = NotificationMessageType.Raw)
    {
        OnNotificationMessageRecived?.Invoke(null, new NotificationMessageEventArgs(messageType, message));
    }

    internal static void SendMessageNewLine(string message, NotificationMessageType messageType = NotificationMessageType.Raw)
    {
        OnNotificationMessageRecived?.Invoke(null, new NotificationMessageEventArgs(messageType, message + Environment.NewLine));
    }
}