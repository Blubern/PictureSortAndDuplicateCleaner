using System.Globalization;
using System.Security.Cryptography;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Extensions.Logging;
using Directory = System.IO.Directory;

namespace PictureSort;

public class InventoryDirectory
{
    private readonly ILogger<InventoryDirectory> _logger;

    public InventoryDirectory(ILogger<InventoryDirectory> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<FileInventoryResult>> InventoryADirectoryAsync(
        string directory,
        int maxConcurrency,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        progress.Report("Read all files in the Directory.");
        var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);
        progress.Report($"We found {files.Length} files in the directory.");

        var chunkSize = files.Length;
        if (maxConcurrency < chunkSize)
            chunkSize = (chunkSize + (maxConcurrency - 1)) / maxConcurrency;

        var chunks = files.Chunk(chunkSize);

        _logger.LogDebug($"Found: {files.Length} total files in {directory}. We Split it in {maxConcurrency} parts with a chunk size {chunkSize}.");

        var chunkTasks = chunks.Select(chunk => CheckFilesAsync(progress, chunk)).ToList();

        await Task.WhenAll(chunkTasks);

        var result = new List<FileInventoryResult>();
        foreach (var chunkTask in chunkTasks)
        {
            result.AddRange(chunkTask.Result);
        }
       
        return result.AsReadOnly();
    }

    private async Task<List<FileInventoryResult>> CheckFilesAsync(IProgress<string> progress, IEnumerable<string> files)
    {
        var result = new List<FileInventoryResult>();
        using var hashAlgorithm = MD5.Create();
        
        foreach (var file in files)
        {
            var hash = await GetFileHashAsync(file, hashAlgorithm);
            var originalFileName = Path.GetFileName(file);
            var creationTime = File.GetCreationTime(file);
            var lastWriteTime = File.GetLastWriteTime(file);
            var lastAccessTime = File.GetLastAccessTime(file);

            string? originalDateAsString = null;
            DateTime? originalDate = null;

            try
            {
                var directories = ImageMetadataReader.ReadMetadata(file);
                var exifSubDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                originalDateAsString = exifSubDirectory?.GetDescription(ExifDirectoryBase.TagDateTimeOriginal);

                if (!string.IsNullOrWhiteSpace(originalDateAsString))
                {
                    try
                    {
                        originalDate = DateTime.ParseExact(originalDateAsString, "yyyy:MM:dd HH:mm:ss",
                            CultureInfo.InvariantCulture);
                    }
                    catch (FormatException)
                    {
                        try
                        {
                            originalDate = DateTime.ParseExact(originalDateAsString, "yyyy-MM-dd HH:mm:ss",
                                CultureInfo.InvariantCulture);
                        }
                        catch (FormatException)
                        {
                        }
                    }
                }
            }
            catch (ImageProcessingException e)
            {
            }

            var fileInventoryResult = new FileInventoryResult(
                file,
                hash,
                creationTime,
                lastWriteTime,
                lastAccessTime,
                originalDateAsString,
                originalDate,
                originalFileName);

            result.Add(fileInventoryResult);

            progress.Report($"Checked {fileInventoryResult}.");
        }

        return result;
    }

    private async Task<string> GetFileHashAsync(string file, HashAlgorithm hashAlgorithm)
    {
        await using var fs = File.OpenRead(file);
        var hashBytes = await hashAlgorithm.ComputeHashAsync(fs);
        return Convert.ToBase64String(hashBytes);
    }
}