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

    public InputHandler(HostsManager hostsManager, FilesManager filesManager, DirectoryWatcher directoryWatcher)
    {
        HostCommands hostCommands = new HostCommands(hostsManager);
        FileCommands fileCommands = new FileCommands(hostsManager, filesManager);
        DirectoryCommands directoryCommands = new DirectoryCommands(directoryWatcher);
        DownloadCommands downloadCommands = new DownloadCommands(hostsManager, filesManager);
        StreamingCommands streamingCommands = new StreamingCommands(hostsManager);

        ClientCommands clientCommands = new ClientCommands();
        LogCommands logCommands = new LogCommands();

        _ = commander.Register(input => commander.PrintHelp(input), (HelpText)"Lists all commands.", "help", "h", "?");
        _ = commander.Register(_ => Console.Clear(), (HelpText)"Clears the console.", "clear", "cls");
        _ = commander.Register(_ => exit = true, (HelpText)"Exits the application.", "exit", "quit");
        _ = commander.Register(clientCommands.ShowInfo, (HelpText)"Displays the private and public IP address.", "info", "inf");
        _ = commander.Register(hostCommands.Discover, (HelpText)"Tries to find other active hosts on the local network.", "discover", "disc");
        _ = commander.Register(hostCommands.Sync, (HelpText)"Syncs the host list", "sync");

        Command logCommand = commander.Register((_) => logCommands.Log(false), (HelpText)"Displays live log.", "log");
        _ = logCommand.Register((_) => logCommands.Log(true), (HelpText)"Displays live debug log.", "debug");

        Command getCommand = commander.Register(downloadCommands.GetFile, (HelpText)"Attempts to retrieve a file from another host using a hash.", "get");
        _ = getCommand.Register(downloadCommands.GetFileFrom, (HelpText)"Attempts to retrieve a file from another host using a .hyper file.", "from");

        Command streamCommand = commander.Register(streamingCommands.StreamWav, (HelpText)"Attempts to stream a .wav file from another host using a hash.", "stream");
        _ = streamCommand.Register(streamingCommands.GetWavStreamFrom, (HelpText)"Attempts to stream a .wav file from another host using a .hyper file.", "from");

        Command generateCommad = commander.Register(fileCommands.GenerateFileFull, (HelpText)"Generates a .hyper file from a file hash.", "generate", "gen");
        _ = generateCommad.Register(fileCommands.GenerateFileSingle, (HelpText)"Generates a .hyper file from a file hash without checking the known hosts. This adds only the local host to the file.", "noscan");

        Command addCommand = commander.Register(fileCommands.AddFile, (HelpText)"Adds a file to the tracking list.", "add");
        _ = addCommand.Register(fileCommands.AddFile, (HelpText)"Adds a file to the tracking list.", "file");
        _ = addCommand.Register(directoryCommands.AddDirectory, (HelpText)"Adds a directory to the tracking list.", "directory", "dir");
        _ = addCommand.Register(hostCommands.AddHost, (HelpText)"Adds a host to the list of known hosts.", "host");

        Command removeCommand = commander.Register(fileCommands.RemoveFile, (HelpText)"Removes a file from the tracking list.", "remove", "rm");
        _ = removeCommand.Register(fileCommands.RemoveFile, (HelpText)"Removes a file from the tracking list.", "file");
        _ = removeCommand.Register(directoryCommands.RemoveDirectory, (HelpText)"Removes a directory from the tracking list.", "directory", "dir");
        _ = removeCommand.Register(hostCommands.RemoveHost, (HelpText)"Removes a host from the list of known hosts.", "host");

        Command listCommand = commander.Register(fileCommands.ListFiles, (HelpText)"Lists all files.", "list", "ls");
        Command listFilesCommand = listCommand.Register(fileCommands.ListFiles, (HelpText)"Lists all files.", "files");
        _ = listFilesCommand.Register(fileCommands.ListFilesRemote, (HelpText)"Lists all files of another host.", "remote");
        _ = listCommand.Register(directoryCommands.ListDirectories, (HelpText)"Lists all directories.", "directories", "dirs");
        _ = listCommand.Register(hostCommands.ListHosts, (HelpText)"lists all hosts.", "hosts");

        _ = commander.Register(hostCommands.CheckActiveHosts, (HelpText)"Checks the status of known hosts.", "status", "check");
    }

    public void ReadInput()
    {
        while (!exit)
        {
            Console.WriteLine();
            Console.Write("> ");
            Console.CursorVisible = true;

            string input = Console.ReadLine()?.Trim() ?? string.Empty;
            ExecuteInput(input);
        }
    }

    public void ExecuteInput(string input)
    {
        Console.CursorVisible = false;
        try
        {
            if (!commander.Execute(input, out _))
            {
                ConsoleExt.WriteLine("Unknown command! Use `help` to list all commands.", ConsoleColor.Red);
            }
        }
        catch (Exception ex)
        {
            ConsoleExt.WriteLine($"An unexpected error occurred! {ex}", ConsoleColor.Red);
        }
    }
}