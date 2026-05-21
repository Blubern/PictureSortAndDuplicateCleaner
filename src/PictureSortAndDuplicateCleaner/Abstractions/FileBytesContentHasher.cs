using System.IO.Hashing;

namespace PictureSortAndDuplicateCleaner.Abstractions;

/// <summary>
/// Default content hasher: hashes the raw file bytes with XxHash3 and returns
/// the lower-case hex string. This is the historical PictureSortAndDuplicateCleaner behavior.
/// </summary>
public sealed class FileBytesContentHasher : IContentHasher
{
    private readonly IFileSystem _fileSystem;

    public FileBytesContentHasher(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
    {
        await using var fs = _fileSystem.OpenRead(filePath);
        var hasher = new XxHash3();
        var buffer = new byte[81920];
        int read;
        while ((read = await fs.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            hasher.Append(buffer.AsSpan(0, read));
        }
        return Convert.ToHexString(hasher.GetCurrentHash()).ToLowerInvariant();
    }
}
