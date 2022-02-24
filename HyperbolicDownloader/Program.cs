using HyperbolicDownloaderApi.FileProcessing;
using HyperbolicDownloaderApi.Managment;
using HyperbolicDownloaderApi.Networking;
using HyperbolicDownloaderApi.UserInterface;

using Stone_Red_Utilities.ConsoleExtentions;

using System.Text.Json;

namespace HyperbolicDownloaderApi;

internal static class Program
{
    private static readonly ApiManager apiManager = new ApiManager();

    private static async Task Main(string[] args)
    {
        Console.CancelKeyPress += Console_CancelKeyPress;
        Console.CursorVisible = false;

        ApiManager.OnNotificationMessageRecived += ApiManager_OnNotificationMessageRecived;

        if (File.Exists(ApiConfiguration.HostsFilePath))
        {
            string hostsJson = await File.ReadAllTextAsync(ApiConfiguration.HostsFilePath);
            apiManager.HostsManager.AddRange(JsonSerializer.Deserialize<List<NetworkSocket>>(hostsJson) ?? new());
        }

        if (File.Exists(ApiConfiguration.FilesInfoPath))
        {
            string filesJson = await File.ReadAllTextAsync(ApiConfiguration.FilesInfoPath);
            apiManager.FilesManager.AddRange(JsonSerializer.Deserialize<List<PrivateHyperFileInfo>>(filesJson) ?? new());
        }

        InputHandler inputHandler = new InputHandler(apiManager.HostsManager, apiManager.FilesManager);

        if (args.Length > 0 && File.Exists(args[0]))
        {
            Commands.DownloadCommands downloadCommands = new Commands.DownloadCommands(apiManager.HostsManager, apiManager.FilesManager);
            downloadCommands.GetFileFrom(args[0]);
            Console.WriteLine("Do you want to continue using this instance? [y/N]");
            if (char.ToLower(Console.ReadKey().KeyChar) != 'y')
            {
                return;
            }
            Console.WriteLine();
        }

        Console.WriteLine("Searching for a UPnP/NAT-PMP device...");
        _ = await ApiManager.OpenPorts();

        ConsoleExt.WriteLine($"The private IP address is: {NetworkUtilities.GetIP4Adress()} ", ConsoleColor.Green);
        ConsoleExt.WriteLine($"The private port is: {ApiConfiguration.PrivatePort}", ConsoleColor.Green);

        Console.WriteLine("Starting TCP listener...");
        apiManager.StartTcpListener();

        Console.WriteLine("Starting broadcast listener...");
        apiManager.StartBroadcastListener();

        int activeHostsCount = 0;
        if (apiManager.HostsManager.Count > 0)
        {
            Console.WriteLine("Checking if hosts are active...");
            activeHostsCount = apiManager.HostsManager.CheckHostsActivity();
        }

        if (activeHostsCount == 0)
        {
            ConsoleExt.WriteLine("No active hosts found!", ConsoleColor.Red);
            ConsoleExt.WriteLine("Use 'add host xxx.xxx.xxx.xxx:yyyy' to add a new host or use 'discover' to find hosts in the local network.", ConsoleColor.Red);
        }

        Console.WriteLine($"{apiManager.HostsManager.Count} known host(s).");
        Console.WriteLine($"{activeHostsCount} active host(s).");

        ConsoleExt.WriteLine("Ready", ConsoleColor.Green);

        inputHandler.ReadInput();
        ApiManager.ClosePorts();
    }

    private static void ApiManager_OnNotificationMessageRecived(object? sender, NotificationMessageEventArgs e)
    {
        switch (e.NotificationMessageType)
        {
            case NotificationMessageType.Raw: Console.Write(e.Message); break;
            case NotificationMessageType.Success: ConsoleExt.Write(e.Message, ConsoleColor.Green); break;
            case NotificationMessageType.Warning: ConsoleExt.Write(e.Message, ConsoleColor.DarkYellow); break;
            case NotificationMessageType.Error: ConsoleExt.Write(e.Message, ConsoleColor.Red); break;
        }
    }

    private static void Console_CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        ApiManager.ClosePorts();
        apiManager.HostsManager.SaveHosts();
    }
}