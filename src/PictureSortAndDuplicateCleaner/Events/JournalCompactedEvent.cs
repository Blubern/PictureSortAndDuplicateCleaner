namespace PictureSortAndDuplicateCleaner.Events;

public sealed record JournalCompactedEvent(int Kept, int Removed) : PictureSortEvent;
