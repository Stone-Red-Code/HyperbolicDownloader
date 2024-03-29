﻿using HyperbolicDownloaderApi.FileProcessing;
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

    public static NetworkSocket? GetLocalSocket()
    {
        int port = ApiConfiguration.PublicPort;
        string? ipAddress = null;

        if (device is not null)
        {
            ipAddress = device.GetExternalIPAsync().GetAwaiter().GetResult()?.ToString();
        }

        if (ipAddress is null or "0.0.0.0")
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
            SendNotificationMessageNewLine($"The public IP address is: {ip} ", NotificationMessageType.Success);

            portMapping = new Mapping(Protocol.Tcp, ApiConfiguration.PrivatePort, ApiConfiguration.PublicPort, "HyperbolicDowloader");

            await device.CreatePortMapAsync(portMapping);

            SendNotificationMessageNewLine($"The public port is: {ApiConfiguration.PublicPort}", NotificationMessageType.Success);
            return true;
        }
        catch (NatDeviceNotFoundException)
        {
            SendNotificationMessageNewLine($"Could not find a UPnP or NAT-PMP device!", NotificationMessageType.Error);
            return false;
        }
        catch (MappingException ex)
        {
            SendNotificationMessageNewLine($"An error occurred while mapping the private port ({ApiConfiguration.PrivatePort}) to the public port ({ApiConfiguration.PublicPort})! Error message: {ex.Message}", NotificationMessageType.Error);
            return false;
        }
    }

    public static void ClosePorts()
    {
        SendNotificationMessageNewLine("Closing ports...", NotificationMessageType.Info);
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
                SendNotificationMessageNewLine(ex.ToString(), NotificationMessageType.Error);
            }
        }

        SendNotificationMessageNewLine("Ports closed!", NotificationMessageType.Warning);
        Environment.Exit(0);
    }

    public void StartBroadcastListener()
    {
        broadcastClient.StartListening(ApiConfiguration.BroadcastPort);
        broadcastClient.OnBroadcastRecived += BroadcastClient_OnBroadcastRecived;
    }

    public bool StartTcpListener()
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
            SendNotificationMessageNewLine($"An error occurred while starting the TCP listener! Error message: {ex.Message}", NotificationMessageType.Error); // net stop hens && net start hns
            return false;
        }

        return true;
    }

    private async void BroadcastClient_OnBroadcastRecived(object? sender, BroadcastRecivedEventArgs recivedEventArgs)
    {
        IPAddress remoteIpAddress = recivedEventArgs.IPEndPoint.Address;

        Debug.WriteLine($"Received broadcast \"{recivedEventArgs.Message}\" from {remoteIpAddress}");
        List<NetworkSocket> hostsToSend = HostsManager.ToList();

        NetworkSocket? localSocket = GetLocalSocket();

        if (localSocket is null || remoteIpAddress.Equals(PublicIpAddress) || remoteIpAddress.Equals(NetworkUtilities.GetIP4Adress()))
        {
            Debug.WriteLine("Invalid broadcast!");
            return;
        }

        _ = hostsToSend.RemoveAll(x => x.IPAddress == remoteIpAddress.ToString());
        hostsToSend.Add(localSocket);

        bool success = int.TryParse(recivedEventArgs.Message, out int remotePort);

        if (success)
        {
            HostsManager.Add(new NetworkSocket(remoteIpAddress.ToString(), remotePort, DateTime.Now));
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
        SendNotificationMessageNewLine($"Received answer from {recivedEventArgs.IpAddress}. Returned {recivedEventArgs.Data.Count} host(s).", NotificationMessageType.Info);
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

        _ = hostsToSend.RemoveAll(x => x.IPAddress == recivedEventArgs.IpAddress.ToString());
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
        SendNotificationMessageNewLine($"Received \"{recivedEventArgs.Data}\" from {recivedEventArgs.IpAddress}.", NotificationMessageType.Info);
    }

    internal static void SendNotificationMessage(string message, NotificationMessageType messageType = NotificationMessageType.Info)
    {
        OnNotificationMessageRecived?.Invoke(null, new NotificationMessageEventArgs(messageType, message));
    }

    internal static void SendNotificationMessageNewLine(string message, NotificationMessageType messageType = NotificationMessageType.Info)
    {
        OnNotificationMessageRecived?.Invoke(null, new NotificationMessageEventArgs(messageType, message + Environment.NewLine));
    }
}