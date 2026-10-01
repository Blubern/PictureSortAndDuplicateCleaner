using PictureSortAndDuplicateCleaner.Abstractions;
using PictureSortAndDuplicateCleaner.Events;
using PictureSortAndDuplicateCleaner.Journal;
using PictureSortAndDuplicateCleaner.Sidecars;
using System.Collections.Concurrent;

namespace PictureSortAndDuplicateCleaner;

public class PictureSorter
{
    private const string DuplicateIgnoredReasonPart = "This file is a duplicate file";
    private const string AlreadyExistingIgnoredReason = "The File exists already in the Target.";

    private readonly InventoryDirectory _inventoryDirectory;
    private readonly IFileSystem _fileSystem;
    private readonly IClock _clock;
    private IProgress<PictureSortEvent> _events = NullEventProgress.Instance;
    private ConcurrentBag<PictureSortError> _errors = new();

    private sealed record MoveFileAndSidecarsResult(string PrimaryFinalPath, int SidecarsMoved, int SidecarErrors);
    private sealed record TransferResult(string FinalPath, bool Renamed);

    public PictureSorter(InventoryDirectory inventoryDirectory)
        : this(inventoryDirectory, DefaultFileSystem.Instance, SystemClock.Instance)
    {
    }

    public PictureSorter(InventoryDirectory inventoryDirectory, IFileSystem fileSystem)
        : this(inventoryDirectory, fileSystem, SystemClock.Instance)
    {
    }

