namespace PictureSortAndDuplicateCleaner.Journal;

/// <summary>
/// A single line of the journal. Besides the content <see cref="Hash"/> and the
/// <see cref="TargetPath"/> it was written to, the entry carries the file's
/// <see cref="Length"/> and <see cref="FileLastWriteUtc"/> so the journal can act
/// as a hash cache: an inventoried file may skip hashing when its path, size and
/// last-write-time still match a recorded entry.
/// </summary>
public sealed record JournalEntry(
    string Hash,
    string TargetPath,
    DateTime MovedAtUtc,
    long Length = 0,
    DateTime FileLastWriteUtc = default);
