namespace PictureSortAndDuplicateCleaner.Events;

public sealed record SidecarMovedEvent(string Source, string Destination, bool IsCopy, bool DryRun) : PictureSortEvent;
