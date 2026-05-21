namespace PictureSortAndDuplicateCleaner.Sidecars;

internal sealed class SidecarMatcher
{
    private readonly Dictionary<(string Directory, string Key), List<SidecarFile>> _byKey;
    private readonly List<SidecarFile> _orphans;

    public SidecarMatcher(IReadOnlyList<SidecarFile> sidecars, IReadOnlyList<FileInventoryResult> primaries)
    {
        _byKey = new Dictionary<(string, string), List<SidecarFile>>();

        // For sidecar "IMG.jpg.xmp" the key becomes "IMG.jpg" (Path.GetFileNameWithoutExtension
        // strips only the last extension) — matches against a primary by full file name.
        // For sidecar "IMG.xmp" the key becomes "IMG" — matches against a primary's base name.
        foreach (var sidecar in sidecars)
        {
            var sidecarFile = Path.GetFileName(sidecar.FullPath);
            var key = (sidecar.OriginalDirectory, Path.GetFileNameWithoutExtension(sidecarFile));
            if (!_byKey.TryGetValue(key, out var list))
            {
                list = new List<SidecarFile>();
                _byKey[key] = list;
            }
            list.Add(sidecar);
        }

        var primaryKeys = new HashSet<(string, string)>();
        foreach (var primary in primaries)
        {
            primaryKeys.Add((primary.OriginalDirectory, primary.OriginalFileName));
            var baseName = Path.GetFileNameWithoutExtension(primary.OriginalFileName);
            if (!baseName.Equals(primary.OriginalFileName, StringComparison.Ordinal))
            {
                primaryKeys.Add((primary.OriginalDirectory, baseName));
            }
        }

        _orphans = new List<SidecarFile>();
        foreach (var sidecar in sidecars)
        {
            var sidecarFile = Path.GetFileName(sidecar.FullPath);
            var key = (sidecar.OriginalDirectory, Path.GetFileNameWithoutExtension(sidecarFile));
            if (!primaryKeys.Contains(key))
            {
                _orphans.Add(sidecar);
            }
        }
    }

    public IReadOnlyList<SidecarFile> Find(FileInventoryResult primary)
    {
        var directory = primary.OriginalDirectory;
        var primaryFullName = primary.OriginalFileName;
        var primaryBase = Path.GetFileNameWithoutExtension(primaryFullName);

        var found = new List<SidecarFile>();
        if (_byKey.TryGetValue((directory, primaryFullName), out var fullNameMatches))
        {
            found.AddRange(fullNameMatches);
        }

        if (!primaryBase.Equals(primaryFullName, StringComparison.Ordinal)
            && _byKey.TryGetValue((directory, primaryBase), out var baseNameMatches))
        {
            found.AddRange(baseNameMatches);
        }

        return found;
    }

    public IReadOnlyList<SidecarFile> Orphans => _orphans;
}


