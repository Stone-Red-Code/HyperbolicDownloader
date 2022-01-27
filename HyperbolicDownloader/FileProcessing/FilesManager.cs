using System.Text.Json;

namespace HyperbolicDownloader.FileProcessing;

internal class FilesManager
{
    private readonly List<HyperFileInfo> files = new List<HyperFileInfo>();

    public bool TryAdd(string filePath, out HyperFileInfo? fileInfo, out string? errorMessage)
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
            errorMessage = "File already tracked!";
            return false;
        }

        fileInfo = new HyperFileInfo(hash, fullPath);

        files.Add(fileInfo);

        SaveFiles();

        errorMessage = null;
        return true;
    }

    public void AddRange(IEnumerable<HyperFileInfo> fileInfos)
    {
        foreach (HyperFileInfo fileInfo in fileInfos)
        {
            if (File.Exists(fileInfo.FilePath) && !Contains(fileInfo.Hash))
            {
                files.Add(fileInfo);
            }
        }

        SaveFiles();
    }

    public bool TryGet(string hash, out HyperFileInfo? fileInfo)
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
        return files.Any(f => f.Hash == hash);
    }

    public List<HyperFileInfo> ToList()
    {
        return files.ToList();
    }

    private void SaveFiles()
    {
        File.WriteAllText(Program.FilesInfoPath, JsonSerializer.Serialize(files));
    }
}