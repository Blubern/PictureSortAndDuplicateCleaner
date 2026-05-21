namespace PictureSortAndDuplicateCleaner.Events;

public sealed record JournalAppendedEvent(string Hash, string TargetPath) : PictureSortEvent;
