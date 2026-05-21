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
}
