using HyperbolicDownloaderApi.Managment;

using System.Diagnostics;
using System.Text.Json;

namespace HyperbolicDownloaderApi.FileProcessing;

public class FilesManager
{
    private readonly List<PrivateHyperFileInfo> files = new List<PrivateHyperFileInfo>();

    public bool TryAdd(string filePath, out PrivateHyperFileInfo? fileInfo, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            fileInfo = null;
            errorMessage = "Path is empty!";
            return false;
        }

        string fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
        {
            fileInfo = null;
            errorMessage = "Invalid file path!";
            return false;
        }

        string hash = FileValidator.CalculateHash(filePath);

        if (Contains(hash))
        {
            fileInfo = null;
            Debug.WriteLine(filePath + "-.-" + hash);
            errorMessage = "File already tracked!";
            return false;
        }

        if (files.Exists(f => f.FilePath == fullPath))
        {
            _ = files.RemoveAll(f => f.FilePath == fullPath);
        }

        fileInfo = new PrivateHyperFileInfo(hash, fullPath);

        files.Add(fileInfo);

        SaveFiles();

        errorMessage = null;
        return true;
    }

    public void AddRange(IEnumerable<PrivateHyperFileInfo> fileInfos)
    {
        foreach (PrivateHyperFileInfo fileInfo in fileInfos)
        {
            if (File.Exists(fileInfo.FilePath) && !Contains(fileInfo.Hash))
            {
                files.Add(fileInfo);
            }
        }

        SaveFiles();
    }

    public bool TryGet(string hash, out PrivateHyperFileInfo? fileInfo)
    {
        if (Contains(hash))
        {
            fileInfo = files.First(x => x.Hash == hash);
            return true;
        }
        else
        {
            fileInfo = null;
            return false;
        }
    }

    public bool TryRemove(string hash)
    {
        int count = files.RemoveAll(f => f.Hash == hash);

        SaveFiles();

        return count > 0;
    }

    public bool Contains(string? hash)
    {
        return files.Exists(f => f.Hash == hash);
    }

    public List<PrivateHyperFileInfo> ToList()
    {
        return files.ToList();
    }

    internal void RemoveFilesThatDontExist()
    {
        List<PrivateHyperFileInfo> filesToRemove = files
            .Where(f => !File.Exists(f.FilePath))
            .ToList();

        foreach (PrivateHyperFileInfo fileToRemove in filesToRemove)
        {
            _ = files.Remove(fileToRemove);
        }

        SaveFiles();
    }

    private void SaveFiles()
    {
        File.WriteAllText(ApiConfiguration.FilesInfoPath, JsonSerializer.Serialize(files));
    }
}