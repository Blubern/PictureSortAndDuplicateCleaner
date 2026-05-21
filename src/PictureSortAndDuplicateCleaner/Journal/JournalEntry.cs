namespace PictureSortAndDuplicateCleaner.Journal;

public sealed record JournalEntry(string Hash, string TargetPath, DateTime MovedAtUtc);
