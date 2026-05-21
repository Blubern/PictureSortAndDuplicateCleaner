using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Journal;

namespace PictureSortAndDuplicateCleaner.Tests;

/// <summary>
/// In-memory <see cref="IPictureSortJournal"/> used by tests that need to seed
/// "known" hashes without touching the disk.
/// </summary>
internal sealed class InMemoryTestJournal : IPictureSortJournal
{
    private readonly HashSet<string> _hashes;
    public InMemoryTestJournal(params string[] hashes)
        => _hashes = new HashSet<string>(hashes, StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> KnownHashes => _hashes;
    public int EntriesLoaded => _hashes.Count;
    public int EntriesWritten => 0;
    public int EntriesStale => 0;
    public string FilePath => string.Empty;
    public void Load() { }
    public void Append(JournalEntry entry) => _hashes.Add(entry.Hash);
}
