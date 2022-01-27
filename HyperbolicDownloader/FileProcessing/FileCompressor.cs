using System.IO.Compression;

namespace HyperbolicDownloader.FileProcessing;

internal static class FileCompressor
{
    public static void CompressFile(string inputFilePath, string compressedFilePath)
    {
        using FileStream originalFileStream = File.Open(inputFilePath, FileMode.Open);
        using FileStream compressedFileStream = File.Create(compressedFilePath);
        using GZipStream? compressor = new GZipStream(compressedFileStream, CompressionMode.Compress);
        originalFileStream.CopyTo(compressor);
    }

    public static void DecompressFile(string compressedFilePath, string outputFilePath)
    {
        using FileStream compressedFileStream = File.Open(compressedFilePath, FileMode.Open);
        using FileStream outputFileStream = File.Create(outputFilePath);
        using GZipStream? decompressor = new GZipStream(compressedFileStream, CompressionMode.Decompress);
        decompressor.CopyTo(outputFileStream);
    }

    public static IEnumerable<byte[]> ReadChunks(string path, int chunkSize)
    {
        byte[] buffer = new byte[chunkSize];
        int bytesRead;
        using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read))
        using (BufferedStream bs = new BufferedStream(fs))
        {
            while ((bytesRead = bs.Read(buffer, 0, chunkSize)) != 0)
            {
                yield return buffer;
            }
        }
    }
}