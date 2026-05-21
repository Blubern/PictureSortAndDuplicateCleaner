using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;
using PictureSortAndDuplicateCleaner.Events;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSorterInMemoryTests
{
    [Fact]
    public async Task EndToEnd_MovesFileFromSourceToTarget_UsingInMemoryFileSystem()
    {
        var fs = new InMemoryFileSystem();
        var sourceDir = Path.Combine(Path.GetTempPath(), "ps-src");
        var targetDir = Path.Combine(Path.GetTempPath(), "ps-dst");
        var sourceFile = Path.Combine(sourceDir, "photo.jpg");
        fs.AddFile(sourceFile, "image-bytes", lastWriteUtc: new DateTime(2023, 7, 15, 12, 0, 0, DateTimeKind.Utc));
        fs.CreateDirectory(targetDir);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs, new FixedClock(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        var parameter = new PictureSortParameter(
            new[] { sourceDir },
            targetDir,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(1, result.SourceFilesFound);
        Assert.Equal(1, result.FilesMovedToTarget);
        Assert.False(fs.FileExists(sourceFile));
        // Target file should land under target / Unknown (no EXIF) — verify it exists somewhere under targetDir.
        var moved = fs.EnumerateFiles(targetDir, "*", SearchOption.AllDirectories).ToList();
        Assert.Single(moved);
        Assert.EndsWith("photo.jpg", moved[0]);
    }

    [Fact]
    public async Task EndToEnd_CleansEmptySourceSubdirectories_UsingInMemoryFileSystem()
    {
        var fs = new InMemoryFileSystem();
        var sourceDir = Path.Combine(Path.GetTempPath(), "ps-src-clean");
        var targetDir = Path.Combine(Path.GetTempPath(), "ps-dst-clean");
        var sourceFile = Path.Combine(sourceDir, "sub", "deep", "photo.jpg");
        fs.AddFile(sourceFile, "x", lastWriteUtc: new DateTime(2023, 7, 15, 12, 0, 0, DateTimeKind.Utc));
        fs.CreateDirectory(targetDir);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDir },
            targetDir,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);

        await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.False(fs.DirectoryExists(Path.Combine(sourceDir, "sub", "deep")));
        Assert.False(fs.DirectoryExists(Path.Combine(sourceDir, "sub")));
    }

    [Fact]
    public async Task EndToEnd_RenamesOnNameCollision_UsingInMemoryFileSystem()
    {
        var fs = new InMemoryFileSystem();
        var sourceDir = Path.Combine(Path.GetTempPath(), "ps-src-coll");
        var targetDir = Path.Combine(Path.GetTempPath(), "ps-dst-coll");
        // Same filename, different content (=> different hash, not a duplicate) in two source subdirectories.
        fs.AddFile(Path.Combine(sourceDir, "a", "photo.jpg"), "one", lastWriteUtc: new DateTime(2023, 7, 15, 12, 0, 0, DateTimeKind.Utc));
        fs.AddFile(Path.Combine(sourceDir, "b", "photo.jpg"), "two", lastWriteUtc: new DateTime(2023, 7, 15, 12, 0, 0, DateTimeKind.Utc));
        fs.CreateDirectory(targetDir);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDir },
            targetDir,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);
        var captured = new List<PictureSortEvent>();
        var sink = new Progress<PictureSortEvent>(e => { lock (captured) { captured.Add(e); } });

        await sorter.StartPictureSortAsync(parameter, new TestProgress(), sink, CancellationToken.None);
        await Task.Delay(50);

        var moved = fs.EnumerateFiles(targetDir, "*", SearchOption.AllDirectories).ToList();
        Assert.Equal(2, moved.Count);
        lock (captured)
        {
            Assert.NotEmpty(captured.OfType<NameCollisionEvent>());
        }
    }

    [Fact]
    public async Task EndToEnd_WithParallelMoves_DoesNotSerializeFinalTransfers()
    {
        var inner = new InMemoryFileSystem();
        var fs = new CountingMoveFileSystem(inner, moveDelay: TimeSpan.FromMilliseconds(75));
        var sourceDir = Path.Combine(Path.GetTempPath(), "ps-src-parallel");
        var targetDir = Path.Combine(Path.GetTempPath(), "ps-dst-parallel");
        var takenTime = new DateTime(2023, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 8; i++)
        {
            inner.AddFile(Path.Combine(sourceDir, $"photo-{i}.jpg"), "image-" + i, lastWriteUtc: takenTime);
        }
        inner.CreateDirectory(targetDir);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDir },
            targetDir,
            4,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(0, result.ErrorCount);
        Assert.True(fs.MaxConcurrentMoves > 1, $"Expected concurrent final transfers, observed {fs.MaxConcurrentMoves}.");
    }

    [Fact]
    public async Task EndToEnd_WhenDestinationAppearsDuringMove_RetriesNextCandidate()
    {
        var inner = new InMemoryFileSystem();
        var fs = new FirstMoveConflictFileSystem(inner);
        var sourceDir = Path.Combine(Path.GetTempPath(), "ps-src-race");
        var targetDir = Path.Combine(Path.GetTempPath(), "ps-dst-race");
        var sourceFile = Path.Combine(sourceDir, "photo.jpg");
        var takenTime = new DateTime(2023, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        inner.AddFile(sourceFile, "image-bytes", lastWriteUtc: takenTime);
        inner.CreateDirectory(targetDir);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDir },
            targetDir,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(0, result.ErrorCount);
        var moved = inner.EnumerateFiles(targetDir, "*.jpg", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .OrderBy(name => name)
            .ToArray();
        Assert.Contains("photo.jpg", moved);
        Assert.Contains("photo_0.jpg", moved);
        Assert.False(inner.FileExists(sourceFile));
    }

    [Fact]
    public async Task EndToEnd_WhenPartialCleanupFails_ReportsProgressAndRetries()
    {
        var inner = new InMemoryFileSystem();
        var fs = new PartialCleanupFailureFileSystem(inner);
        var sourceDir = Path.Combine(Path.GetTempPath(), "ps-src-partial-cleanup");
        var targetDir = Path.Combine(Path.GetTempPath(), "ps-dst-partial-cleanup");
        var sourceFile = Path.Combine(sourceDir, "photo.jpg");
        var takenTime = new DateTime(2023, 7, 15, 12, 0, 0, DateTimeKind.Utc);
        inner.AddFile(sourceFile, "image-bytes", lastWriteUtc: takenTime);
        inner.CreateDirectory(targetDir);
        var progress = new TestProgress();

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDir },
            targetDir,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            operationMode: OperationMode.Copy);

        var result = await sorter.StartPictureSortAsync(parameter, progress, CancellationToken.None);

        Assert.Equal(0, result.ErrorCount);
        Assert.True(inner.FileExists(sourceFile));
        Assert.Contains(progress.Messages, message => message.Contains("Failed to delete partial transfer file", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CountingMoveFileSystem : DelegatingFileSystem
    {
        private readonly TimeSpan _moveDelay;
        private int _activeMoves;
        private int _maxConcurrentMoves;

        public CountingMoveFileSystem(InMemoryFileSystem inner, TimeSpan moveDelay)
            : base(inner)
        {
            _moveDelay = moveDelay;
        }

        public int MaxConcurrentMoves => _maxConcurrentMoves;

        public override bool TryMove(string source, string destination)
        {
            var active = Interlocked.Increment(ref _activeMoves);
            try
            {
                int observed;
                do
                {
                    observed = _maxConcurrentMoves;
                    if (active <= observed)
                    {
                        break;
                    }
                }
                while (Interlocked.CompareExchange(ref _maxConcurrentMoves, active, observed) != observed);

                Thread.Sleep(_moveDelay);
                return base.TryMove(source, destination);
            }
            finally
            {
                Interlocked.Decrement(ref _activeMoves);
            }
        }
    }

    private sealed class FirstMoveConflictFileSystem : DelegatingFileSystem
    {
        private int _injected;

        public FirstMoveConflictFileSystem(InMemoryFileSystem inner)
            : base(inner)
        {
        }

        public override bool TryMove(string source, string destination)
        {
            if (Interlocked.Exchange(ref _injected, 1) == 0)
            {
                Inner.AddFile(destination, "late collision");
                return false;
            }

            return base.TryMove(source, destination);
        }
    }

    private sealed class PartialCleanupFailureFileSystem : DelegatingFileSystem
    {
        private int _injected;

        public PartialCleanupFailureFileSystem(InMemoryFileSystem inner)
            : base(inner)
        {
        }

        public override bool TryMove(string source, string destination)
        {
            if (source.EndsWith(".partial", StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref _injected, 1) == 0)
            {
                Inner.AddFile(destination, "late collision");
                return false;
            }

            return base.TryMove(source, destination);
        }

        public override void Delete(string path)
        {
            if (path.EndsWith(".partial", StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Injected partial cleanup failure.");
            }

            base.Delete(path);
        }
    }

    private abstract class DelegatingFileSystem : IFileSystem
    {
        protected DelegatingFileSystem(InMemoryFileSystem inner)
        {
            Inner = inner;
        }

        protected InMemoryFileSystem Inner { get; }

        public bool FileExists(string path) => Inner.FileExists(path);
        public bool DirectoryExists(string path) => Inner.DirectoryExists(path);
        public void CreateDirectory(string path) => Inner.CreateDirectory(path);
        public void Move(string source, string destination) => Inner.Move(source, destination);
        public virtual bool TryMove(string source, string destination) => Inner.TryMove(source, destination);
        public void Copy(string source, string destination, bool overwrite) => Inner.Copy(source, destination, overwrite);
        public virtual void Delete(string path) => Inner.Delete(path);
        public void DeleteEmptyDirectory(string path) => Inner.DeleteEmptyDirectory(path);
        public Stream OpenRead(string path) => Inner.OpenRead(path);
        public Stream OpenWrite(string path) => Inner.OpenWrite(path);
        public IReadOnlyList<string> ReadAllLines(string path) => Inner.ReadAllLines(path);
        public void WriteAllText(string path, string content) => Inner.WriteAllText(path, content);
        public void AppendAllText(string path, string content) => Inner.AppendAllText(path, content);
        public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, SearchOption searchOption) => Inner.EnumerateFiles(directory, searchPattern, searchOption);
        public IEnumerable<string> EnumerateDirectories(string directory) => Inner.EnumerateDirectories(directory);
        public string? GetPathRoot(string path) => Inner.GetPathRoot(path);
        public DateTime GetCreationTime(string path) => Inner.GetCreationTime(path);
        public DateTime GetLastWriteTime(string path) => Inner.GetLastWriteTime(path);
        public DateTime GetLastAccessTime(string path) => Inner.GetLastAccessTime(path);
        public long GetFileLength(string path) => Inner.GetFileLength(path);
    }
}
