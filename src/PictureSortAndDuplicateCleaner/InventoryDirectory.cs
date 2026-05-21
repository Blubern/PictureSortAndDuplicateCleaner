using System.Collections.Concurrent;
using System.Globalization;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using PictureSortAndDuplicateCleaner.Abstractions;
using PictureSortAndDuplicateCleaner.Exif;
using PictureSortAndDuplicateCleaner.Sidecars;

namespace PictureSortAndDuplicateCleaner;

public class InventoryDirectory
{
    private readonly DateExtractorChain _dateExtractorChain;
    private readonly IFileSystem _fileSystem;
    private readonly IContentHasher _contentHasher;

    public InventoryDirectory()
        : this(DateExtractorChain.Default, DefaultFileSystem.Instance, contentHasher: null)
    {
    }

    public InventoryDirectory(DateExtractorChain dateExtractorChain)
        : this(dateExtractorChain, DefaultFileSystem.Instance, contentHasher: null)
    {
    }

    public InventoryDirectory(IFileSystem fileSystem)
        : this(DateExtractorChain.Default, fileSystem, contentHasher: null)
    {
    }

    public InventoryDirectory(DateExtractorChain dateExtractorChain, IFileSystem fileSystem)
        : this(dateExtractorChain, fileSystem, contentHasher: null)
    {
    }

    public InventoryDirectory(IFileSystem fileSystem, IContentHasher contentHasher)
        : this(DateExtractorChain.Default, fileSystem, contentHasher)
    {
    }

    public InventoryDirectory(DateExtractorChain dateExtractorChain, IFileSystem fileSystem, IContentHasher? contentHasher)
    {
        _dateExtractorChain = dateExtractorChain ?? throw new ArgumentNullException(nameof(dateExtractorChain));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _contentHasher = contentHasher ?? new FileBytesContentHasher(_fileSystem);
    }

    public async Task<InventoryResult> InventoryADirectoryAsync(
        IReadOnlyList<string> directories,
        int maxConcurrency,
        IProgress<string> progress,
        bool addExifAndFileInformation,
        IReadOnlyList<string> sidecarExtensions,
        CancellationToken cancellationToken)
    {
        var files = new List<FileInventoryResult>();
        var sidecars = new List<SidecarFile>();
        var sidecarLookup = sidecarExtensions.Count == 0
            ? null
            : new HashSet<string>(sidecarExtensions, StringComparer.OrdinalIgnoreCase);

        progress.Report("Try to read all files in the Directory.");

        foreach (var directory in directories)
        {
            progress.Report($"Try to read all files in the Directory {directory}.");

            var primaryFiles = new List<string>();
            var sidecarFiles = new List<string>();
            var i = 0;
            foreach (var file in _fileSystem.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                i++;
                if (sidecarLookup is not null && sidecarLookup.Contains(Path.GetExtension(file)))
                {
                    sidecarFiles.Add(file);
                }
                else
                {
                    primaryFiles.Add(file);
                }

                if ((i % 250) == 0)
                {
                    progress.Report($"We are still searching files currently we found {primaryFiles.Count} primaries and {sidecarFiles.Count} sidecars.");
                }
            }

            progress.Report($"We found {primaryFiles.Count} primaries and {sidecarFiles.Count} sidecars in the directory.");

            foreach (var sidecar in sidecarFiles)
            {
                sidecars.Add(new SidecarFile(sidecar, directory));
            }

            if (primaryFiles.Count == 0)
            {
                continue;
            }

            progress.Report($"Inventorying {primaryFiles.Count} files in {directory} with max concurrency {maxConcurrency}.");

            var bag = new ConcurrentBag<FileInventoryResult>();
            var processedCount = 0;
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxConcurrency,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(primaryFiles, parallelOptions, async (file, ct) =>
            {
                var item = await CreateFileInventoryResultAsync(file, directory, addExifAndFileInformation, progress, ct);
                bag.Add(item);
                var current = Interlocked.Increment(ref processedCount);
                progress.Report($"File {current}/{primaryFiles.Count} - Checked {item}.");
            });

            files.AddRange(bag);
        }

        return new InventoryResult(files.AsReadOnly(), sidecars.AsReadOnly());
    }

    private async Task<FileInventoryResult> CreateFileInventoryResultAsync(
        string file,
        string directory,
        bool addExifAndFileInformation,
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var hash = await GetFileHashAsync(file, cancellationToken);
        var originalFileName = Path.GetFileName(file);
        long length = 0;
        try
        {
            length = _fileSystem.GetFileLength(file);
        }
        catch (Exception e)
        {
            progress.Report($"Could not read file length for {file}: {e.Message}.");
        }

        if (!addExifAndFileInformation)
        {
            return new FileInventoryResult(file, directory, hash, originalFileName, length);
        }

        var creationTime = _fileSystem.GetCreationTime(file);
        var lastWriteTime = _fileSystem.GetLastWriteTime(file);
        var lastAccessTime = _fileSystem.GetLastAccessTime(file);
        string? originalDateAsString = null;
        DateTime? originalDate = null;

        try
        {
            using var metadataStream = _fileSystem.OpenRead(file);
            var directories = ImageMetadataReader.ReadMetadata(metadataStream);
            var (date, raw, source) = _dateExtractorChain.Extract(directories);
            if (date.HasValue)
            {
                originalDate = date;
                originalDateAsString = raw;
                if (!string.IsNullOrEmpty(source))
                {
                    progress.Report($"Date for {file} resolved via {source}: '{raw}'.");
                }
            }
        }
        catch (Exception e)
        {
            progress.Report($"Exception when reading the Metadata of the file {file}. Exception: {e.Message}.");
        }

        return new FileInventoryResult(
            file,
            directory,
            hash,
            creationTime,
            lastWriteTime,
            lastAccessTime,
            originalDateAsString,
            originalDate,
            originalFileName,
            length);
    }

    private Task<string> GetFileHashAsync(string file, CancellationToken cancellationToken)
        => _contentHasher.ComputeHashAsync(file, cancellationToken);
}