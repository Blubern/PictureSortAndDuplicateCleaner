using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSortResultErrorsTests
{
    [Fact]
    public async Task UnknownDatePolicy_Fail_RecordsErrorEntryInResult()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var sourceFile = sourceDirectory.CreateFile("nodate.bin", "raw bytes without exif");

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            unknownDatePolicy: UnknownDatePolicy.Fail);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(1, result.ErrorCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(sourceFile, error.SourcePath);
        Assert.Equal("NoDate", error.Reason);
        Assert.Contains("UnknownDatePolicy=Fail", error.Message);
    }

    [Fact]
    public async Task SuccessfulRun_HasEmptyErrorsList()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var file = sourceDirectory.CreateFile("photo.jpg", "image bytes");
        fs.SetLastWriteTime(file, new DateTime(2024, 1, 1));

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(0, result.ErrorCount);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task SidecarMoveFailure_RecordsErrorEntryInResult()
    {
        var inner = new InMemoryFileSystem();
        var fs = new ThrowOnSidecarMoveFileSystem(inner);
        var sourceDirectory = new InMemoryDirectory(inner);
        var targetDirectory = new InMemoryDirectory(inner);
        var primary = sourceDirectory.CreateFile("photo.jpg", "image bytes");
        var sidecar = sourceDirectory.CreateFile("photo.xmp", "sidecar bytes");
        inner.SetLastWriteTime(primary, new DateTime(2024, 1, 1));
        inner.SetLastWriteTime(sidecar, new DateTime(2024, 1, 1));

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: new[] { ".xmp" });

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(0, result.SidecarsMoved);
        var error = Assert.Single(result.Errors);
        Assert.Equal(sidecar, error.SourcePath);
        Assert.Equal("SidecarMoveFailed", error.Reason);
        Assert.True(inner.FileExists(sidecar), "Failed sidecar should remain in source.");
    }

    private sealed class ThrowOnSidecarMoveFileSystem : IFileSystem
    {
        private readonly InMemoryFileSystem _inner;

        public ThrowOnSidecarMoveFileSystem(InMemoryFileSystem inner)
        {
            _inner = inner;
        }

        public bool FileExists(string path) => _inner.FileExists(path);
        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
        public void CreateDirectory(string path) => _inner.CreateDirectory(path);
        public bool TryMove(string source, string destination)
        {
            if (Path.GetExtension(source).Equals(".xmp", StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Injected sidecar move failure.");
            }

            return _inner.TryMove(source, destination);
        }
        public void Copy(string source, string destination, bool overwrite) => _inner.Copy(source, destination, overwrite);
        public void Delete(string path) => _inner.Delete(path);
        public void DeleteEmptyDirectory(string path) => _inner.DeleteEmptyDirectory(path);
        public Stream OpenRead(string path) => _inner.OpenRead(path);
        public Stream OpenWrite(string path) => _inner.OpenWrite(path);
        public IReadOnlyList<string> ReadAllLines(string path) => _inner.ReadAllLines(path);
        public void WriteAllText(string path, string content) => _inner.WriteAllText(path, content);
        public void AppendAllText(string path, string content) => _inner.AppendAllText(path, content);
        public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, SearchOption searchOption) => _inner.EnumerateFiles(directory, searchPattern, searchOption);
        public IEnumerable<string> EnumerateDirectories(string directory) => _inner.EnumerateDirectories(directory);
        public string? GetPathRoot(string path) => _inner.GetPathRoot(path);
        public DateTime GetCreationTime(string path) => _inner.GetCreationTime(path);
        public DateTime GetLastWriteTime(string path) => _inner.GetLastWriteTime(path);
        public DateTime GetLastAccessTime(string path) => _inner.GetLastAccessTime(path);
        public long GetFileLength(string path) => _inner.GetFileLength(path);

        public void Move(string source, string destination)
        {
            if (Path.GetExtension(source).Equals(".xmp", StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Injected sidecar move failure.");
            }

            _inner.Move(source, destination);
        }
    }
}
