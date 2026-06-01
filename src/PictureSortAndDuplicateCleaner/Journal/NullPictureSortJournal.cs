namespace PictureSortAndDuplicateCleaner.Journal;

public sealed class NullPictureSortJournal : IPictureSortJournal
{
    public static readonly NullPictureSortJournal Instance = new();

    private NullPictureSortJournal()
    {
    }

    public IReadOnlySet<string> KnownHashes { get; } = new HashSet<string>();
    public int EntriesLoaded => 0;
    public int EntriesWritten => 0;
    public int EntriesStale => 0;

    public void Load()
    {
    }

    public void Append(JournalEntry entry)
    {
    }

    public bool TryGetCachedHash(string path, long length, DateTime lastWriteUtc, out string hash)
    {
        hash = string.Empty;
        return false;
    }

    public void RecordInventory(string path, string hash, long length, DateTime lastWriteUtc)
    {
    }

    public JournalCompactionResult Compact() => new(Kept: 0, Removed: 0);
}
