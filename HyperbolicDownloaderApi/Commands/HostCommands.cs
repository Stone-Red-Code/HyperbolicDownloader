using HyperbolicDownloaderApi.Managment;
using HyperbolicDownloaderApi.Networking;
using HyperbolicDownloaderApi.Utilities;

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace HyperbolicDownloaderApi.Commands;

public class HostCommands(HostsManager hostsManager)
{
    public void Discover(string _)
    {
        ApiManager.SendNotificationMessageNewLine("Running local discovery routine...", NotificationMessageType.Info);

        int hostsCountBefore = hostsManager.Count;

        BroadcastClient.Send(ApiConfiguration.BroadcastPort, ApiConfiguration.PrivatePort.ToString());
        Thread.Sleep(3000);

        ApiManager.SendNotificationMessageNewLine($"Found {hostsManager.Count - hostsCountBefore} host(s)", NotificationMessageType.Info);
    }

    public void CheckActiveHosts(string _)
    {
        int activeHostsCount = hostsManager.CheckHostsActivity();
        if (activeHostsCount == 0)
        {
            ApiManager.SendNotificationMessageNewLine("No active hosts found!", NotificationMessageType.Error);
            ApiManager.SendNotificationMessageNewLine("Use 'add host <ip address>:<port>' to add a new host.", NotificationMessageType.Error);
        }

        ApiManager.SendNotificationMessageNewLine(string.Empty, NotificationMessageType.Info);
        ApiManager.SendNotificationMessageNewLine($"{hostsManager.Count} known host(s).", NotificationMessageType.Info);
        ApiManager.SendNotificationMessageNewLine($"{activeHostsCount} active host(s).", NotificationMessageType.Info);
    }

    public void ListHosts(string _)
    {
        int index = 0;
        List<NetworkSocket> hosts = hostsManager.ToList();

        if (hosts.Count == 0)
        {
            ApiManager.SendNotificationMessageNewLine("No known hosts", NotificationMessageType.Warning);
            return;
        }

        foreach (NetworkSocket host in hosts)
        {
            index++;
            ApiManager.SendNotificationMessageNewLine($"{index}) {host.IPAddress}:{host.Port}", NotificationMessageType.Info);
            ApiManager.SendNotificationMessageNewLine($"Last active: {host.LastActive}", NotificationMessageType.Info);
            ApiManager.SendNotificationMessageNewLine($"Download speed: {(host.DownloadSpeed <= 0 ? "N/A" : UnitFormatter.TransferRate(host.DownloadSpeed))}", NotificationMessageType.Info);
            ApiManager.SendNotificationMessageNewLine(string.Empty, NotificationMessageType.Info);
        }
        Console.CursorTop--;
    }

    public void RemoveHost(string args)
    {
        string[] parts = args.Split(":");

        if (int.TryParse(args, out int index))
        {
            if (hostsManager.Count < index || index < 1)
            {
                ApiManager.SendNotificationMessageNewLine("Invalid index!", NotificationMessageType.Error);
                return;
            }

            hostsManager.Remove(hostsManager.ToList()[index - 1], true);
            ApiManager.SendNotificationMessageNewLine($"Successfully Removed host!", NotificationMessageType.Success);

            return;
        }

        if (parts.Length != 2)
        {
            ApiManager.SendNotificationMessageNewLine("Invalid format! Use this format: <ip address>:<port>", NotificationMessageType.Error);
            return;
        }

        string ipAddressInput = parts[0];
        string portInput = parts[1];

        _ = int.TryParse(portInput, out int port);
        if (port is < 1000 or >= 6000)
        {
            ApiManager.SendNotificationMessageNewLine("Invalid port number!", NotificationMessageType.Error);
            return;
        }

        if (!IPAddress.TryParse(ipAddressInput, out IPAddress? ipAddress))
        {
            ApiManager.SendNotificationMessageNewLine("Invalid IP address!", NotificationMessageType.Error);
            return;
        }

        NetworkSocket hostToRemove = new NetworkSocket(ipAddress.ToString(), port, DateTime.MinValue);

        if (!hostsManager.Contains(hostToRemove))
        {
            ApiManager.SendNotificationMessageNewLine("Host not in list", NotificationMessageType.Error);
            return;
        }

        hostsManager.Remove(new NetworkSocket(ipAddress.ToString(), port, DateTime.MinValue), true);
        ApiManager.SendNotificationMessageNewLine($"Successfully Removed host!", NotificationMessageType.Success);
    }

