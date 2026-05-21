namespace PictureSortAndDuplicateCleaner.Events;

public sealed record OrphanSidecarEvent(string SidecarPath) : PictureSortEvent;
