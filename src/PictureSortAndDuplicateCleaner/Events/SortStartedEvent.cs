namespace PictureSortAndDuplicateCleaner.Events;

public sealed record SortStartedEvent(PictureSortParameter Parameter) : PictureSortEvent;
