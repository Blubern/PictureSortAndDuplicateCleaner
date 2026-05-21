namespace PictureSortAndDuplicateCleaner.Abstractions;

/// <summary>
/// Thin abstraction over the filesystem operations used by the sorter so tests can swap
/// in an in-memory or fault-injecting implementation without touching the disk.
/// </summary>
public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void Move(string source, string destination);
    /// <summary>
    /// Atomically moves <paramref name="source"/> to <paramref name="destination"/> when the
    /// destination path is free. Returns <c>false</c> only when the destination already exists;
    /// other I/O failures should still be thrown so callers can distinguish collisions from errors.
    /// </summary>
    bool TryMove(string source, string destination);
    void Copy(string source, string destination, bool overwrite);
    void Delete(string path);
    /// <summary>
    /// Deletes an empty directory. Implementations should throw <see cref="IOException"/>
    /// (or equivalent) if the directory still contains files or subdirectories.
    /// </summary>
    void DeleteEmptyDirectory(string path);
    Stream OpenRead(string path);
    Stream OpenWrite(string path);
    /// <summary>Reads all UTF-8 lines from a file. Throws if the file does not exist.</summary>
    IReadOnlyList<string> ReadAllLines(string path);
    /// <summary>Writes UTF-8 <paramref name="content"/>, overwriting any existing file. Creates parent directories as needed.</summary>
    void WriteAllText(string path, string content);
    /// <summary>Appends UTF-8 <paramref name="content"/> to <paramref name="path"/>, creating the file (and parent directories) if missing.</summary>
    void AppendAllText(string path, string content);
    IEnumerable<string> EnumerateFiles(string directory, string searchPattern, SearchOption searchOption);
    IEnumerable<string> EnumerateDirectories(string directory);
    string? GetPathRoot(string path);
    DateTime GetCreationTime(string path);
    DateTime GetLastWriteTime(string path);
    DateTime GetLastAccessTime(string path);
    long GetFileLength(string path);
}
