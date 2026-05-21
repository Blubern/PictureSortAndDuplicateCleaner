namespace PictureSortAndDuplicateCleaner.Events;

public sealed record DuplicateDetectedEvent(string SourcePath, string Hash) : PictureSortEvent;
