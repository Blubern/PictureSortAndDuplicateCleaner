namespace PictureSortAndDuplicateCleaner;

public class PictureSortResult
{
    public PictureSortResult(
        int sourceFilesFound,
        int targetFilesFound,
        int filesMovedToTarget,
        int sourceFilesIgnored,
        int targetFilesIgnored,
        int duplicateFilesMoved,
        int alreadyExistingFilesMoved,
        int errorCount,
        int sidecarsMoved = 0,
        int sidecarsOrphaned = 0,
        int journalEntriesLoaded = 0,
        int journalEntriesWritten = 0,
        int journalEntriesStale = 0,
        int filesWithoutDateSkipped = 0,
        IReadOnlyList<PictureSortError>? errors = null)
    {
        SourceFilesFound = sourceFilesFound;
        TargetFilesFound = targetFilesFound;
        FilesMovedToTarget = filesMovedToTarget;
        SourceFilesIgnored = sourceFilesIgnored;
        TargetFilesIgnored = targetFilesIgnored;
        DuplicateFilesMoved = duplicateFilesMoved;
        AlreadyExistingFilesMoved = alreadyExistingFilesMoved;
        ErrorCount = errorCount;
        SidecarsMoved = sidecarsMoved;
        SidecarsOrphaned = sidecarsOrphaned;
        JournalEntriesLoaded = journalEntriesLoaded;
        JournalEntriesWritten = journalEntriesWritten;
        JournalEntriesStale = journalEntriesStale;
        FilesWithoutDateSkipped = filesWithoutDateSkipped;
        Errors = errors ?? Array.Empty<PictureSortError>();
    }

    public int SourceFilesFound { get; }

    public int TargetFilesFound { get; }

    public int FilesMovedToTarget { get; }

    public int SourceFilesIgnored { get; }

    public int TargetFilesIgnored { get; }

    public int DuplicateFilesMoved { get; }

    public int AlreadyExistingFilesMoved { get; }

    public int TotalFilesIgnored => SourceFilesIgnored + TargetFilesIgnored;

    public int ErrorCount { get; }

    public bool HasErrors => ErrorCount > 0;

    public int SidecarsMoved { get; }

    public int SidecarsOrphaned { get; }

    public int JournalEntriesLoaded { get; }

    public int JournalEntriesWritten { get; }

    public int JournalEntriesStale { get; }

    public int FilesWithoutDateSkipped { get; }

    /// <summary>
    /// Per-file failures collected during the run. Empty when <see cref="ErrorCount"/> is 0.
    /// </summary>
    public IReadOnlyList<PictureSortError> Errors { get; }
}

