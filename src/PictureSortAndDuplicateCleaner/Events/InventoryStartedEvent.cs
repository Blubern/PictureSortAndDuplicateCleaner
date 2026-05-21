namespace PictureSortAndDuplicateCleaner.Events;

public sealed record InventoryStartedEvent(string Directory) : PictureSortEvent;
