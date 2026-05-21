namespace PictureSortAndDuplicateCleaner.Journal;

public interface IPictureSortJournal
{
    IReadOnlySet<string> KnownHashes { get; }
    int EntriesLoaded { get; }
    int EntriesWritten { get; }
    int EntriesStale { get; }
    void Load();
    void Append(JournalEntry entry);
}
