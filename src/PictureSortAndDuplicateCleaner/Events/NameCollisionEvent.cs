namespace PictureSortAndDuplicateCleaner.Events;

public sealed record NameCollisionEvent(string DesiredPath, string FinalPath) : PictureSortEvent;
