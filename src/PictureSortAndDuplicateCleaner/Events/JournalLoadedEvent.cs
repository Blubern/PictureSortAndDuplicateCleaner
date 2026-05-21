namespace PictureSortAndDuplicateCleaner.Events;

public sealed record JournalLoadedEvent(string FilePath, int EntriesLoaded, int EntriesStale) : PictureSortEvent;
