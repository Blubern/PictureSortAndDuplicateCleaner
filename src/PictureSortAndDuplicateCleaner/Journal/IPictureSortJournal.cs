namespace PictureSortAndDuplicateCleaner.Journal;

public interface IPictureSortJournal
{
    IReadOnlySet<string> KnownHashes { get; }
    int EntriesLoaded { get; }
    int EntriesWritten { get; }
    int EntriesStale { get; }
    void Load();
    void Append(JournalEntry entry);

    /// <summary>
    /// Attempts to reuse a previously recorded hash for <paramref name="path"/>.
    /// Returns <c>true</c> only when an entry exists whose <see cref="JournalEntry.Length"/>
    /// and <see cref="JournalEntry.FileLastWriteUtc"/> still match the supplied values,
    /// in which case the file can be treated as unchanged and re-hashing is skipped.
    /// Marks the path as seen for the next <see cref="Compact"/>.
    /// </summary>
    bool TryGetCachedHash(string path, long length, DateTime lastWriteUtc, out string hash);

    /// <summary>
    /// Records the freshly computed hash for an inventoried file so a later run can
    /// skip hashing it. Upserts by path, appends a line for crash safety and marks
    /// the path as seen for the next <see cref="Compact"/>.
    /// </summary>
    void RecordInventory(string path, string hash, long length, DateTime lastWriteUtc);

    /// <summary>
    /// Rewrites the journal keeping only entries whose target file still exists and was
    /// seen during this run (via <see cref="TryGetCachedHash"/>, <see cref="RecordInventory"/>
    /// or <see cref="Append"/>). Prunes vanished files and collapses duplicate paths.
    /// Should only be called after a full target inventory, otherwise unseen-but-valid
    /// cache entries would be discarded.
    /// </summary>
    JournalCompactionResult Compact();
}
