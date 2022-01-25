using System.IO.Compression;

namespace HyperbolicDowloader.FileProcessing;

internal class FileCompressor
{
    private static void CompressFile(string inputFilePath, string compressedFilePath)
    {
        using FileStream originalFileStream = File.Open(inputFilePath, FileMode.Open);
        using FileStream compressedFileStream = File.Create(compressedFilePath);
        using GZipStream? compressor = new GZipStream(compressedFileStream, CompressionMode.Compress);
        originalFileStream.CopyTo(compressor);
    }

    private static void DecompressFile(string compressedFilePath, string outputFilePath)
    {
        using FileStream compressedFileStream = File.Open(compressedFilePath, FileMode.Open);
        using FileStream outputFileStream = File.Create(outputFilePath);
        using GZipStream? decompressor = new GZipStream(compressedFileStream, CompressionMode.Decompress);
        decompressor.CopyTo(outputFileStream);
    }
}