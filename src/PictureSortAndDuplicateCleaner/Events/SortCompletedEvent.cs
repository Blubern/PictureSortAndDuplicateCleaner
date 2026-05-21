namespace PictureSortAndDuplicateCleaner.Events;

public sealed record SortCompletedEvent(PictureSortResult Result) : PictureSortEvent;
