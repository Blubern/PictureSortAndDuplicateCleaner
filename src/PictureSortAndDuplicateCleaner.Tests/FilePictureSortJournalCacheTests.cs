using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;
using PictureSortAndDuplicateCleaner.Journal;

namespace PictureSortAndDuplicateCleaner.Tests;

/// <summary>
/// Covers the journal's hash-cache behavior: reusing hashes for unchanged target
/// files, invalidating on size/mtime change, pruning, and the end-to-end effect of
/// skipping hashing on a second inventory run.
/// </summary>
public sealed class FilePictureSortJournalCacheTests
{
    private static readonly DateTime Mtime = new(2026, 6, 1, 6, 57, 14, DateTimeKind.Utc);

    private sealed class CountingHasher : IContentHasher
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _count);
            return Task.FromResult("H_" + Path.GetFileName(filePath));
        }
    }

    /// <summary>Cancels the supplied token source on the first hash, then honors cancellation.</summary>
    private sealed class CancelingHasher : IContentHasher
    {
        private readonly CancellationTokenSource _cts;
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public CancelingHasher(CancellationTokenSource cts) => _cts = cts;
        public Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _count);
            _cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("H_" + Path.GetFileName(filePath));
        }
    }

    [Fact]
    public void TryGetCachedHash_ReturnsRecordedHash_WhenSizeAndMtimeMatch()
    {
        var fs = new InMemoryFileSystem();
        var dir = Path.Combine(Path.GetTempPath(), "cache-hit");
        var target = Path.Combine(dir, "a.jpg");
        fs.AddFile(target, "hello", Mtime);
        var length = fs.GetFileLength(target);
        var path = Path.Combine(dir, "journal.jsonl");

        var writer = new FilePictureSortJournal(path, fs);
        writer.RecordInventory(target, "HASH_A", length, Mtime);

        var reader = new FilePictureSortJournal(path, fs);
        reader.Load();

        Assert.True(reader.TryGetCachedHash(target, length, Mtime, out var hash));
        Assert.Equal("HASH_A", hash);
    }

    [Fact]
    public void TryGetCachedHash_Misses_WhenSizeChanged()
    {
        var fs = new InMemoryFileSystem();
        var dir = Path.Combine(Path.GetTempPath(), "cache-size");
        var target = Path.Combine(dir, "a.jpg");
        fs.AddFile(target, "hello", Mtime);
        var length = fs.GetFileLength(target);
        var path = Path.Combine(dir, "journal.jsonl");

        var journal = new FilePictureSortJournal(path, fs);
        journal.RecordInventory(target, "HASH_A", length, Mtime);

        Assert.False(journal.TryGetCachedHash(target, length + 1, Mtime, out var hash));
        Assert.Equal(string.Empty, hash);
    }

    [Fact]
    public void TryGetCachedHash_Misses_WhenMtimeChanged()
    {
        var fs = new InMemoryFileSystem();
        var dir = Path.Combine(Path.GetTempPath(), "cache-mtime");
        var target = Path.Combine(dir, "a.jpg");
        fs.AddFile(target, "hello", Mtime);
        var length = fs.GetFileLength(target);
        var path = Path.Combine(dir, "journal.jsonl");

        var journal = new FilePictureSortJournal(path, fs);
        journal.RecordInventory(target, "HASH_A", length, Mtime);

        Assert.False(journal.TryGetCachedHash(target, length, Mtime.AddSeconds(5), out _));
    }

    [Fact]
    public void RecordInventory_ThenReload_RecognizesEntryViaCache()
    {
        var fs = new InMemoryFileSystem();
        var dir = Path.Combine(Path.GetTempPath(), "cache-reload");
        var target = Path.Combine(dir, "a.jpg");
        fs.AddFile(target, "hello", Mtime);
        var length = fs.GetFileLength(target);
        var path = Path.Combine(dir, "journal.jsonl");

        new FilePictureSortJournal(path, fs).RecordInventory(target, "HASH_A", length, Mtime);

        var reader = new FilePictureSortJournal(path, fs);
        reader.Load();

        Assert.Equal(1, reader.EntriesLoaded);
        Assert.Contains("HASH_A", reader.KnownHashes);
        Assert.True(reader.TryGetCachedHash(target, length, Mtime, out _));
    }

    [Fact]
    public void Compact_KeepsSeenAndExisting_PrunesUnseenAndMissing()
    {
        var fs = new InMemoryFileSystem();
        var dir = Path.Combine(Path.GetTempPath(), "cache-compact");
        var a = Path.Combine(dir, "a.jpg");
        var b = Path.Combine(dir, "b.jpg");
        var c = Path.Combine(dir, "c.jpg");
        fs.AddFile(a, "aaa", Mtime);
        fs.AddFile(b, "bbb", Mtime);
        fs.AddFile(c, "ccc", Mtime);
        var path = Path.Combine(dir, "journal.jsonl");

        var writer = new FilePictureSortJournal(path, fs);
        writer.RecordInventory(a, "HA", fs.GetFileLength(a), Mtime);
        writer.RecordInventory(b, "HB", fs.GetFileLength(b), Mtime);
        writer.RecordInventory(c, "HC", fs.GetFileLength(c), Mtime);

        // Simulate the next run: load, mark some files as seen, mutate the target.
        var journal = new FilePictureSortJournal(path, fs);
        journal.Load();

        Assert.True(journal.TryGetCachedHash(a, fs.GetFileLength(a), Mtime, out _)); // seen + exists -> kept
        Assert.True(journal.TryGetCachedHash(c, fs.GetFileLength(c), Mtime, out _)); // seen, then deleted -> pruned
        fs.Delete(c);
        // b is never seen this run -> pruned

        var result = journal.Compact();

        Assert.Equal(1, result.Kept);
        Assert.Equal(2, result.Removed);

        var reloaded = new FilePictureSortJournal(path, fs);
        reloaded.Load();
        Assert.Equal(1, reloaded.EntriesLoaded);
        Assert.Contains("HA", reloaded.KnownHashes);
        Assert.DoesNotContain("HB", reloaded.KnownHashes);
        Assert.DoesNotContain("HC", reloaded.KnownHashes);
    }

    [Fact]
    public void Reload_CollapsesDuplicatePathLines_KeepingLatest()
    {
        var fs = new InMemoryFileSystem();
        var dir = Path.Combine(Path.GetTempPath(), "cache-dupe");
        var target = Path.Combine(dir, "a.jpg");
        fs.AddFile(target, "hello", Mtime);
        var path = Path.Combine(dir, "journal.jsonl");

        var writer = new FilePictureSortJournal(path, fs);
        // Two appends for the same path (e.g. file changed between runs) -> two lines.
        writer.Append(new JournalEntry("OLD", target, DateTime.UtcNow, 5, Mtime));
        writer.Append(new JournalEntry("NEW", target, DateTime.UtcNow, 5, Mtime));

        var reader = new FilePictureSortJournal(path, fs);
        reader.Load();

        Assert.Equal(1, reader.EntriesLoaded);
        Assert.Contains("NEW", reader.KnownHashes);
        Assert.DoesNotContain("OLD", reader.KnownHashes);
    }

    [Fact]
    public async Task InventoryDirectory_SecondRun_SkipsHashing_WhenTargetUnchanged()
    {
        var fs = new InMemoryFileSystem();
        var scanDir = Path.Combine(Path.GetTempPath(), "inv-cache-scan");
        var journalDir = Path.Combine(Path.GetTempPath(), "inv-cache-journal");
        fs.CreateDirectory(journalDir);
        fs.AddFile(Path.Combine(scanDir, "1.jpg"), "one", Mtime);
        fs.AddFile(Path.Combine(scanDir, "2.jpg"), "two", Mtime);
        var journalPath = Path.Combine(journalDir, "journal.jsonl");

        var hasher = new CountingHasher();
        var inventory = new InventoryDirectory(fs, hasher);

        // First run: nothing cached -> both files hashed.
        var firstJournal = new FilePictureSortJournal(journalPath, fs);
        firstJournal.Load();
        var firstRun = await inventory.InventoryADirectoryAsync(
            new[] { scanDir }, 2, new TestProgress(), false, Array.Empty<string>(), firstJournal, CancellationToken.None);
        firstJournal.Compact();

        Assert.Equal(2, hasher.Count);

        // Second run: same files (path+size+mtime unchanged) -> zero hashing.
        var hashesBefore = hasher.Count;
        var secondJournal = new FilePictureSortJournal(journalPath, fs);
        secondJournal.Load();
        var secondRun = await inventory.InventoryADirectoryAsync(
            new[] { scanDir }, 2, new TestProgress(), false, Array.Empty<string>(), secondJournal, CancellationToken.None);

        Assert.Equal(hashesBefore, hasher.Count);
        Assert.Equal(
            firstRun.Files.OrderBy(f => f.FullPath).Select(f => f.Hash),
            secondRun.Files.OrderBy(f => f.FullPath).Select(f => f.Hash));
    }

    [Fact]
    public void Load_DoesNotCreateJournalFile_WhenMissing()
    {
        var fs = new InMemoryFileSystem();
        var dir = Path.Combine(Path.GetTempPath(), "cache-dryrun");
        fs.CreateDirectory(dir);
        var path = Path.Combine(dir, "journal.jsonl");

        var journal = new FilePictureSortJournal(path, fs);
        journal.Load();

        // A dry run only loads the journal; loading must remain completely write-free.
        Assert.False(fs.FileExists(path));
        Assert.Empty(journal.KnownHashes);
    }

    [Fact]
    public void TryGetCachedHash_FalseHit_WhenContentReplacedButSizeAndMtimePreserved()
    {
        // Documents the accepted size+mtime trade-off: replacing content in place while
        // keeping the exact same length and mtime is NOT detected (same as rsync default).
        var fs = new InMemoryFileSystem();
        var dir = Path.Combine(Path.GetTempPath(), "cache-falsehit");
        var target = Path.Combine(dir, "a.jpg");
        fs.AddFile(target, "AAAAA", Mtime);
        var length = fs.GetFileLength(target);
        var path = Path.Combine(dir, "journal.jsonl");

        var journal = new FilePictureSortJournal(path, fs);
        journal.RecordInventory(target, "HASH_OLD", length, Mtime);

        // Replace content with same byte length and same mtime.
        fs.AddFile(target, "BBBBB", Mtime);
        Assert.Equal(length, fs.GetFileLength(target));

        Assert.True(journal.TryGetCachedHash(target, fs.GetFileLength(target), Mtime, out var hash));
        Assert.Equal("HASH_OLD", hash); // stale hash served by design
    }

    [Fact]
    public void Cache_NormalizesSubSecondMtime_OnRecordAndProbe()
    {
        var fs = new InMemoryFileSystem();
        var dir = Path.Combine(Path.GetTempPath(), "cache-subsec");
        var target = Path.Combine(dir, "a.jpg");
        var withTicks = new DateTime(2026, 6, 1, 6, 57, 14, 123, DateTimeKind.Utc).AddTicks(4567);
        fs.AddFile(target, "hello", withTicks);
        var length = fs.GetFileLength(target);
        var path = Path.Combine(dir, "journal.jsonl");

        var writer = new FilePictureSortJournal(path, fs);
        writer.RecordInventory(target, "HASH_A", length, withTicks);

        var reader = new FilePictureSortJournal(path, fs);
        reader.Load();

        // A probe whose sub-second component differs but whose whole second matches still hits.
        var sameSecondDifferentTicks = new DateTime(2026, 6, 1, 6, 57, 14, 0, DateTimeKind.Utc).AddTicks(99);
        Assert.True(reader.TryGetCachedHash(target, length, sameSecondDifferentTicks, out var hash));
        Assert.Equal("HASH_A", hash);

        // A probe one whole second later misses.
        Assert.False(reader.TryGetCachedHash(target, length, withTicks.AddSeconds(1), out _));
    }

    [Fact]
    public void Cache_ConvergesUtc_ForLocalUnspecifiedAndUtcKinds()
    {
        var fs = new InMemoryFileSystem();
        var dir = Path.Combine(Path.GetTempPath(), "cache-kinds");
        var target = Path.Combine(dir, "a.jpg");
        fs.AddFile(target, "hello", Mtime);
        var length = fs.GetFileLength(target);
        var path = Path.Combine(dir, "journal.jsonl");

        var journal = new FilePictureSortJournal(path, fs);
        journal.RecordInventory(target, "HASH_A", length, Mtime);

        // Local-kind probe representing the same instant must converge to the same UTC second.
        var asLocal = Mtime.ToLocalTime();
        Assert.Equal(DateTimeKind.Local, asLocal.Kind);
        Assert.True(journal.TryGetCachedHash(target, length, asLocal, out var fromLocal));
        Assert.Equal("HASH_A", fromLocal);

        // Utc-kind probe of the exact instant hits as well.
        Assert.True(journal.TryGetCachedHash(target, length, Mtime, out var fromUtc));
        Assert.Equal("HASH_A", fromUtc);
    }

    [Fact]
    public async Task InventoryDirectory_CancelledMidRun_DoesNotPruneJournal()
    {
        var fs = new InMemoryFileSystem();
        var scanDir = Path.Combine(Path.GetTempPath(), "inv-cancel-scan");
        var journalDir = Path.Combine(Path.GetTempPath(), "inv-cancel-journal");
        fs.CreateDirectory(journalDir);
        var a = Path.Combine(scanDir, "a.jpg");
        var b = Path.Combine(scanDir, "b.jpg");
        fs.AddFile(a, "one", Mtime);
        fs.AddFile(b, "two", Mtime);
        var journalPath = Path.Combine(journalDir, "journal.jsonl");

        // Seed the journal with entries whose mtime no longer matches, forcing a re-hash
        // (cache miss) so the canceling hasher is actually invoked.
        var staleMtime = Mtime.AddDays(-1);
        var seed = new FilePictureSortJournal(journalPath, fs);
        seed.RecordInventory(a, "SEED_A", fs.GetFileLength(a), staleMtime);
        seed.RecordInventory(b, "SEED_B", fs.GetFileLength(b), staleMtime);

        using var cts = new CancellationTokenSource();
        var hasher = new CancelingHasher(cts);
        var inventory = new InventoryDirectory(fs, hasher);

        var journal = new FilePictureSortJournal(journalPath, fs);
        journal.Load();

        // Cancellation mid-inventory must surface as an OperationCanceledException.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            inventory.InventoryADirectoryAsync(
                new[] { scanDir }, 1, new TestProgress(), false, Array.Empty<string>(), journal, cts.Token));

        // Inventory never compacts on its own, so a partial/aborted run must not prune
        // anything: the seeded entries are still present on disk.
        var reloaded = new FilePictureSortJournal(journalPath, fs);
        reloaded.Load();
        Assert.True(reloaded.EntriesLoaded >= 2);
        Assert.Contains("SEED_B", reloaded.KnownHashes);
    }

    [Fact]
    public void Compact_PreservesExistingJournal_WhenTempWriteFails()
    {
        var inner = new InMemoryFileSystem();
        var dir = Path.Combine(Path.GetTempPath(), "cache-crash");
        var a = Path.Combine(dir, "a.jpg");
        var b = Path.Combine(dir, "b.jpg");
        inner.AddFile(a, "aaa", Mtime);
        inner.AddFile(b, "bbb", Mtime);
        var path = Path.Combine(dir, "journal.jsonl");

        var writer = new FilePictureSortJournal(path, inner);
        writer.RecordInventory(a, "HA", inner.GetFileLength(a), Mtime);
        writer.RecordInventory(b, "HB", inner.GetFileLength(b), Mtime);

        var before = inner.ReadAllLines(path).ToArray();

        // Fail the compaction temp write; the original journal must stay intact.
        var faulting = new FaultingFileSystem(inner, failWriteWhenPathContains: ".compact.tmp");
        var journal = new FilePictureSortJournal(path, faulting);
        journal.Load();
        journal.TryGetCachedHash(a, inner.GetFileLength(a), Mtime, out _); // mark a seen

        Assert.Throws<IOException>(() => journal.Compact());

        // Original journal content is untouched (no truncation / data loss).
        var after = inner.ReadAllLines(path).ToArray();
        Assert.Equal(before, after);
        Assert.False(inner.FileExists(path + ".compact.tmp"));
    }
}