    public PictureSorter(InventoryDirectory inventoryDirectory, IFileSystem fileSystem, IClock clock)
    {
        _inventoryDirectory = inventoryDirectory;
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<PictureSortResult> StartPictureSortAsync(
        PictureSortParameter pictureSortParameter,
        IProgress<string> progress,
        CancellationToken cancellationToken)
        => StartPictureSortAsync(pictureSortParameter, progress, NullEventProgress.Instance, cancellationToken);

    public async Task<PictureSortResult> StartPictureSortAsync(
        PictureSortParameter pictureSortParameter,
        IProgress<string> progress,
        IProgress<PictureSortEvent> events,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _events = events ?? NullEventProgress.Instance;
        _errors = new ConcurrentBag<PictureSortError>();

        PictureSortParameterValidator.Validate(pictureSortParameter, _fileSystem);

        progress.Report($"Starting the Picture sort process. With the following Parameter {pictureSortParameter}.");
        _events.Report(new SortStartedEvent(pictureSortParameter));

        IPictureSortJournal journal = NullPictureSortJournal.Instance;
        if (!string.IsNullOrWhiteSpace(pictureSortParameter.JournalFilePath))
        {
            var fileJournal = new FilePictureSortJournal(pictureSortParameter.JournalFilePath, _fileSystem);
            fileJournal.Load();
            journal = fileJournal;
            progress.Report($"Journal enabled at '{fileJournal.FilePath}'. Loaded {journal.EntriesLoaded} entries (stale: {journal.EntriesStale}).");
            _events.Report(new JournalLoadedEvent(fileJournal.FilePath, journal.EntriesLoaded, journal.EntriesStale));
        }
        else
        {
            progress.Report("DISABLED: Journal.");
        }

        progress.Report("Making a inventor of the Source Directory.");
        var sourceLabel = string.Join(";", pictureSortParameter.SourceDirectories);
        _events.Report(new InventoryStartedEvent(sourceLabel));
        var sourceInventory = await _inventoryDirectory.InventoryADirectoryAsync(
            pictureSortParameter.SourceDirectories,
            pictureSortParameter.MaxConcurrency,
            progress,
            true,
            pictureSortParameter.SidecarExtensions,
            cancellationToken);
        _events.Report(new InventoryCompletedEvent(sourceLabel, sourceInventory.Files.Count, sourceInventory.Sidecars.Count));

        InventoryResult targetInventory = InventoryResult.Empty;
        if (pictureSortParameter.InventoryOfTheTargetDirectory)
        {
            progress.Report("Making a inventor of the Target Directory.");
            _events.Report(new InventoryStartedEvent(pictureSortParameter.TargetDirectory));
            targetInventory = await _inventoryDirectory.InventoryADirectoryAsync(
                new[] { pictureSortParameter.TargetDirectory },
                pictureSortParameter.MaxConcurrency,
                progress,
                false,
                Array.Empty<string>(),
                journal,
                cancellationToken);
            _events.Report(new InventoryCompletedEvent(pictureSortParameter.TargetDirectory, targetInventory.Files.Count, targetInventory.Sidecars.Count));
        }
        else
        {
            progress.Report("DISABLED: Making a inventor of the Target Directory.");
        }

        var sourceFiles = sourceInventory.Files;
        var targetFiles = targetInventory.Files;
        var sidecarMatcher = new SidecarMatcher(sourceInventory.Sidecars, sourceFiles);

        var sidecarsMoved = 0;
        var duplicateMoveErrorCount = 0;

        var sourceDuplicateCandidates = sourceFiles.MarkDuplicatesAndCollectMoveCandidates(
            progress,
            pictureSortParameter.DuplicateFolderName,
            cancellationToken,
            pictureSortParameter.DuplicateVerification);
        if (pictureSortParameter.MoveDuplicateFilesInSourceDirectory && pictureSortParameter.OperationMode == OperationMode.Move)
        {
            duplicateMoveErrorCount += MoveDuplicateCandidates(
                sourceDuplicateCandidates,
                progress,
                sidecarMatcher,
                ref sidecarsMoved,
                pictureSortParameter.DryRun,
                cancellationToken,
                duplicateFolderName: pictureSortParameter.DuplicateFolderName,
                moveToSourceRoot: false,
                sourceRoot: null);
        }
        else if (pictureSortParameter.OperationMode == OperationMode.Copy && sourceDuplicateCandidates.Count > 0)
        {
            progress.Report($"SKIPPED source duplicate reorg ({sourceDuplicateCandidates.Count} candidates) — OperationMode=Copy leaves source untouched.");
        }

        var targetDuplicateCandidates = targetFiles.MarkDuplicatesAndCollectMoveCandidates(
            progress,
            pictureSortParameter.DuplicateInTargetFolderName,
            cancellationToken,
            pictureSortParameter.DuplicateVerification);
        if (pictureSortParameter.MoveDuplicateFilesInTargetDirectory)
        {
            duplicateMoveErrorCount += MoveDuplicateCandidates(
                targetDuplicateCandidates,
                progress,
                sidecarMatcher: null,
                ref sidecarsMoved,
                pictureSortParameter.DryRun,
                cancellationToken,
                duplicateFolderName: pictureSortParameter.DuplicateInTargetFolderName,
                moveToSourceRoot: true,
                sourceRoot: pictureSortParameter.SourceDirectories.FirstOrDefault());
        }

        var existingErrorCount = 0;
        if (pictureSortParameter.OperationMode == OperationMode.Move)
        {
            existingErrorCount = CheckForExistingFilesInTarget(
                sourceDirectoryInventory: sourceFiles,
                targetDirectoryInventory: targetFiles,
                alreadyExistingFolderName: pictureSortParameter.AlreadyExistingFolderName,
                sidecarMatcher: sidecarMatcher,
                sidecarsMoved: ref sidecarsMoved,
                journal: journal,
                progress: progress,
                dryRun: pictureSortParameter.DryRun,
                duplicateVerification: pictureSortParameter.DuplicateVerification,
                cancellationToken: cancellationToken);
        }
        else
        {
            // In Copy mode the user wants source untouched. Files already in target are simply
            // skipped from copying — handled inline below by checking against targetHashes/journal.
            MarkAlreadyExistingForCopyMode(sourceFiles, targetFiles, journal, progress, pictureSortParameter.DuplicateVerification);
        }

        var filesPendingMove = sourceFiles.Count(a => !a.IsIgnored);

        var (moveErrorCount, sidecarMovedDuringFinalMove, filesWithoutDateSkipped) = await MovePicturesToTheTargetFolderAsync(
            sourceFiles,
            pictureSortParameter.TargetDirectory,
            progress,
            pictureSortParameter.MaxConcurrency,
            sidecarMatcher,
            journal,
            pictureSortParameter.FolderTemplate,
            pictureSortParameter.UnknownDatePolicy,
            pictureSortParameter.DryRun,
            pictureSortParameter.OperationMode,
            cancellationToken);
        sidecarsMoved += sidecarMovedDuringFinalMove;

        ReportOrphanSidecars(sidecarMatcher.Orphans, progress);

        if (pictureSortParameter.OperationMode == OperationMode.Move && !pictureSortParameter.DryRun)
        {
            progress.Report("Cleaning Empty Directories in the Source Directories.");
            foreach (var sourceDirectory in pictureSortParameter.SourceDirectories)
            {
                DeleteEmptyDirectory(sourceDirectory);
            }
        }
        else
        {
            progress.Report($"SKIPPED empty-source-directory cleanup (DryRun={pictureSortParameter.DryRun}, OperationMode={pictureSortParameter.OperationMode}).");
        }

        // The journal doubles as a hash cache for the target inventory. Only compact when a full
        // target inventory ran this turn; otherwise unseen-but-valid cache entries would be pruned.
        if (pictureSortParameter.InventoryOfTheTargetDirectory
            && !pictureSortParameter.DryRun
            && !ReferenceEquals(journal, NullPictureSortJournal.Instance))
        {
            var compaction = journal.Compact();
            progress.Report($"Journal compacted: kept {compaction.Kept} entries, pruned {compaction.Removed}.");
            _events.Report(new JournalCompactedEvent(compaction.Kept, compaction.Removed));
        }

        var sourceFilesIgnored = sourceFiles.Count(a => a.IsIgnored);
        var targetFilesIgnored = targetFiles.Count(a => a.IsIgnored);
        var duplicateFilesMoved = sourceFiles
            .Concat(targetFiles)
            .Count(a => a.IgnoredReason.Contains(DuplicateIgnoredReasonPart, StringComparison.OrdinalIgnoreCase));
        var alreadyExistingFilesMoved = sourceFiles
            .Count(a => a.IgnoredReason.Equals(AlreadyExistingIgnoredReason, StringComparison.OrdinalIgnoreCase));
        var errorCount = duplicateMoveErrorCount + existingErrorCount + moveErrorCount;
        var filesMovedToTarget = Math.Max(0, filesPendingMove - moveErrorCount - filesWithoutDateSkipped);

        var result = new PictureSortResult(
            sourceFiles.Count,
            targetFiles.Count,
            filesMovedToTarget,
            sourceFilesIgnored,
            targetFilesIgnored,
            duplicateFilesMoved,
            alreadyExistingFilesMoved,
            errorCount,
            sidecarsMoved,
            sidecarMatcher.Orphans.Count,
            journal.EntriesLoaded,
            journal.EntriesWritten,
            journal.EntriesStale,
            filesWithoutDateSkipped,
            _errors.ToArray());

        _events.Report(new SortCompletedEvent(result));
        return result;
    }

    private int MoveDuplicateCandidates(
        IReadOnlyList<DuplicateMoveCandidate> candidates,
        IProgress<string> progress,
        SidecarMatcher? sidecarMatcher,
        ref int sidecarsMoved,
        bool dryRun,
        CancellationToken cancellationToken,
        string duplicateFolderName,
        bool moveToSourceRoot,
        string? sourceRoot)
    {
        var errorCount = 0;
        for (var i = 0; i < candidates.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var candidate = candidates[i];
            _events.Report(new DuplicateDetectedEvent(candidate.File.FullPath, candidate.File.Hash));
            var sidecars = sidecarMatcher?.Find(candidate.File) ?? Array.Empty<SidecarFile>();
            var targetDirectory = ResolveDuplicateTargetDirectory(candidate, duplicateFolderName, moveToSourceRoot, sourceRoot);
            var result = MoveFileAndSidecars(
                candidate.File.FullPath,
                targetDirectory,
                candidate.File.OriginalFileName,
                sidecars,
                progress,
                $"({i + 1}/{candidates.Count}) - Moved duplicate to the duplicate file directory",
                dryRun: dryRun,
                useCopy: false);
            sidecarsMoved += result.SidecarsMoved;
            errorCount += result.SidecarErrors;
        }

        return errorCount;
    }

    private static string ResolveDuplicateTargetDirectory(DuplicateMoveCandidate candidate, string duplicateFolderName, bool moveToSourceRoot, string? sourceRoot)
    {
        if (!moveToSourceRoot)
        {
            return candidate.TargetDirectory;
        }

        var root = string.IsNullOrWhiteSpace(sourceRoot) ? candidate.File.OriginalDirectory : sourceRoot;
        return Path.Combine(root, duplicateFolderName, candidate.File.Hash);
    }

    private void MarkAlreadyExistingForCopyMode(
        IReadOnlyList<FileInventoryResult> sourceFiles,
        IReadOnlyList<FileInventoryResult> targetFiles,
        IPictureSortJournal journal,
        IProgress<string> progress,
        DuplicateVerification duplicateVerification)
    {
        var verifier = AlreadyExistingVerifier.Build(targetFiles, journal);
        var targetHashes = new HashSet<string>(targetFiles.Select(f => f.Hash), StringComparer.OrdinalIgnoreCase);

        var marked = 0;
        foreach (var sourceFile in sourceFiles)
        {
            if (sourceFile.IsIgnored)
            {
                continue;
            }
            if (verifier.IsAlreadyInTarget(sourceFile, duplicateVerification))
            {
                sourceFile.SetIgnored(AlreadyExistingIgnoredReason);
                _events.Report(new AlreadyExistsInTargetEvent(sourceFile.FullPath, sourceFile.Hash, ViaJournal: !targetHashes.Contains(sourceFile.Hash)));
                marked++;
            }
        }

        if (marked > 0)
        {
            progress.Report($"OperationMode=Copy: skipped {marked} source files already present in target (or journal). Verification={duplicateVerification}.");
        }
    }

    private int CheckForExistingFilesInTarget(
        IReadOnlyList<FileInventoryResult> sourceDirectoryInventory,
        IReadOnlyList<FileInventoryResult> targetDirectoryInventory,
        string alreadyExistingFolderName,
        SidecarMatcher sidecarMatcher,
        ref int sidecarsMoved,
        IPictureSortJournal journal,
        IProgress<string> progress,
        bool dryRun,
        DuplicateVerification duplicateVerification,
        CancellationToken cancellationToken)
    {
        progress.Report($"Checking for files who already exists in the target. Verification={duplicateVerification}.");

        var verifier = AlreadyExistingVerifier.Build(targetDirectoryInventory, journal);
        var targetHashes = new HashSet<string>(targetDirectoryInventory.Select(f => f.Hash), StringComparer.OrdinalIgnoreCase);

        var filesAlreadyExistsInTarget = sourceDirectoryInventory
            .Where(a => !a.IsIgnored && verifier.IsAlreadyInTarget(a, duplicateVerification))
            .ToList();

        var errorCount = 0;
        if (filesAlreadyExistsInTarget.Count == 0)
        {
            return errorCount;
        }

        progress.Report($"We found {filesAlreadyExistsInTarget.Count} files who already exists in the target.");

        for (var i = 0; i < filesAlreadyExistsInTarget.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileInventoryResult = filesAlreadyExistsInTarget[i];
            _events.Report(new AlreadyExistsInTargetEvent(fileInventoryResult.FullPath, fileInventoryResult.Hash, ViaJournal: !targetHashes.Contains(fileInventoryResult.Hash)));

            try
            {
                var targetFullDirectoryPath = Path.Combine(fileInventoryResult.OriginalDirectory, alreadyExistingFolderName, fileInventoryResult.Hash);
                var sidecars = sidecarMatcher.Find(fileInventoryResult);
                var result = MoveFileAndSidecars(
                    fileInventoryResult.FullPath,
                    targetFullDirectoryPath,
                    fileInventoryResult.OriginalFileName,
                    sidecars,
                    progress,
                    $"({i + 1}/{filesAlreadyExistsInTarget.Count}) - Moved file to the already existing file directory",
                    dryRun: dryRun,
                    useCopy: false);
                sidecarsMoved += result.SidecarsMoved;
                errorCount += result.SidecarErrors;
                fileInventoryResult.SetIgnored(AlreadyExistingIgnoredReason);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errorCount++;
                progress.Report($"Failed to move existing-in-target file {fileInventoryResult.FullPath}: {ex.Message}.");
                _errors.Add(new PictureSortError(fileInventoryResult.FullPath, "MoveExistingInTargetFailed", ex.Message));
            }
        }

        return errorCount;
    }

    private async Task<(int ErrorCount, int SidecarsMoved, int FilesWithoutDateSkipped)> MovePicturesToTheTargetFolderAsync(
        IReadOnlyList<FileInventoryResult> sourceDirectoryInventory,
        string targetFolder,
        IProgress<string> progress,
        int maxConcurrency,
        SidecarMatcher sidecarMatcher,
        IPictureSortJournal journal,
        FolderStructure.FolderStructureTemplate folderTemplate,
        UnknownDatePolicy unknownDatePolicy,
        bool dryRun,
        OperationMode operationMode,
        CancellationToken cancellationToken)
    {
        if (sourceDirectoryInventory.Count == 0)
        {
            return (0, 0, 0);
        }

        var sourceList = sourceDirectoryInventory
            .Where(a => !a.IsIgnored)
            .ToList();

        if (sourceList.Count == 0)
        {
            return (0, 0, 0);
        }

        progress.Report($"Moving {sourceList.Count} files to the target directory with max concurrency {maxConcurrency}.");

        var errorCount = 0;
        var sidecarsMoved = 0;
        var filesWithoutDateSkipped = 0;
        var processedCount = 0;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxConcurrency,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(sourceList, parallelOptions, async (fileInventoryResult, ct) =>
        {
            await Task.Run(() =>
            {
                try
                {
                    var current = Interlocked.Increment(ref processedCount);
                    // "Strict" Unknown handling: when the user opts in to SkipAndCount/Fail we treat a
                    // missing EXIF date as "no date" even if filesystem timestamps would otherwise be a
                    // fallback. The default MoveToUnknownFolder policy keeps legacy semantics.
                    var hasReliableDate = fileInventoryResult.OriginalDate.HasValue;
                    if (!hasReliableDate && unknownDatePolicy != UnknownDatePolicy.MoveToUnknownFolder)
                    {
                        _events.Report(new FileWithoutDateEvent(fileInventoryResult.FullPath, unknownDatePolicy));
                        if (unknownDatePolicy == UnknownDatePolicy.SkipAndCount)
                        {
                            Interlocked.Increment(ref filesWithoutDateSkipped);
                            progress.Report($"({current}/{sourceList.Count}) - Skipped file without date (UnknownDatePolicy=SkipAndCount): {fileInventoryResult.FullPath}.");
                        }
                        else // Fail
                        {
                            Interlocked.Increment(ref errorCount);
                            progress.Report($"({current}/{sourceList.Count}) - Failed file without date (UnknownDatePolicy=Fail): {fileInventoryResult.FullPath}.");
                            _events.Report(new FileFailedEvent(fileInventoryResult.FullPath, "NoDate", "UnknownDatePolicy=Fail"));
                            _errors.Add(new PictureSortError(fileInventoryResult.FullPath, "NoDate", "UnknownDatePolicy=Fail"));
                        }
                        return;
                    }

                    var targetFullDirectoryPath = Path.Combine(targetFolder, folderTemplate.Build(fileInventoryResult));
                    var sidecars = sidecarMatcher.Find(fileInventoryResult);
                    var moveResult = MoveFileAndSidecars(
                        fileInventoryResult.FullPath,
                        targetFullDirectoryPath,
                        fileInventoryResult.OriginalFileName,
                        sidecars,
                        progress,
                        $"({current}/{sourceList.Count}) - {(dryRun ? "[DRY-RUN] " : string.Empty)}{(operationMode == OperationMode.Copy ? "Copied" : "Moved")} file to the target directory",
                        dryRun: dryRun,
                        useCopy: operationMode == OperationMode.Copy);
                    Interlocked.Add(ref sidecarsMoved, moveResult.SidecarsMoved);
                    Interlocked.Add(ref errorCount, moveResult.SidecarErrors);
                    if (!dryRun)
                    {
                        long finalLength = 0;
                        var finalLastWriteUtc = default(DateTime);
                        try
                        {
                            finalLength = _fileSystem.GetFileLength(moveResult.PrimaryFinalPath);
                            finalLastWriteUtc = _fileSystem.GetLastWriteTime(moveResult.PrimaryFinalPath);
                        }
                        catch (Exception statEx)
                        {
                            progress.Report($"Could not stat moved file {moveResult.PrimaryFinalPath} for journal: {statEx.Message}.");
                        }
                        journal.Append(new JournalEntry(fileInventoryResult.Hash, moveResult.PrimaryFinalPath, _clock.UtcNow, finalLength, finalLastWriteUtc));
                        _events.Report(new JournalAppendedEvent(fileInventoryResult.Hash, moveResult.PrimaryFinalPath));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Interlocked.Increment(ref errorCount);
                    progress.Report($"Failed to move file {fileInventoryResult.FullPath}: {ex.Message}.");
                    _events.Report(new FileFailedEvent(fileInventoryResult.FullPath, "MoveFailed", ex.Message));
                    _errors.Add(new PictureSortError(fileInventoryResult.FullPath, "MoveFailed", ex.Message));
                }

            }, ct);
        });

        return (errorCount, sidecarsMoved, filesWithoutDateSkipped);
    }

    private MoveFileAndSidecarsResult MoveFileAndSidecars(
        string sourceFullPath,
        string targetDirectory,
        string preferredFileName,
        IReadOnlyList<SidecarFile> sidecars,
        IProgress<string> progress,
        string progressPrefix,
        bool dryRun,
        bool useCopy)
    {
        if (!dryRun)
        {
            _fileSystem.CreateDirectory(targetDirectory);
        }

        var desiredPrimaryPath = Path.Combine(targetDirectory, preferredFileName);
        var primaryTransfer = TransferToAvailablePath(sourceFullPath, desiredPrimaryPath, progress, dryRun, useCopy);
        if (primaryTransfer.Renamed)
        {
            progress.Report($"NAME_COLLISION: '{desiredPrimaryPath}' already taken; renamed to '{primaryTransfer.FinalPath}'.");
            _events.Report(new NameCollisionEvent(desiredPrimaryPath, primaryTransfer.FinalPath));
        }
        progress.Report($"{progressPrefix}: {sourceFullPath} => {primaryTransfer.FinalPath}.");
        _events.Report(new FileMovedEvent(sourceFullPath, primaryTransfer.FinalPath, useCopy, dryRun));

        if (sidecars.Count == 0)
        {
            return new MoveFileAndSidecarsResult(primaryTransfer.FinalPath, SidecarsMoved: 0, SidecarErrors: 0);
        }

        var finalBaseName = Path.GetFileNameWithoutExtension(primaryTransfer.FinalPath);
        var sidecarsMoved = 0;
        var sidecarErrors = 0;
        foreach (var sidecar in sidecars)
        {
            try
            {
                var sidecarTargetFileName = BuildSidecarTargetFileName(
                    sidecar.FullPath,
                    preferredFileName,
                    finalBaseName);
                var desiredSidecarPath = Path.Combine(targetDirectory, sidecarTargetFileName);
                var sidecarTransfer = TransferToAvailablePath(sidecar.FullPath, desiredSidecarPath, progress, dryRun, useCopy);
                if (sidecarTransfer.Renamed)
                {
                    progress.Report($"NAME_COLLISION: '{desiredSidecarPath}' already taken; renamed to '{sidecarTransfer.FinalPath}'.");
                    _events.Report(new NameCollisionEvent(desiredSidecarPath, sidecarTransfer.FinalPath));
                }
                progress.Report($"{(dryRun ? "[DRY-RUN] " : string.Empty)}{(useCopy ? "Copied" : "Moved")} sidecar alongside primary: {sidecar.FullPath} => {sidecarTransfer.FinalPath}.");
                _events.Report(new SidecarMovedEvent(sidecar.FullPath, sidecarTransfer.FinalPath, useCopy, dryRun));
                sidecarsMoved++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                sidecarErrors++;
                progress.Report($"Failed to move sidecar {sidecar.FullPath}: {ex.Message}.");
                _events.Report(new FileFailedEvent(sidecar.FullPath, "SidecarMoveFailed", ex.Message));
                _errors.Add(new PictureSortError(sidecar.FullPath, "SidecarMoveFailed", ex.Message));
            }
        }

        return new MoveFileAndSidecarsResult(primaryTransfer.FinalPath, sidecarsMoved, sidecarErrors);
    }

    private TransferResult TransferToAvailablePath(string sourceFullPath, string desiredFullPath, IProgress<string> progress, bool dryRun, bool useCopy)
    {
        foreach (var candidate in desiredFullPath.EnumerateFileNameCandidates())
        {
            if (_fileSystem.FileExists(candidate))
            {
                continue;
            }

            if (dryRun)
            {
                return new TransferResult(candidate, Renamed: !string.Equals(candidate, desiredFullPath, StringComparison.OrdinalIgnoreCase));
            }

            var transferred = TrySafeTransfer(sourceFullPath, candidate, progress, useCopy);
            if (transferred)
            {
                return new TransferResult(candidate, Renamed: !string.Equals(candidate, desiredFullPath, StringComparison.OrdinalIgnoreCase));
            }
        }

        throw new IOException($"Could not find an available file name for '{desiredFullPath}' after {UniqueFileNameExtension.DefaultMaxCollisionAttempts} attempts.");
    }

    /// <summary>
    /// Attempts to transfer (move or copy) a file to one concrete destination path.
    /// For moves: same-volume uses atomic <see cref="IFileSystem.TryMove"/>; cross-volume
    /// copies to a <c>.partial</c> sibling, fsyncs, renames, then deletes the source — so a
    /// crash mid-transfer never leaves a half-written file at the destination path.
    /// For copies: always goes through the <c>.partial</c> + rename pattern.
    /// </summary>
    private bool TrySafeTransfer(string sourceFullPath, string destinationFullPath, IProgress<string> progress, bool useCopy)
    {
        if (!useCopy)
        {
            var sourceRoot = _fileSystem.GetPathRoot(Path.GetFullPath(sourceFullPath));
            var destRoot = _fileSystem.GetPathRoot(Path.GetFullPath(destinationFullPath));
            var sameVolume = string.Equals(sourceRoot, destRoot, StringComparison.OrdinalIgnoreCase);

            if (sameVolume)
            {
                return _fileSystem.TryMove(sourceFullPath, destinationFullPath);
            }
        }

        var partialPath = destinationFullPath + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            using (var src = _fileSystem.OpenRead(sourceFullPath))
            using (var dst = _fileSystem.OpenWrite(partialPath))
            {
                src.CopyTo(dst);
                if (dst is FileStream fs)
                {
                    fs.Flush(flushToDisk: true);
                }
                else
                {
                    dst.Flush();
                }
            }

            if (!_fileSystem.TryMove(partialPath, destinationFullPath))
            {
                DeletePartialBestEffort(partialPath, progress);
                return false;
            }

            if (!useCopy)
            {
                _fileSystem.Delete(sourceFullPath);
            }

            return true;
        }
        catch
        {
            DeletePartialBestEffort(partialPath, progress);
            throw;
        }
    }

    private void DeletePartialBestEffort(string partialPath, IProgress<string> progress)
    {
        if (!_fileSystem.FileExists(partialPath))
        {
            return;
        }

        try
        {
            _fileSystem.Delete(partialPath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            progress.Report($"Failed to delete partial transfer file {partialPath}: {ex.Message}.");
        }
    }

    private static string BuildSidecarTargetFileName(
        string sidecarFullPath,
        string originalPrimaryFileName,
        string finalPrimaryBaseName)
    {
        var sidecarFileName = Path.GetFileName(sidecarFullPath);
        var sidecarBaseName = Path.GetFileNameWithoutExtension(sidecarFileName);
        var sidecarExtension = Path.GetExtension(sidecarFileName);

        // Pattern 1: "IMG.jpg.xmp" — sidecarBaseName == originalPrimaryFileName ("IMG.jpg")
        if (sidecarBaseName.Equals(originalPrimaryFileName, StringComparison.Ordinal))
        {
            var primaryExt = Path.GetExtension(originalPrimaryFileName);
            return finalPrimaryBaseName + primaryExt + sidecarExtension;
        }

        // Pattern 2: "IMG.xmp" — sidecarBaseName matches primary's base name
        return finalPrimaryBaseName + sidecarExtension;
    }

    private void ReportOrphanSidecars(IReadOnlyList<SidecarFile> orphans, IProgress<string> progress)
    {
        if (orphans.Count == 0)
        {
            return;
        }

        progress.Report($"Found {orphans.Count} sidecar files without a matching primary.");
        foreach (var orphan in orphans)
        {
            progress.Report($"Orphan sidecar (no primary found, left in place): {orphan.FullPath}.");
            _events.Report(new OrphanSidecarEvent(orphan.FullPath));
        }
    }

    private void DeleteEmptyDirectory(string startDirectory)
    {
        foreach (var currentDirectory in _fileSystem.EnumerateDirectories(startDirectory).ToList())
        {
            DeleteEmptyDirectory(currentDirectory);
            if (!_fileSystem.EnumerateFiles(currentDirectory, "*", SearchOption.TopDirectoryOnly).Any() &&
                !_fileSystem.EnumerateDirectories(currentDirectory).Any())
            {
                _fileSystem.DeleteEmptyDirectory(currentDirectory);
            }
        }
    }
}
