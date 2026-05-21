namespace PictureSortAndDuplicateCleaner.Events;

public sealed record AlreadyExistsInTargetEvent(string SourcePath, string Hash, bool ViaJournal) : PictureSortEvent;
