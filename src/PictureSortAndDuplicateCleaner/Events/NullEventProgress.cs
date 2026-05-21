namespace PictureSortAndDuplicateCleaner.Events;

internal sealed class NullEventProgress : IProgress<PictureSortEvent>
{
    public static readonly NullEventProgress Instance = new();
    public void Report(PictureSortEvent value) { /* swallow */ }
}
