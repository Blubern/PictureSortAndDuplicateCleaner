namespace PictureSortAndDuplicateCleaner.Abstractions;

/// <summary>
/// Mutable snapshot of an in-memory file used by <see cref="InMemoryFileSystem"/>.
/// Kept internal because the byte[] is shared by reference; consumers must not mutate it.
/// </summary>
internal sealed record InMemoryFileEntry(byte[] Content, DateTime CreationTime, DateTime LastWriteTime, DateTime LastAccessTime);
