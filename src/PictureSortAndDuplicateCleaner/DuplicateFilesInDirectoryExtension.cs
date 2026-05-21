namespace PictureSortAndDuplicateCleaner;

using System.Linq;

public static class DuplicateFilesInDirectoryExtension
{
    public static IReadOnlyList<DuplicateMoveCandidate> MarkDuplicatesAndCollectMoveCandidates(
        this IReadOnlyList<FileInventoryResult> inventoryResult,
        IProgress<string> progress,
        string duplicateFolderName,
        CancellationToken cancellationToken,
        DuplicateVerification duplicateVerification = DuplicateVerification.HashOnly)
    {
        progress.Report("Check for duplicated files.");

        // Hash groups are the first cut. When HashPlusSize is requested we additionally split
        // each hash group by file length so files with identical hash but different sizes are
        // NOT treated as duplicates (defensive against rare hash collisions / truncated files).
        var hashGroups = inventoryResult
            .GroupBy(a => a.Hash)
            .Where(a => a.Count() > 1)
            .ToList();

        var duplicateGroups = duplicateVerification == DuplicateVerification.HashPlusSize
            ? SplitHashGroupsByLength(hashGroups, progress).ToList()
            : hashGroups.Cast<IGrouping<string, FileInventoryResult>>().ToList();

        var candidates = new List<DuplicateMoveCandidate>();
        if (duplicateGroups.Count == 0)
        {
            return candidates.AsReadOnly();
        }

        progress.Report("We found duplicated Files.");

        for (var id = 0; id < duplicateGroups.Count; id++)
        {
            var group = duplicateGroups[id];
            var groupList = group.ToList();
            cancellationToken.ThrowIfCancellationRequested();

            for (var i = 0; i < groupList.Count; i++)
            {
                var duplicateFile = groupList[i];
                progress.Report($"{id + 1}/{duplicateGroups.Count} - {group.Key} ({groupList.Count}) - {duplicateFile}");

                if (i == 0)
                {
                    continue;
                }

                var ignoreMessage = $"This file is a duplicate file - Duplicate Files are: {string.Join(",", groupList.Select(a => a.FullPath))}.";
                duplicateFile.SetIgnored(ignoreMessage);
                progress.Report(ignoreMessage);

                var targetDirectory = Path.Combine(duplicateFile.OriginalDirectory, duplicateFolderName, group.Key);
                candidates.Add(new DuplicateMoveCandidate(duplicateFile, targetDirectory));
            }
        }

        return candidates.AsReadOnly();
    }

    private static IEnumerable<IGrouping<string, FileInventoryResult>> SplitHashGroupsByLength(
        IEnumerable<IGrouping<string, FileInventoryResult>> hashGroups,
        IProgress<string> progress)
    {
        foreach (var hashGroup in hashGroups)
        {
            // If any file in the group lacks length info (Length == 0) we cannot safely split
            // by length without risking false-negatives → keep the original hash-only group.
            if (hashGroup.Any(a => a.Length <= 0))
            {
                yield return hashGroup;
                continue;
            }

            var lengthSubgroups = hashGroup.GroupBy(a => a.Length).ToList();
            if (lengthSubgroups.Count > 1)
            {
                progress.Report($"Hash {hashGroup.Key}: split into {lengthSubgroups.Count} length-subgroups (HashPlusSize verification).");
            }

            foreach (var lengthGroup in lengthSubgroups.Where(g => g.Count() > 1))
            {
                yield return new HashLengthGrouping(hashGroup.Key, lengthGroup);
            }
        }
    }
}