namespace PictureSortAndDuplicateCleaner.Abstractions;

/// <summary>
/// Computes a content fingerprint for a file. Different implementations may hash
/// raw bytes or decoded pixel data. The returned string must be stable for a given
/// hasher implementation and is treated as the duplicate identity by the sorter.
/// </summary>
public interface IContentHasher
{
    Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken);
}
