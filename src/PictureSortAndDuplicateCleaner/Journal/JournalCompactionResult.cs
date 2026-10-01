namespace PictureSortAndDuplicateCleaner.Journal;

/// <summary>
/// Outcome of <see cref="IPictureSortJournal.Compact"/>: how many entries were kept
/// (their target file is still present and was seen this run) and how many were
/// pruned (missing or no longer in the target).
/// </summary>
public readonly record struct JournalCompactionResult(int Kept, int Removed);
