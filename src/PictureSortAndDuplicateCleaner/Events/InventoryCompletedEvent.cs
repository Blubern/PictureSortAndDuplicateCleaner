namespace PictureSortAndDuplicateCleaner.Events;

public sealed record InventoryCompletedEvent(string Directory, int Primaries, int Sidecars) : PictureSortEvent;
