using HyperbolicDownloaderApi.FileProcessing;
using HyperbolicDownloaderApi.Managment;
using HyperbolicDownloaderApi.Networking;

using Stone_Red_Utilities.ConsoleExtentions;

using System.Reflection;
using System.Text.Json;

namespace HyperbolicDownloader;

internal static class Program
{
    private static readonly ApiManager apiManager = new ApiManager();

    private static async Task Main(string[] args)
    {
        Console.CancelKeyPress += (_, _) => Close();
        Console.CursorVisible = false;

        ApiManager.OnNotificationMessageRecived += ApiManager_OnNotificationMessageRecived;

        if (!Directory.Exists(ApiConfiguration.BasePath))
        {
            _ = Directory.CreateDirectory(ApiConfiguration.BasePath);
        }

        if (File.Exists(ApiConfiguration.HostsFilePath))
        {
            string hostsJson = await File.ReadAllTextAsync(ApiConfiguration.HostsFilePath);
            _ = apiManager.HostsManager.AddRange(JsonSerializer.Deserialize<List<NetworkSocket>>(hostsJson) ?? []);
        }

        if (File.Exists(ApiConfiguration.FilesInfoPath))
        {
            string filesJson = await File.ReadAllTextAsync(ApiConfiguration.FilesInfoPath);
            apiManager.FilesManager.AddRange(JsonSerializer.Deserialize<List<PrivateHyperFileInfo>>(filesJson) ?? []);
        }

        if (File.Exists(ApiConfiguration.DirectoriesInfoPath))
        {
            string directoriesJson = await File.ReadAllTextAsync(ApiConfiguration.DirectoriesInfoPath);
            apiManager.DirectoryWatcher.AddRange(JsonSerializer.Deserialize<List<string>>(directoriesJson) ?? []);
        }

        Console.WriteLine($"HyperbolicDownloader - {Assembly.GetExecutingAssembly().GetName().Version}");
        ConsoleExt.WriteLine("https://github.com/Stone-Red-Code/HyperbolicDownloader", ConsoleColor.Blue);
        Console.WriteLine();

        InputHandler inputHandler = new InputHandler(apiManager.HostsManager, apiManager.FilesManager, apiManager.DirectoryWatcher);

        if (args.Length > 0 && File.Exists(args[0]))
        {
            HyperbolicDownloaderApi.Commands.DownloadCommands downloadCommands = new HyperbolicDownloaderApi.Commands.DownloadCommands(apiManager.HostsManager, apiManager.FilesManager);
            downloadCommands.GetFileFrom(args[0]);
            _ = Console.ReadLine();
            return;
        }
        else if (args.Length > 0)
        {
            ConsoleExt.WriteLine("Warning! You are using command line arguments, which can lead to unexpected behavior with some commands.", ConsoleColor.Yellow);
            Console.WriteLine();
            inputHandler.ExecuteInput(string.Join(' ', args));
            return;
        }

        await Initialize();

        inputHandler.ReadInput();
        Close();
    }

    private static async Task Initialize()
    {
        apiManager.DirectoryWatcher.Start();

        Console.WriteLine("Searching for a UPnP/NAT-PMP device...");
        _ = await ApiManager.OpenPorts();

        ConsoleExt.WriteLine($"The private IP address is: {NetworkUtilities.GetIP4Adress()} ", ConsoleColor.Green);
        ConsoleExt.WriteLine($"The private port is: {ApiConfiguration.PrivatePort}", ConsoleColor.Green);

        Console.WriteLine("Starting TCP listener...");
        if (!apiManager.StartTcpListener())
        {
            _ = Console.ReadLine();
            Environment.Exit(-1);
        }

        Console.WriteLine("Starting UDP listener...");
        if (!apiManager.StartBroadcastListener())
        {
            _ = Console.ReadLine();
            Environment.Exit(-2);
        }

        int activeHostsCount = 0;
        if (apiManager.HostsManager.Count > 0)
        {
            Console.WriteLine("Checking if hosts are active...");
            activeHostsCount = apiManager.HostsManager.CheckHostsActivity();
        }

        if (activeHostsCount == 0)
        {
            ConsoleExt.WriteLine("No active hosts found!", ConsoleColor.Red);
            ConsoleExt.WriteLine("Use 'add host <ip address>:<port>' to add a new host or use 'discover' to find hosts in the local network.", ConsoleColor.Red);
        }

        Console.WriteLine($"{apiManager.HostsManager.Count} known host(s).");
        Console.WriteLine($"{activeHostsCount} active host(s).");

        ConsoleExt.WriteLine("Ready", ConsoleColor.Green);
    }

    private static void ApiManager_OnNotificationMessageRecived(object? sender, NotificationMessageEventArgs e)
    {
        switch (e.NotificationMessageType)
        {
            case NotificationMessageType.Info: Console.Write(e.Message); break;
            case NotificationMessageType.Success: ConsoleExt.Write(e.Message, ConsoleColor.Green); break;
            case NotificationMessageType.Warning: ConsoleExt.Write(e.Message, ConsoleColor.DarkYellow); break;
            case NotificationMessageType.Error: ConsoleExt.Write(e.Message, ConsoleColor.Red); break;
        }
    }

    private static void Close()
    {
        ApiManager.ClosePorts();
        apiManager.HostsManager.SaveHosts();
        Environment.Exit(0);
    }
}