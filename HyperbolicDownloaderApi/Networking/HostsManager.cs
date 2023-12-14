using HyperbolicDownloaderApi.Managment;

using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace HyperbolicDownloaderApi.Networking;

public class HostsManager
{
    private List<NetworkSocket> hosts = [];
    public int Count => hosts.Count;

    public int AddRange(IEnumerable<NetworkSocket> hosts)
    {
        int newHosts = hosts.Count(Add);
        SaveHosts();

        return newHosts;
    }

    public bool Add(NetworkSocket host)
    {
        if (!Contains(host) && !host.Equals(ApiManager.GetLocalSocket()))
        {
            hosts.Add(host);
            return true;
        }

        SaveHosts();

        return false;
    }

    public void Remove(NetworkSocket host, bool forceRemove = false)
    {
        if (DateTime.Now - host.LastActive >= new TimeSpan(24, 0, 0) || forceRemove)
        {
            _ = hosts.RemoveAll(x => x.Equals(host));
        }
        SaveHosts();
    }

    public bool Contains(NetworkSocket host)
    {
        return hosts.Exists(x => x.Equals(host));
    }

    public int CheckHostsActivity()
    {
        List<NetworkSocket> hostsToRemove = [];
        int activeHostsCount = 0;
        foreach (NetworkSocket host in hosts)
        {
            ApiManager.SendNotificationMessage($"{host.IPAddress}:{host.Port} > ???", NotificationMessageType.Warning);
            using TcpClient tcpClient = new TcpClient();

            try
            {
                _ = tcpClient.ConnectAsync(host.IPAddress, host.Port).Wait(1000);
                Console.CursorLeft = 0;
                if (tcpClient.Connected)
                {
                    ApiManager.SendNotificationMessageNewLine($"{host.IPAddress}:{host.Port} > Active", NotificationMessageType.Success);
                    host.LastActive = DateTime.Now;
                    activeHostsCount++;
                }
                else
                {
                    if (DateTime.Now - host.LastActive >= new TimeSpan(24, 0, 0))
                    {
                        hostsToRemove.Add(host);
                    }
                    ApiManager.SendNotificationMessageNewLine($"{host.IPAddress}:{host.Port} > Inactive", NotificationMessageType.Error);
                }
            }
            catch
            {
                if (DateTime.Now - host.LastActive >= new TimeSpan(24, 0, 0))
                {
                    hostsToRemove.Add(host);
                }
                Console.CursorLeft = 0;
                ApiManager.SendNotificationMessageNewLine($"{host.IPAddress}:{host.Port} > Inactive", NotificationMessageType.Error);
            }
        }

        foreach (NetworkSocket host in hostsToRemove)
        {
            _ = hosts.Remove(host);
        }

        SaveHosts();
        return activeHostsCount;
    }

    public int Sync()
    {
        int newHostsCount = 0;

        foreach (NetworkSocket host in hosts.ToArray())
        {
            ApiManager.SendNotificationMessage($"{host.IPAddress}:{host.Port} > ???", NotificationMessageType.Warning);

            try
            {
                NetworkSocket? localSocket = ApiManager.GetLocalSocket() ?? new NetworkSocket("0.0.0.0", 0, DateTime.MinValue);

                Task<List<NetworkSocket>?> sendTask = NetworkClient.SendAsync<List<NetworkSocket>>(IPAddress.Parse(host.IPAddress), host.Port, "GetHostsList", localSocket);

                _ = sendTask.Wait(1000);

                Console.CursorLeft = 0;
                if (sendTask.IsCompletedSuccessfully)
                {
                    int newHosts = AddRange(sendTask.Result ?? []);
                    newHostsCount += newHosts;

                    if (newHosts > 0)
                    {
                        ApiManager.SendNotificationMessageNewLine($"{host.IPAddress}:{host.Port} > Found {newHosts} new hosts", NotificationMessageType.Success);
                    }
                    else
                    {
                        ApiManager.SendNotificationMessageNewLine($"{host.IPAddress}:{host.Port} > No new hosts found", NotificationMessageType.Warning);
                    }

                    host.LastActive = DateTime.Now;
                }
                else
                {
                    ApiManager.SendNotificationMessageNewLine($"{host.IPAddress}:{host.Port} > Inactive", NotificationMessageType.Error);
                }
            }
            catch
            {
                Console.CursorLeft = 0;
                ApiManager.SendNotificationMessageNewLine($"{host.IPAddress}:{host.Port} > Inactive", NotificationMessageType.Error);
            }
        }

        SaveHosts();
        return newHostsCount;
    }

    public List<NetworkSocket> ToList()
    {
        return [.. hosts];
    }

    public void SaveHosts()
    {
        hosts = [.. hosts
                .OrderByDescending(s => s.DownloadSpeed)
                .ThenByDescending(s => s.LastActive)];
        File.WriteAllText(ApiConfiguration.HostsFilePath, JsonSerializer.Serialize(hosts));
    }
}