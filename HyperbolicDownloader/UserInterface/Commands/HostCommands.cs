using HyperbolicDownloader.Networking;

using Stone_Red_Utilities.ConsoleExtentions;

using System.Net;
using System.Net.Sockets;

namespace HyperbolicDownloader.UserInterface.Commands;

internal class HostCommands
{
    private readonly HostsManager hostsManager;

    public HostCommands(HostsManager hostsManager)
    {
        this.hostsManager = hostsManager;
    }

    public void Discover(string _)
    {
        Console.WriteLine("Running local discovery routine...");
        BroadcastClient.Send(Program.BroadcastPort, Program.PrivatePort.ToString());
        Thread.Sleep(3000);
    }

    public void CheckActiveHosts(string _)
    {
        int activeHostsCount = hostsManager.CheckHostsActivity();
        if (activeHostsCount == 0)
        {
            ConsoleExt.WriteLine("No active hosts found!", ConsoleColor.Red);
            ConsoleExt.WriteLine("Use 'add host xxx.xxx.xxx.xxx:yyyy' to add a new host.", ConsoleColor.Red);
        }

        Console.WriteLine($"{hostsManager.Count} known host(s).");
        Console.WriteLine($"{activeHostsCount} active host(s).");
    }

    public void ListHosts(string _)
    {
        int index = 0;
        foreach (NetworkSocket host in hostsManager.ToList())
        {
            index++;
            Console.WriteLine($"{index}) {host.IPAddress}:{host.Port}");
            Console.WriteLine($"Last active: {host.LastActive}");
            Console.WriteLine();
        }
        Console.CursorTop--;
    }

    public void AddHost(string args)
    {
        string[] parts = args.Split(":");

        if (parts.Length != 2)
        {
            ConsoleExt.WriteLine("Invalid format! Use this format: (xxx.xxx.xxx.xxx:yyyy)", ConsoleColor.Red);
            return;
        }

        string ipAddressInput = parts[0];
        string portInput = parts[1];

        _ = int.TryParse(portInput, out int port);
        if (port < 1000 || port >= 6000)
        {
            ConsoleExt.WriteLine("Invalid port number!", ConsoleColor.Red);
        }
        else if (IPAddress.TryParse(ipAddressInput, out IPAddress? ipAddress))
        {
            try
            {
                Console.WriteLine("Waiting for response...");
                NetworkSocket? localSocket = Program.GetLocalSocket() ?? new NetworkSocket("0.0.0.0", 0, DateTime.MinValue);
                List<NetworkSocket>? recivedHosts = NetworkClient.Send<List<NetworkSocket>>(ipAddress, port, "GetHostsList", localSocket);

                if (recivedHosts is not null)
                {
                    ConsoleExt.WriteLine($"Success! Added {recivedHosts.Count} new host(s).", ConsoleColor.Green);
                    hostsManager.AddRange(recivedHosts);
                }
                else
                {
                    ConsoleExt.WriteLine($"Invalid response!", ConsoleColor.Red);
                }
            }
            catch (SocketException ex)
            {
                ConsoleExt.WriteLine($"Invalid host! Error message: {ex.Message}", ConsoleColor.Red);
            }
            catch (IOException ex)
            {
                ConsoleExt.WriteLine($"Invalid host! Error message: {ex.Message}", ConsoleColor.Red);
            }
        }
        else
        {
            ConsoleExt.WriteLine("Invalid IP address!", ConsoleColor.Red);
        }
    }
}