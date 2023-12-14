using HyperbolicDownloaderApi.Managment;

using System.Text.Json;
using System.Timers;

namespace HyperbolicDownloaderApi.FileProcessing;

public class DirectoryWatcher
{
    private readonly FilesManager filesManager;

    private readonly List<string> directories = [];
    private readonly System.Timers.Timer timer = new(1);

    public DirectoryWatcher(FilesManager filesManager)
    {
        this.filesManager = filesManager;

        timer.AutoReset = false;
        timer.Elapsed += Timer_Elapsed;
    }

    public bool TryAdd(string path, out string? errorMessage)
    {
        if (directories.Contains(path))
        {
            errorMessage = "Directory already tracked";
            return false;
        }

        directories.Add(path);
        errorMessage = null;

        SaveDirectories();
        return true;
    }

    public void AddRange(IEnumerable<string> directoryInfos)
    {
        foreach (string directoryInfo in directoryInfos)
        {
            if (Directory.Exists(directoryInfo) && !directories.Contains(directoryInfo))
            {
                directories.Add(directoryInfo);
            }
        }

        SaveDirectories();
    }

    public bool TryRemove(string path)
    {
        if (!directories.Contains(path))
        {
            return false;
        }

        _ = directories.Remove(path);

        SaveDirectories();
        return true;
    }

    public void SaveDirectories()
    {
        File.WriteAllText(ApiConfiguration.DirectoriesInfoPath, JsonSerializer.Serialize(directories));
    }

    public List<string> ToList()
    {
        return directories;
    }

    public void Start()
    {
        timer.Start();
    }

    private void Timer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        ApiManager.SendNotificationMessageNewLine("Checking for new files...", NotificationMessageType.Log);

        timer.Interval = TimeSpan.FromMinutes(1).TotalMilliseconds;

        int newFilesCount = 0;

        foreach (string directory in directories)
        {
            ApiManager.SendNotificationMessageNewLine($"Checking directory: {directory}", NotificationMessageType.Log);
            string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                if (filesManager.TryAdd(file, out _, out _))
                {
                    newFilesCount++;
                }
            }
        }

        ApiManager.SendNotificationMessageNewLine($"Finished checking for new files. Found {newFilesCount} new file(s).", NotificationMessageType.Log);

        filesManager.RemoveFilesThatDontExist();
    }
}