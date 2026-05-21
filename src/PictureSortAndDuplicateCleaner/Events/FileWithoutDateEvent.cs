namespace PictureSortAndDuplicateCleaner.Events;

public sealed record FileWithoutDateEvent(string SourcePath, UnknownDatePolicy Policy) : PictureSortEvent;