/// <summary>
/// Wraps an <see cref="InMemoryFileSystem"/> and throws on <see cref="WriteAllText"/> when the
/// target path contains a configured fragment, to simulate a crash/IO error mid-compaction.
/// </summary>
internal sealed class FaultingFileSystem : IFileSystem
{
    private readonly InMemoryFileSystem _inner;
    private readonly string _failWriteWhenPathContains;

    public FaultingFileSystem(InMemoryFileSystem inner, string failWriteWhenPathContains)
    {
        _inner = inner;
        _failWriteWhenPathContains = failWriteWhenPathContains;
    }

    public void WriteAllText(string path, string content)
    {
        if (path.Contains(_failWriteWhenPathContains, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("Simulated write failure.");
        }
        _inner.WriteAllText(path, content);
    }

    public bool FileExists(string path) => _inner.FileExists(path);
    public bool DirectoryExists(string path) => _inner.DirectoryExists(path);
    public void CreateDirectory(string path) => _inner.CreateDirectory(path);
    public void Move(string source, string destination) => _inner.Move(source, destination);
    public bool TryMove(string source, string destination) => _inner.TryMove(source, destination);
    public void Copy(string source, string destination, bool overwrite) => _inner.Copy(source, destination, overwrite);
    public void Delete(string path) => _inner.Delete(path);
    public void DeleteEmptyDirectory(string path) => _inner.DeleteEmptyDirectory(path);
    public Stream OpenRead(string path) => _inner.OpenRead(path);
    public Stream OpenWrite(string path) => _inner.OpenWrite(path);
    public IReadOnlyList<string> ReadAllLines(string path) => _inner.ReadAllLines(path);
    public void AppendAllText(string path, string content) => _inner.AppendAllText(path, content);
    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, SearchOption searchOption)
        => _inner.EnumerateFiles(directory, searchPattern, searchOption);
    public IEnumerable<string> EnumerateDirectories(string directory) => _inner.EnumerateDirectories(directory);
    public string? GetPathRoot(string path) => _inner.GetPathRoot(path);
    public DateTime GetCreationTime(string path) => _inner.GetCreationTime(path);
    public DateTime GetLastWriteTime(string path) => _inner.GetLastWriteTime(path);
    public DateTime GetLastAccessTime(string path) => _inner.GetLastAccessTime(path);
    public long GetFileLength(string path) => _inner.GetFileLength(path);
}
