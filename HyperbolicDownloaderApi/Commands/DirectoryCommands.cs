using HyperbolicDownloaderApi.FileProcessing;
using HyperbolicDownloaderApi.Managment;

namespace HyperbolicDownloaderApi.Commands;

public class DirectoryCommands(DirectoryWatcher directoryWatcher)
{
    public void AddDirectory(string directoryPath)
    {
        if (directoryWatcher.TryAdd(directoryPath, out string? message))
        {
            ApiManager.SendNotificationMessageNewLine($"Added directory: {directoryPath}", NotificationMessageType.Success);
        }
        else
        {
            ApiManager.SendNotificationMessageNewLine(message!, NotificationMessageType.Error);
        }
    }

    public void RemoveDirectory(string args)
    {
        if (int.TryParse(args, out int index))
        {
            List<string> fileInfos = directoryWatcher.ToList();

            if (index < 1 || index > fileInfos.Count)
            {
                ApiManager.SendNotificationMessageNewLine("Invalid index!", NotificationMessageType.Error);
                return;
            }

            args = fileInfos[index - 1];
        }

        if (directoryWatcher.TryRemove(args))
        {
            ApiManager.SendNotificationMessageNewLine($"Removed directory: {args}", NotificationMessageType.Success);
        }
        else
        {
            ApiManager.SendNotificationMessageNewLine("Directory is not tracked!", NotificationMessageType.Error);
        }
    }

    public void ListDirectories(string searchString)
    {
        int index = 0;
        int directoryCount = 0;
        List<string> directoryInfos = directoryWatcher.ToList();

        if (directoryInfos.Count == 0)
        {
            ApiManager.SendNotificationMessageNewLine("No tracked directories!", NotificationMessageType.Warning);
            return;
        }

        foreach (string directoryInfo in directoryInfos)
        {
            index++;

            if (!string.IsNullOrWhiteSpace(searchString) && !directoryInfo.Contains(searchString, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            directoryCount++;

            ApiManager.SendNotificationMessageNewLine($"{index}) {directoryInfo}");
            ApiManager.SendNotificationMessageNewLine(string.Empty);
        }

        if (directoryCount == 0)
        {
            ApiManager.SendNotificationMessage($"No directories found containing \"{searchString}\".", NotificationMessageType.Warning);
        }
        else
        {
            Console.CursorTop--;
        }
    }
}