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

        commander.Register(input => commander.PrintHelp(input), "help");
        commander.Register(_ => Console.Clear(), (HelpText)"Clears the console.", "clear", "cls");
        commander.Register(_ => exit = true, (HelpText)"Exits the application.", "exit", "quit");
        commander.Register(clientCommands.ShowInfo, (HelpText)"Displays the private and public IP address.", "info", "inf");
        commander.Register(hostCommands.Discover, (HelpText)"Tries to find other active hosts on the local network.", "discover", "disc");

        Command getCommand = commander.Register(downloadCommands.GetFile, (HelpText)"Attempts to retrieve a file from another host using a hash.", "get");
        getCommand.Register(downloadCommands.GetFileFrom, (HelpText)"Attempts to retrieve a file from another host using a .hyper file.", "from");

        Command generateCommad = commander.Register(fileCommands.GenerateFileFull, (HelpText)"Generates a .hyper file from a file hash.", "generate", "gen");
        generateCommad.Register(fileCommands.GenerateFileSingle, (HelpText)"Generates a .hyper file from a file hash without checking the known hosts. This adds only the local host to the file.", "noscan");

        Command addCommand = commander.Register(fileCommands.AddFile, (HelpText)"Adds a file to the tracking list.", "add");
        addCommand.Register(fileCommands.AddFile, (HelpText)"Adds a file to the tracking list.", "file");
        addCommand.Register(hostCommands.AddHost, (HelpText)"Adds a host to the list of known hosts.", "host");

        Command removeCommand = commander.Register(fileCommands.RemoveFile, (HelpText)"Removes a file from the tracking list.", "remove", "rm");
        removeCommand.Register(fileCommands.RemoveFile, (HelpText)"Removes a file from the tracking list.", "file");
        removeCommand.Register(hostCommands.RemoveHost, (HelpText)"Removes a host from the list of known hosts.", "host");

        Command listCommand = commander.Register(fileCommands.ListFiles, (HelpText)"Lists all files.", "list", "ls");
        listCommand.Register(fileCommands.ListFiles, (HelpText)"Lists all files.", "files");
        listCommand.Register(hostCommands.ListHosts, (HelpText)"lists all hosts.", "hosts");

        commander.Register(hostCommands.CheckActiveHosts, (HelpText)"Checks the status of known hosts.", "status", "check");
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
                if (!commander.Execute(input, out _))
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