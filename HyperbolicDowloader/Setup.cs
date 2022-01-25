using HyperbolicDowloader.Networking;

using Stone_Red_Utilities.ConsoleExtentions;

using System.Net;
using System.Net.Sockets;

namespace HyperbolicDowloader;

internal static class Setup
{
    public static async Task<List<NetworkSocket>> ConfigureHost()
    {
        ConsoleExt.WriteLine("No active hosts found!", ConsoleColor.Red);
        do
        {
            Console.WriteLine();
            Console.Write("Please enter an IP address manually: ");
            string? ipAddressInput = Console.ReadLine();

            Console.Write("Please enter an port number manually: ");
            string? portInput = Console.ReadLine();

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
                    NetworkSocket? localSocket = Program.GetLocalSocket() ?? new NetworkSocket("0.0.0.0", 0);
                    List<NetworkSocket>? recivedHosts = await NetworkClient.SendAsync<List<NetworkSocket>>(ipAddress, port, "GetHostsList", localSocket);

                    if (recivedHosts is not null)
                    {
                        ConsoleExt.WriteLine($"Success! Added {recivedHosts.Count} new host(s).", ConsoleColor.Green);
                        return recivedHosts;
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
            }
            else
            {
                ConsoleExt.WriteLine("Invalid IP address!", ConsoleColor.Red);
            }
        } while (true);
    }
}