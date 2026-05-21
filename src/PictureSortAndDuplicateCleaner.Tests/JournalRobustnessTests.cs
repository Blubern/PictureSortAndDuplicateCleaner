using PictureSortAndDuplicateCleaner.Journal;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class JournalRobustnessTests
{
    [Fact]
    public void Load_OnMissingFile_IsNoOpAndReportsZero()
    {
        using var dir = new TemporaryDirectory();
        var path = Path.Combine(dir.Path, "does-not-exist.jsonl");
        var journal = new FilePictureSortJournal(path);

        journal.Load();

        Assert.Equal(0, journal.EntriesLoaded);
        Assert.Equal(0, journal.EntriesStale);
        Assert.Empty(journal.KnownHashes);
        Assert.False(File.Exists(path), "Load must not create the file.");
    }

    [Fact]
    public void Load_SkipsCorruptedJsonLines_AndKeepsValidEntries()
    {
        using var dir = new TemporaryDirectory();
        var targetFile = dir.CreateFile("kept-target.jpg", "x");
        var path = Path.Combine(dir.Path, "journal.jsonl");
        var lines = new[]
        {
            "{\"schema\":\"picsort-journal/v1\"}",
            "{ this is not valid json",
            "{\"hash\":\"abc\",\"targetPath\":\"" + targetFile.Replace("\\", "\\\\") + "\",\"movedAtUtc\":\"2024-05-19T10:30:00Z\"}",
            "",
            "garbage line without braces",
        };
        File.WriteAllLines(path, lines);

        var journal = new FilePictureSortJournal(path);
        journal.Load();

        Assert.Equal(1, journal.EntriesLoaded);
        Assert.Contains("abc", journal.KnownHashes);
    }

    [Fact]
    public void Load_CountsEntriesWhoseTargetFileNoLongerExists_AsStale()
    {
        using var dir = new TemporaryDirectory();
        var path = Path.Combine(dir.Path, "journal.jsonl");
        var staleTarget = Path.Combine(dir.Path, "vanished.jpg").Replace("\\", "\\\\");
        var lines = new[]
        {
            "{\"schema\":\"picsort-journal/v1\"}",
            "{\"hash\":\"stale1\",\"targetPath\":\"" + staleTarget + "\",\"movedAtUtc\":\"2024-05-19T10:30:00Z\"}",
            "{\"hash\":\"stale2\",\"targetPath\":\"" + staleTarget + "\",\"movedAtUtc\":\"2024-05-19T10:30:01Z\"}",
        };
        File.WriteAllLines(path, lines);

        var journal = new FilePictureSortJournal(path);
        journal.Load();

        Assert.Equal(0, journal.EntriesLoaded);
        Assert.Equal(2, journal.EntriesStale);
        Assert.Empty(journal.KnownHashes);
    }

    [Fact]
    public void Load_IsIdempotent()
    {
        using var dir = new TemporaryDirectory();
        var targetFile = dir.CreateFile("kept.jpg", "x").Replace("\\", "\\\\");
        var path = Path.Combine(dir.Path, "journal.jsonl");
        File.WriteAllLines(path, new[]
        {
            "{\"schema\":\"picsort-journal/v1\"}",
            "{\"hash\":\"only\",\"targetPath\":\"" + targetFile + "\",\"movedAtUtc\":\"2024-05-19T10:30:00Z\"}",
        });

        var journal = new FilePictureSortJournal(path);
        journal.Load();
        journal.Load(); // second call must not double-count

        Assert.Equal(1, journal.EntriesLoaded);
        Assert.Single(journal.KnownHashes);
    }

    [Fact]
    public void Append_OnFirstWrite_CreatesFileWithSchemaHeader()
    {
        using var dir = new TemporaryDirectory();
        var path = Path.Combine(dir.Path, "fresh-journal.jsonl");
        var journal = new FilePictureSortJournal(path);

        journal.Append(new JournalEntry("hash1", Path.Combine(dir.Path, "anywhere.jpg"), DateTime.UtcNow));

        Assert.True(File.Exists(path));
        var lines = File.ReadAllLines(path);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"schema\"", lines[0]);
        Assert.Contains("\"hash1\"", lines[1]);
        Assert.Equal(1, journal.EntriesWritten);
    }

    [Fact]
    public void Append_FromMultipleThreads_IsSerializedAndAllEntriesPersist()
    {
        using var dir = new TemporaryDirectory();
        var path = Path.Combine(dir.Path, "concurrent.jsonl");
        var journal = new FilePictureSortJournal(path);
        const int writers = 16;
        const int perWriter = 25;

        Parallel.For(0, writers, w =>
        {
            for (var i = 0; i < perWriter; i++)
            {
                journal.Append(new JournalEntry(
                    $"hash_{w}_{i}",
                    Path.Combine(dir.Path, $"file_{w}_{i}.jpg"),
                    DateTime.UtcNow));
            }
        });

        var lines = File.ReadAllLines(path);
        // header + writers*perWriter data lines, no truncation/partial writes
        Assert.Equal(1 + writers * perWriter, lines.Length);
        Assert.Equal(writers * perWriter, journal.EntriesWritten);
        // Every line beyond the header must be valid JSON (no interleaving).
        foreach (var line in lines.Skip(1))
        {
            Assert.True(line.StartsWith("{") && line.EndsWith("}"),
                $"Line not a complete JSON object: {line}");
        }
    }

    [Fact]
    public void Constructor_WithExistingDirectoryPath_ResolvesToDefaultFileName()
    {
        using var dir = new TemporaryDirectory();
        var journal = new FilePictureSortJournal(dir.Path);

        Assert.Equal(
            Path.Combine(dir.Path, FilePictureSortJournal.DefaultFileName),
            journal.FilePath);
    }

    [Fact]
    public void Constructor_WithEmptyPath_Throws()
    {
        Assert.Throws<ArgumentException>(() => new FilePictureSortJournal(""));
        Assert.Throws<ArgumentException>(() => new FilePictureSortJournal("   "));
    }
}
