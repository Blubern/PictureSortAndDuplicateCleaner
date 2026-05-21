using System.Text.Json;
using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Journal;

public sealed class FilePictureSortJournal : IPictureSortJournal
{
    public const string SchemaVersion = "picturesortandduplicatecleaner-journal/v1";
    public const string DefaultFileName = "picturesortandduplicatecleaner-journal.jsonl";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IFileSystem _fileSystem;
    private readonly string _filePath;
    private readonly object _writeLock = new();
    private readonly HashSet<string> _knownHashes = new(StringComparer.OrdinalIgnoreCase);
    private int _entriesWritten;
    private int _entriesLoaded;
    private int _entriesStale;
    private bool _loaded;

    public FilePictureSortJournal(string filePath)
        : this(filePath, DefaultFileSystem.Instance)
    {
    }

    public FilePictureSortJournal(string filePath, IFileSystem fileSystem)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Journal file path must not be empty.", nameof(filePath));
        }

        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _filePath = ResolveFilePath(filePath, _fileSystem);
    }

    public string FilePath => _filePath;
    public IReadOnlySet<string> KnownHashes => _knownHashes;
    public int EntriesLoaded => _entriesLoaded;
    public int EntriesWritten => _entriesWritten;
    public int EntriesStale => _entriesStale;

    public void Load()
    {
        if (_loaded)
        {
            return;
        }
        _loaded = true;

        if (!_fileSystem.FileExists(_filePath))
        {
            return;
        }

        foreach (var rawLine in _fileSystem.ReadAllLines(_filePath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("{\"schema\":", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            JournalEntry? entry;
            try
            {
                entry = JsonSerializer.Deserialize<JournalEntry>(line, JsonOptions);
            }
            catch (JsonException)
            {
                continue;
            }

            if (entry is null || string.IsNullOrWhiteSpace(entry.Hash))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.TargetPath) && _fileSystem.FileExists(entry.TargetPath))
            {
                _knownHashes.Add(entry.Hash);
                _entriesLoaded++;
            }
            else
            {
                _entriesStale++;
            }
        }
    }

    public void Append(JournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_writeLock)
        {
            EnsureFileInitialized();
            var serialized = JsonSerializer.Serialize(entry, JsonOptions);
            _fileSystem.AppendAllText(_filePath, serialized + Environment.NewLine);
            _knownHashes.Add(entry.Hash);
            _entriesWritten++;
        }
    }

    private void EnsureFileInitialized()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            _fileSystem.CreateDirectory(directory);
        }

        if (_fileSystem.FileExists(_filePath))
        {
            return;
        }

        var header = "{\"schema\":\"" + SchemaVersion + "\"}" + Environment.NewLine;
        _fileSystem.WriteAllText(_filePath, header);
    }

    private static string ResolveFilePath(string filePath, IFileSystem fileSystem)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (fileSystem.DirectoryExists(fullPath))
        {
            return Path.Combine(fullPath, DefaultFileName);
        }

        // Trailing slash → user clearly meant a directory even if it doesn't exist yet.
        if (filePath.EndsWith(Path.DirectorySeparatorChar) || filePath.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return Path.Combine(fullPath, DefaultFileName);
        }

        return fullPath;
    }
}
