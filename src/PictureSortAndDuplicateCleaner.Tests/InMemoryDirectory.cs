using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

/// <summary>
/// In-memory test fixture that mimics <see cref="TemporaryDirectory"/>'s API so existing tests
/// can be migrated mechanically. Backed by a shared <see cref="InMemoryFileSystem"/>; multiple
/// instances should be created from the same <see cref="InMemoryFileSystem"/> to share state
/// (typical pattern: one fs, one source dir, one target dir).
/// </summary>
internal sealed class InMemoryDirectory
{
    public InMemoryFileSystem FileSystem { get; }
    public string Path { get; }

    public InMemoryDirectory(InMemoryFileSystem fileSystem)
    {
        FileSystem = fileSystem;
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "imd-" + Guid.NewGuid().ToString("N"));
        FileSystem.CreateDirectory(Path);
    }

    public string CreateFile(string relativePath, string content = "test content", DateTime? lastWriteUtc = null)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        FileSystem.AddFile(fullPath, content, lastWriteUtc);
        return fullPath;
    }
}
