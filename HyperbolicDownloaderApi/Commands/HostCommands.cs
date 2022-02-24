using HyperbolicDownloaderApi.Managment;
using HyperbolicDownloaderApi.Networking;

using System.Net;
using System.Net.Sockets;

namespace HyperbolicDownloaderApi.Commands;

public class HostCommands
{
    private readonly HostsManager hostsManager;

    public HostCommands(HostsManager hostsManager)
    {
        this.hostsManager = hostsManager;
    }

    public void Discover(string _)
    {
        ApiManager.SendMessageNewLine("Running local discovery routine...", NotificationMessageType.Raw);
        BroadcastClient.Send(ApiConfiguration.BroadcastPort, ApiConfiguration.PrivatePort.ToString());
        Thread.Sleep(3000);
    }

    public void CheckActiveHosts(string _)
    {
        int activeHostsCount = hostsManager.CheckHostsActivity();
        if (activeHostsCount == 0)
        {
            ApiManager.SendMessageNewLine("No active hosts found!", NotificationMessageType.Error);
            ApiManager.SendMessageNewLine("Use 'add host xxx.xxx.xxx.xxx:yyyy' to add a new host.", NotificationMessageType.Error);
        }

        ApiManager.SendMessageNewLine(string.Empty, NotificationMessageType.Raw);
        ApiManager.SendMessageNewLine($"{hostsManager.Count} known host(s).", NotificationMessageType.Raw);
        ApiManager.SendMessageNewLine($"{activeHostsCount} active host(s).", NotificationMessageType.Raw);
    }

    public void ListHosts(string _)
    {
        int index = 0;
        List<NetworkSocket> hosts = hostsManager.ToList();

        if (hosts.Count == 0)
        {
            ApiManager.SendMessageNewLine("No known hosts", NotificationMessageType.Warning);
            return;
        }

        foreach (NetworkSocket host in hosts)
        {
            index++;
            ApiManager.SendMessageNewLine($"{index}) {host.IPAddress}:{host.Port}", NotificationMessageType.Raw);
            ApiManager.SendMessageNewLine($"Last active: {host.LastActive}", NotificationMessageType.Raw);
            ApiManager.SendMessageNewLine(string.Empty, NotificationMessageType.Raw);
        }
        Console.CursorTop--;
    }

    public void RemoveHost(string args)
    {
        string[] parts = args.Split(":");

        if (parts.Length != 2)
        {
            ApiManager.SendMessageNewLine("Invalid format! Use this format: (xxx.xxx.xxx.xxx:yyyy)", NotificationMessageType.Error);
            return;
        }

        string ipAddressInput = parts[0];
        string portInput = parts[1];

        _ = int.TryParse(portInput, out int port);
        if (port < 1000 || port >= 6000)
        {
            ApiManager.SendMessageNewLine("Invalid port number!", NotificationMessageType.Error);
            return;
        }

        if (!IPAddress.TryParse(ipAddressInput, out IPAddress? ipAddress))
        {
            ApiManager.SendMessageNewLine("Invalid IP address!", NotificationMessageType.Error);
            return;
        }

        NetworkSocket hostToRemove = new NetworkSocket(ipAddress.ToString(), port, DateTime.MinValue);

        if (!hostsManager.Contains(hostToRemove))
        {
            ApiManager.SendMessageNewLine("Host not in list", NotificationMessageType.Error);
            return;
        }

        hostsManager.Remove(new NetworkSocket(ipAddress.ToString(), port, DateTime.MinValue), true);
        ApiManager.SendMessageNewLine($"Successfully Removed host!", NotificationMessageType.Success);
    }

    public void AddHost(string args)
    {
        string[] parts = args.Split(":");

        if (parts.Length != 2)
        {
            ApiManager.SendMessageNewLine("Invalid format! Use this format: (xxx.xxx.xxx.xxx:yyyy)", NotificationMessageType.Error);
            return;
        }

        string ipAddressInput = parts[0];
        string portInput = parts[1];

        _ = int.TryParse(portInput, out int port);
        if (port < 1000 || port >= 6000)
        {
            ApiManager.SendMessageNewLine("Invalid port number!", NotificationMessageType.Error);
        }
        else if (IPAddress.TryParse(ipAddressInput, out IPAddress? ipAddress))
        {
            try
            {
                ApiManager.SendMessageNewLine("Waiting for response...", NotificationMessageType.Raw);
                NetworkSocket? localSocket = ApiManager.GetLocalSocket() ?? new NetworkSocket("0.0.0.0", 0, DateTime.MinValue);
                List<NetworkSocket>? recivedHosts = NetworkClient.Send<List<NetworkSocket>>(ipAddress, port, "GetHostsList", localSocket);

                if (recivedHosts is not null)
                {
                    ApiManager.SendMessageNewLine($"Success! Added {recivedHosts.Count} new host(s).", NotificationMessageType.Success);
                    hostsManager.AddRange(recivedHosts);
                }
                else
                {
                    ApiManager.SendMessageNewLine($"Invalid response!", NotificationMessageType.Error);
                }
            }
            catch (SocketException ex)
            {
                ApiManager.SendMessageNewLine($"Invalid host! Error message: {ex.Message}", NotificationMessageType.Error);
            }
            catch (IOException ex)
            {
                ApiManager.SendMessageNewLine($"Invalid host! Error message: {ex.Message}", NotificationMessageType.Error);
            }
        }
        else
        {
            ApiManager.SendMessageNewLine("Invalid IP address!", NotificationMessageType.Error);
        }
    }
}