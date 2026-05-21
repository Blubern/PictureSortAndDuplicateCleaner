namespace PictureSortAndDuplicateCleaner;

/// <summary>
/// Lightweight <see cref="IGrouping{TKey,TElement}"/> used by
/// <see cref="DuplicateFilesInDirectoryExtension"/> when splitting a hash-based duplicate
/// group into per-length subgroups under <see cref="DuplicateVerification.HashPlusSize"/>.
/// </summary>
internal sealed class HashLengthGrouping : IGrouping<string, FileInventoryResult>
{
    private readonly IEnumerable<FileInventoryResult> _items;

    public HashLengthGrouping(string key, IEnumerable<FileInventoryResult> items)
    {
        Key = key;
        _items = items;
    }

    public string Key { get; }
    public IEnumerator<FileInventoryResult> GetEnumerator() => _items.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
