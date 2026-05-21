namespace PictureSortAndDuplicateCleaner.Events;

public sealed record FileMovedEvent(string Source, string Destination, bool IsCopy, bool DryRun) : PictureSortEvent;
