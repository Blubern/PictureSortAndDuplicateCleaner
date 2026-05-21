using PictureSortAndDuplicateCleaner.Journal;

namespace PictureSortAndDuplicateCleaner;

/// <summary>
/// Helper used by <see cref="PictureSorter"/> to decide whether a source file is
/// already present in the target directory (or has been written there previously
/// according to the journal). Encapsulates the <see cref="DuplicateVerification"/>
/// policy so the call sites stay readable.
/// </summary>
/// <remarks>
/// HashPlusSize semantics:
/// <list type="bullet">
///   <item>Target lengths per hash are collected from the inventoried target files.</item>
///   <item>Journal entries have no length info → their hashes are recorded with a
///         sentinel length of 0 meaning "unknown".</item>
///   <item>A source file is considered "already in target" if the hash matches AND
///         (target has at least one length for that hash equal to the source length
///         OR either side reports length 0). The unknown-length fallback keeps the
///         behaviour backward compatible for journals/inventories without size info
///         and prevents false negatives that would re-import the file.</item>
/// </list>
/// </remarks>
internal sealed class AlreadyExistingVerifier
{
    private readonly Dictionary<string, HashSet<long>> _lengthsByHash;

    private AlreadyExistingVerifier(Dictionary<string, HashSet<long>> lengthsByHash)
    {
        _lengthsByHash = lengthsByHash;
    }

    public static AlreadyExistingVerifier Build(
        IReadOnlyList<FileInventoryResult> targetFiles,
        IPictureSortJournal journal)
    {
        var map = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in targetFiles)
        {
            if (file.IsIgnored)
            {
                continue;
            }
            AddLength(map, file.Hash, file.Length);
        }

        foreach (var journalHash in journal.KnownHashes)
        {
            // Journal entries don't carry length info → sentinel 0 ("unknown").
            AddLength(map, journalHash, 0);
        }

        return new AlreadyExistingVerifier(map);
    }

    public bool IsAlreadyInTarget(FileInventoryResult sourceFile, DuplicateVerification verification)
    {
        if (!_lengthsByHash.TryGetValue(sourceFile.Hash, out var lengths))
        {
            return false;
        }

        if (verification != DuplicateVerification.HashPlusSize)
        {
            return true;
        }

        // HashPlusSize: confirm length matches. Length 0 on either side = unknown → fall back
        // to hash-only behavior for that comparison to preserve backward compatibility.
        if (sourceFile.Length <= 0)
        {
            return true;
        }

        foreach (var len in lengths)
        {
            if (len <= 0 || len == sourceFile.Length)
            {
                return true;
            }
        }
        return false;
    }

    private static void AddLength(Dictionary<string, HashSet<long>> map, string hash, long length)
    {
        if (string.IsNullOrEmpty(hash))
        {
            return;
        }
        if (!map.TryGetValue(hash, out var set))
        {
            set = new HashSet<long>();
            map[hash] = set;
        }
        set.Add(length);
    }
}
