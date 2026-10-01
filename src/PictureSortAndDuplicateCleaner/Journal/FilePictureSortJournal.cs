using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Journal;

public sealed class FilePictureSortJournal : IPictureSortJournal
{
    public const string SchemaVersion = "picturesortandduplicatecleaner-journal/v2";
    public const string DefaultFileName = "picturesortandduplicatecleaner-journal.jsonl";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Comparer used for the path-keyed cache. Windows and macOS filesystems are
    /// case-insensitive, so paths differing only by case refer to the same file there.
    /// Linux filesystems are case-sensitive, so paths are compared ordinally to avoid
    /// collapsing two distinct files (e.g. <c>a.jpg</c> and <c>A.jpg</c>) into one entry.
    /// </summary>
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly IFileSystem _fileSystem;
    private readonly string _filePath;
    private readonly object _writeLock = new();
    private readonly ConcurrentDictionary<string, JournalEntry> _entriesByPath = new(PathComparer);
    private readonly ConcurrentDictionary<string, byte> _seenPaths = new(PathComparer);
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

    public IReadOnlySet<string> KnownHashes
    {
        get
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _entriesByPath.Values)
            {
                if (!string.IsNullOrWhiteSpace(entry.Hash))
                {
                    set.Add(entry.Hash);
                }
            }
            return set;
        }
    }

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

        // Loading must never create the journal file: a dry run loads the journal
        // to read known hashes but must remain completely write-free. The header is
        // written lazily on the first Append/Compact instead.
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
                // Normalize on read as well so hand-written or older lines with sub-second
                // ticks or non-UTC kinds compare correctly against probe values.
                // Last writer wins for a duplicate path (the compacted/most-recent line).
                _entriesByPath[entry.TargetPath] = entry with { FileLastWriteUtc = NormalizeMtime(entry.FileLastWriteUtc) };
            }
            else
            {
                _entriesStale++;
            }
        }

        _entriesLoaded = _entriesByPath.Count;
    }

    public void Append(JournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var normalized = entry with { FileLastWriteUtc = NormalizeMtime(entry.FileLastWriteUtc) };

        lock (_writeLock)
        {
            EnsureFileInitialized();
            var serialized = JsonSerializer.Serialize(normalized, JsonOptions);
            _fileSystem.AppendAllText(_filePath, serialized + Environment.NewLine);
            _entriesByPath[normalized.TargetPath] = normalized;
            _seenPaths[normalized.TargetPath] = 0;
            _entriesWritten++;
        }
    }

    public void RecordInventory(string path, string hash, long length, DateTime lastWriteUtc)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(hash))
        {
            return;
        }

        Append(new JournalEntry(hash, path, DateTime.UtcNow, length, lastWriteUtc));
    }

    public bool TryGetCachedHash(string path, long length, DateTime lastWriteUtc, out string hash)
    {
        if (!string.IsNullOrWhiteSpace(path)
            && _entriesByPath.TryGetValue(path, out var entry)
            && !string.IsNullOrWhiteSpace(entry.Hash)
            && entry.Length == length
            && entry.FileLastWriteUtc == NormalizeMtime(lastWriteUtc))
        {
            hash = entry.Hash;
            _seenPaths[path] = 0;
            return true;
        }

        hash = string.Empty;
        return false;
    }

    public JournalCompactionResult Compact()
    {
        lock (_writeLock)
        {
            EnsureFileInitialized();

            var kept = new List<JournalEntry>();
            foreach (var entry in _entriesByPath.Values)
            {
                if (_seenPaths.ContainsKey(entry.TargetPath) && _fileSystem.FileExists(entry.TargetPath))
                {
                    kept.Add(entry);
                }
            }

            var removed = _entriesByPath.Count - kept.Count;

            var builder = new StringBuilder();
            builder.Append("{\"schema\":\"").Append(SchemaVersion).Append("\"}").Append(Environment.NewLine);
            foreach (var entry in kept)
            {
                builder.Append(JsonSerializer.Serialize(entry, JsonOptions)).Append(Environment.NewLine);
            }

            // Write the full replacement to a sibling temp file first, then swap it in.
            // This keeps the existing journal intact if the process dies mid-write
            // instead of truncating the only copy in place.
            var tempPath = _filePath + ".compact.tmp";
            if (_fileSystem.FileExists(tempPath))
            {
                _fileSystem.Delete(tempPath);
            }
            _fileSystem.WriteAllText(tempPath, builder.ToString());
            if (_fileSystem.FileExists(_filePath))
            {
                _fileSystem.Delete(_filePath);
            }
            _fileSystem.Move(tempPath, _filePath);

            _entriesByPath.Clear();
            foreach (var entry in kept)
            {
                _entriesByPath[entry.TargetPath] = entry;
            }

            // A compaction marks the end of an inventory run; reset the "seen this run"
            // set so a subsequent run on the same instance starts fresh and does not
            // retain entries solely because they were seen in an earlier run.
            _seenPaths.Clear();

            return new JournalCompactionResult(Kept: kept.Count, Removed: removed);
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

    private static DateTime NormalizeMtime(DateTime value)
    {
        if (value == default)
        {
            return default;
        }

        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        // Truncate to whole seconds so values survive JSON round-trips and tolerate
        // sub-second drift between filesystems.
        return new DateTime(utc.Ticks - (utc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
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
