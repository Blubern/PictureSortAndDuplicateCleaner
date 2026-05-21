namespace PictureSortAndDuplicateCleaner.Events;

public sealed record FileFailedEvent(string Source, string Reason, string ExceptionMessage) : PictureSortEvent;