    public void AddHost(string args)
    {
        string[] parts = args.Split(":");

        if (parts.Length != 2)
        {
            ApiManager.SendNotificationMessageNewLine("Invalid format! Use this format: <ip address>:<port>", NotificationMessageType.Error);
            return;
        }

        string ipAddressInput = parts[0];
        string portInput = parts[1];
        IPAddress? ipAddress;

        _ = int.TryParse(portInput, out int port);

        if (port is < 1000 or >= 6000)
        {
            ApiManager.SendNotificationMessageNewLine("Invalid port number!", NotificationMessageType.Error);
            return;
        }
        else if (!IPAddress.TryParse(ipAddressInput, out ipAddress))
        {
            try
            {
                ipAddress = Dns.GetHostAddresses(ipAddressInput).FirstOrDefault();
            }
            catch (SocketException ex)
            {
                Debug.WriteLine(ex);
            }
        }

        if (ipAddress is null)
        {
            ApiManager.SendNotificationMessageNewLine("Invalid IP address!", NotificationMessageType.Error);
            return;
        }

        try
        {
            ApiManager.SendNotificationMessageNewLine("Waiting for response...", NotificationMessageType.Info);
            NetworkSocket? localSocket = ApiManager.GetLocalSocket() ?? new NetworkSocket("0.0.0.0", 0, DateTime.MinValue);
            Task<List<NetworkSocket>?> sendTask = NetworkClient.SendAsync<List<NetworkSocket>>(ipAddress, port, "GetHostsList", localSocket);

            _ = sendTask.Wait(5000);

            if (!sendTask.IsCompletedSuccessfully)
            {
                ApiManager.SendNotificationMessageNewLine($"No response from {ipAddress}:{port}", NotificationMessageType.Error);
                return;
            }

            List<NetworkSocket>? recivedHosts = sendTask.Result;

            if (recivedHosts is not null)
            {
                int newHosts = hostsManager.AddRange(recivedHosts);

                if (hostsManager.Add(new NetworkSocket(ipAddress.ToString(), port, DateTime.Now)))
                {
                    newHosts++;
                }

                if (newHosts > 0)
                {
                    ApiManager.SendNotificationMessageNewLine($"Added {newHosts} new host(s).", NotificationMessageType.Success);
                }
                else
                {
                    ApiManager.SendNotificationMessageNewLine($"No new hosts added.", NotificationMessageType.Warning);
                }
            }
            else
            {
                ApiManager.SendNotificationMessageNewLine($"Invalid response!", NotificationMessageType.Error);
            }
        }
        catch (SocketException ex)
        {
            ApiManager.SendNotificationMessageNewLine($"Invalid host! Error message: {ex.Message}", NotificationMessageType.Error);
        }
        catch (IOException ex)
        {
            ApiManager.SendNotificationMessageNewLine($"Invalid host! Error message: {ex.Message}", NotificationMessageType.Error);
        }
    }

    public void Sync(string _)
    {
        int newHostsCount = hostsManager.Sync();

        ApiManager.SendNotificationMessageNewLine(string.Empty, NotificationMessageType.Info);

        if (newHostsCount > 0)
        {
            ApiManager.SendNotificationMessage($"Added {newHostsCount} new host(s).");
        }
        else
        {
            ApiManager.SendNotificationMessage($"No new hosts added.", NotificationMessageType.Warning);
        }
    }
}