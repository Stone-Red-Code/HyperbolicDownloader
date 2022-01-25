using HyperbolicDownloader.Networking;

using Stone_Red_Utilities.ConsoleExtentions;

using System.Net.Sockets;
using System.Text.Json;

namespace HyperbolicDownloader;

internal class HostsManager
{
    private readonly List<NetworkSocket> hosts = new List<NetworkSocket>();
    public int Count => hosts.Count;

    public void AddRange(IEnumerable<NetworkSocket> hosts)
    {
        foreach (NetworkSocket host in hosts)
        {
            if (!this.hosts.Any(x => x.IPAddress == host.IPAddress && x.Port == host.Port))
            {
                this.hosts.Add(host);
            }
        }

        SaveHosts();
    }

    public void Add(NetworkSocket host)
    {
        if (!hosts.Any(x => x.IPAddress == host.IPAddress && x.Port == host.Port))
        {
            hosts.Add(host);
        }
        SaveHosts();
    }

    public void Remove(NetworkSocket host)
    {
        hosts.RemoveAll(x => x.IPAddress == host.IPAddress && x.Port == host.Port);
        SaveHosts();
    }

    public void RemoveInactiveHosts()
    {
        List<NetworkSocket> hostsToRemove = new List<NetworkSocket>();
        foreach (NetworkSocket host in hosts)
        {
            ConsoleExt.Write($"{host.IPAddress}:{host.Port} > ???", ConsoleColor.DarkYellow);
            using TcpClient tcpClient = new TcpClient();

            try
            {
                tcpClient.ConnectAsync(host.IPAddress, host.Port).Wait(1000);
                Console.CursorLeft = 0;
                if (tcpClient.Connected)
                {
                    ConsoleExt.WriteLine($"{host.IPAddress}:{host.Port} > Active", ConsoleColor.Green);
                }
                else
                {
                    hostsToRemove.Add(host);
                    ConsoleExt.WriteLine($"{host.IPAddress}:{host.Port} > Inactive", ConsoleColor.Red);
                }
            }
            catch
            {
                hostsToRemove.Add(host);
                Console.CursorLeft = 0;
                ConsoleExt.WriteLine($"{host.IPAddress}:{host.Port} > Inactive", ConsoleColor.Red);
            }
        }

        foreach (NetworkSocket host in hostsToRemove)
        {
            hosts.Remove(host);
        }

        SaveHosts();
    }

    public List<NetworkSocket> ToList()
    {
        return hosts.ToList();
    }

    private void SaveHosts()
    {
        File.WriteAllText(Program.HostsFilePath, JsonSerializer.Serialize(hosts));
    }
}