using HyperbolicDownloaderApi.Managment;

using System.Net.Sockets;
using System.Text.Json;

namespace HyperbolicDownloaderApi.Networking;

public class HostsManager
{
    private List<NetworkSocket> hosts = new List<NetworkSocket>();
    public int Count => hosts.Count;

    public int AddRange(IEnumerable<NetworkSocket> hosts)
    {
        int newHosts = 0;

        foreach (NetworkSocket host in hosts)
        {
            if (Add(host))
            {
                newHosts++;
            }
        }

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
            _ = hosts.RemoveAll(x => x.IPAddress == host.IPAddress && x.Port == host.Port);
        }
        SaveHosts();
    }

    public bool Contains(NetworkSocket host)
    {
        return hosts.Any(x => x.IPAddress == host.IPAddress && x.Port == host.Port);
    }

    public int CheckHostsActivity()
    {
        List<NetworkSocket> hostsToRemove = new List<NetworkSocket>();
        int activeHostsCount = 0;
        foreach (NetworkSocket host in hosts)
        {
            ApiManager.SendNotificationMessage($"{host.IPAddress}:{host.Port} > ???", NotificationMessageType.Warning);
            using TcpClient tcpClient = new TcpClient();

            try
            {
                _ = tcpClient.ConnectAsync(host.IPAddress, host.Port).Wait(500);
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

    public List<NetworkSocket> ToList()
    {
        return hosts.ToList();
    }

    public void SaveHosts()
    {
        hosts = hosts.OrderByDescending(h => h.LastActive).ToList();
        File.WriteAllText(ApiConfiguration.HostsFilePath, JsonSerializer.Serialize(hosts));
    }
}