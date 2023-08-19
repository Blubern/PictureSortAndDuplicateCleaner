using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Directory = System.IO.Directory;

namespace PictureSort;

public class InventoryDirectory
{
    public async Task<IReadOnlyList<FileInventoryResult>> InventoryADirectoryAsync(
        IReadOnlyList<string> directories,
        int maxConcurrency,
        IProgress<string> progress,
        bool addExifAndFileInformation,
        CancellationToken cancellationToken)
    {
        var result = new List<FileInventoryResult>();
        
        progress.Report("Try to read all files in the Directory.");

        foreach (var directory in directories)
        {
            progress.Report($"Try to read all files in the Directory {directory}.");
            
            var files = new List<string>();
            var i = 0;
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                i++;
                files.Add(file);

                if ((i % 250) == 0)
                {
                    progress.Report($"We are still searching files currently we found {files.Count}.");
                }
            }

            progress.Report($"We found {files.Count} files in the directory.");

            if (files.Count == 0)
            {
                return result;
            }

            var chunkSize = files.Count;
            if (maxConcurrency < chunkSize)
                chunkSize = (chunkSize + (maxConcurrency - 1)) / maxConcurrency;

            var chunks = files.Chunk(chunkSize);

            progress.Report($"Found: {files.Count} total files in {directory}. We Split it in {maxConcurrency} parts with a chunk size {chunkSize}.");

            var chunkTasks = chunks.Select((chunk, index) =>
                CheckFilesAsync(progress, chunk, index, addExifAndFileInformation, directory, cancellationToken)).ToList();

            await Task.WhenAll(chunkTasks);

            foreach (var chunkTask in chunkTasks)
            {
                result.AddRange(chunkTask.Result);
            }
        }

        return result.AsReadOnly();
    }

    private async Task<List<FileInventoryResult>> CheckFilesAsync(IProgress<string> progress,
        IList<string> files,
        int taskNumber,
        bool addExifAndFileInformation,
        string directory,
        CancellationToken cancellationToken)
    {
        var result = new List<FileInventoryResult>();
        await Task.Run(async () =>
        {
            using var hashAlgorithm = MD5.Create();

            var i = 1;
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var hash = await GetFileHashAsync(file, hashAlgorithm);
                var originalFileName = Path.GetFileName(file);
                var creationTime = DateTime.MinValue;
                var lastWriteTime = DateTime.MinValue;
                var lastAccessTime = DateTime.MinValue;
                string? originalDateAsString = null;
                DateTime? originalDate = null;

                if (addExifAndFileInformation)
                {
                    creationTime = File.GetCreationTime(file);
                    lastWriteTime = File.GetLastWriteTime(file);
                    lastAccessTime = File.GetLastAccessTime(file);

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
                    catch (Exception e)
                    {
                        progress.Report($"Exception when reading the Metadata of the file {file}. Exception: {e}.");
                    }
                }

                FileInventoryResult fileInventoryResult;
                if (addExifAndFileInformation)
                {
                    fileInventoryResult = new FileInventoryResult(
                        file,
                        directory,
                        hash,
                        creationTime,
                        lastWriteTime,
                        lastAccessTime,
                        originalDateAsString,
                        originalDate,
                        originalFileName);
                }
                else
                {
                    fileInventoryResult = new FileInventoryResult(
                        file,
                        directory,
                        hash,
                        originalFileName);
                }
                result.Add(fileInventoryResult);

                progress.Report($"Task: {taskNumber} - File {i}/{files.Count} - Checked {fileInventoryResult}.");
                i++;
            }
        });
        
        return result;
    }

    private async Task<string> GetFileHashAsync(string file, HashAlgorithm hashAlgorithm)
    {
        await using var fs = File.OpenRead(file);
        var hashBytes = await hashAlgorithm.ComputeHashAsync(fs);
        var sb = new StringBuilder();
        foreach (var b in hashBytes)
        {
            sb.Append(b.ToString("x2").ToLower());
        }

        return sb.ToString();
    }
}