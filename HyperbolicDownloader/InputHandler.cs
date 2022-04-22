using Commander_Net;

using HyperbolicDownloaderApi.Commands;
using HyperbolicDownloaderApi.FileProcessing;
using HyperbolicDownloaderApi.Networking;

using Stone_Red_Utilities.ConsoleExtentions;

namespace HyperbolicDownloader;

internal class InputHandler
{
    private readonly Commander commander = new Commander();
    private bool exit = false;

    public InputHandler(HostsManager hostsManager, FilesManager filesManager)
    {
        HostCommands hostCommands = new HostCommands(hostsManager);
        FileCommands fileCommands = new FileCommands(hostsManager, filesManager);
        DownloadCommands downloadCommands = new DownloadCommands(hostsManager, filesManager);
        ClientCommands clientCommands = new ClientCommands();

        commander.Register((_) => Console.Clear(), "clear", "cls");
        commander.Register((_) => exit = true, "exit", "quit");
        commander.Register(clientCommands.ShowInfo, "info", "inf");
        commander.Register(hostCommands.Discover, "discover", "disc");

        Command getCommand = commander.Register(downloadCommands.GetFile, "get");
        getCommand.Register(downloadCommands.GetFileFrom, "from");

        Command generateCommad = commander.Register(fileCommands.GenerateFileFull, "generate", "gen");
        generateCommad.Register(fileCommands.GenerateFileSingle, "noscan");

        Command addCommand = commander.Register(fileCommands.AddFile, "add");
        addCommand.Register(hostCommands.AddHost, "host");
        addCommand.Register(fileCommands.AddFile, "file");

        Command removeCommand = commander.Register(fileCommands.RemoveFile, "remove", "rm");
        removeCommand.Register(hostCommands.RemoveHost, "host");
        removeCommand.Register(fileCommands.RemoveFile, "file");

        Command listCommand = commander.Register(fileCommands.ListFiles, "list", "ls");
        listCommand.Register(fileCommands.ListFiles, "files");
        listCommand.Register(hostCommands.ListHosts, "hosts");

        commander.Register(hostCommands.CheckActiveHosts, "status", "check");
    }

    public void ReadInput()
    {
        while (!exit)
        {
            Console.WriteLine();
            Console.Write("> ");
            Console.CursorVisible = true;

            string input = Console.ReadLine()?.Trim() ?? string.Empty;

            Console.CursorVisible = false;
            try
            {
                if (!commander.Execute(input))
                {
                    ConsoleExt.WriteLine("Unknown command!", ConsoleColor.Red);
                }
            }
            catch (Exception ex)
            {
                ConsoleExt.WriteLine($"An unexpected error occurred! {ex}", ConsoleColor.Red);
            }
        }
    }
}