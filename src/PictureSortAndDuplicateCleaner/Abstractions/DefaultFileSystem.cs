namespace PictureSortAndDuplicateCleaner.Abstractions;

/// <summary>
/// Default implementation that forwards to <see cref="System.IO.File"/> and
/// <see cref="System.IO.Directory"/>. Used by production code; tests can replace with a fake.
/// </summary>
public sealed class DefaultFileSystem : IFileSystem
{
    public static readonly DefaultFileSystem Instance = new();

    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public void Move(string source, string destination) => File.Move(source, destination);
    public bool TryMove(string source, string destination)
    {
        try
        {
            File.Move(source, destination);
            return true;
        }
        catch (IOException) when (File.Exists(destination))
        {
            return false;
        }
    }
    public void Copy(string source, string destination, bool overwrite) => File.Copy(source, destination, overwrite);
    public void Delete(string path) => File.Delete(path);
    public void DeleteEmptyDirectory(string path) => Directory.Delete(path, recursive: false);
    public Stream OpenRead(string path) => File.OpenRead(path);
    public Stream OpenWrite(string path) => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    public IReadOnlyList<string> ReadAllLines(string path) => File.ReadAllLines(path);
    public void WriteAllText(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllText(path, content);
    }
    public void AppendAllText(string path, string content)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.AppendAllText(path, content);
    }
    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, SearchOption searchOption)
        => Directory.EnumerateFiles(directory, searchPattern, searchOption);
    public IEnumerable<string> EnumerateDirectories(string directory) => Directory.EnumerateDirectories(directory);
    public string? GetPathRoot(string path) => Path.GetPathRoot(path);
    public DateTime GetCreationTime(string path) => File.GetCreationTime(path);
    public DateTime GetLastWriteTime(string path) => File.GetLastWriteTime(path);
    public DateTime GetLastAccessTime(string path) => File.GetLastAccessTime(path);
    public long GetFileLength(string path) => new FileInfo(path).Length;
}
