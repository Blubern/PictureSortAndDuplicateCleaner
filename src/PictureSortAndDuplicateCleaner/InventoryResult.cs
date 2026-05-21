using PictureSortAndDuplicateCleaner.Sidecars;

namespace PictureSortAndDuplicateCleaner;

public sealed class InventoryResult
{
    public static readonly InventoryResult Empty = new(
        new List<FileInventoryResult>().AsReadOnly(),
        new List<SidecarFile>().AsReadOnly());

    public InventoryResult(IReadOnlyList<FileInventoryResult> files, IReadOnlyList<SidecarFile> sidecars)
    {
        Files = files;
        Sidecars = sidecars;
    }

    public IReadOnlyList<FileInventoryResult> Files { get; }

    public IReadOnlyList<SidecarFile> Sidecars { get; }
}
