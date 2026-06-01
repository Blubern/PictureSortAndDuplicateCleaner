using PictureSortAndDuplicateCleaner.Journal;

namespace PictureSortAndDuplicateCleaner.Tests;

/// <summary>
/// Covers the basic contract and edge cases that the robustness suite does not:
/// the real Append -> Load round-trip (i.e. "is what we write actually recognized
/// when read back?"), and the boundary conditions on entry recognition.
/// </summary>
public sealed class FilePictureSortJournalRoundTripTests
{
    [Fact]
    public void Append_ThenLoadWithFreshInstance_RecognizesEntriesWhoseTargetExists()
    {
        using var dir = new TemporaryDirectory();
        var path = Path.Combine(dir.Path, "journal.jsonl");
        var target = dir.CreateFile("kept.jpg", "x");

        var writer = new FilePictureSortJournal(path);
        writer.Append(new JournalEntry("roundtrip-hash", target, new DateTime(2026, 6, 1, 6, 57, 14, DateTimeKind.Utc)));

        // A brand-new instance must be able to read back exactly what was written.
        var reader = new FilePictureSortJournal(path);
        reader.Load();

        Assert.Equal(1, reader.EntriesLoaded);
        Assert.Equal(0, reader.EntriesStale);
        Assert.Contains("roundtrip-hash", reader.KnownHashes);
    }

    [Fact]
    public void Append_ThenLoad_TreatsEntryAsStaleWhenTargetWasRemoved()
    {
        using var dir = new TemporaryDirectory();
        var path = Path.Combine(dir.Path, "journal.jsonl");
        var target = dir.CreateFile("temp.jpg", "x");

        var writer = new FilePictureSortJournal(path);
        writer.Append(new JournalEntry("gone-hash", target, DateTime.UtcNow));

        File.Delete(target);

        var reader = new FilePictureSortJournal(path);
        reader.Load();

        Assert.Equal(0, reader.EntriesLoaded);
        Assert.Equal(1, reader.EntriesStale);
        Assert.Empty(reader.KnownHashes);
    }

    [Fact]
    public void Append_RecordsHashInKnownHashesImmediately_WithoutReload()
    {
        using var dir = new TemporaryDirectory();
        var path = Path.Combine(dir.Path, "journal.jsonl");
        var journal = new FilePictureSortJournal(path);

        journal.Append(new JournalEntry("live-hash", Path.Combine(dir.Path, "anywhere.jpg"), DateTime.UtcNow));

        Assert.Contains("live-hash", journal.KnownHashes);
    }

    [Fact]
    public void KnownHashes_IsCaseInsensitive()
    {
        using var dir = new TemporaryDirectory();
        var path = Path.Combine(dir.Path, "journal.jsonl");
        var journal = new FilePictureSortJournal(path);

        journal.Append(new JournalEntry("ABCDEF", Path.Combine(dir.Path, "anywhere.jpg"), DateTime.UtcNow));

        Assert.Contains("abcdef", journal.KnownHashes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Load_SkipsEntriesWithBlankHash(string blankHash)
    {
        using var dir = new TemporaryDirectory();
        var target = dir.CreateFile("kept.jpg", "x").Replace("\\", "\\\\");
        var path = Path.Combine(dir.Path, "journal.jsonl");
        File.WriteAllLines(path, new[]
        {
            "{\"schema\":\"picturesortandduplicatecleaner-journal/v1\"}",
            "{\"hash\":\"" + blankHash + "\",\"targetPath\":\"" + target + "\",\"movedAtUtc\":\"2026-06-01T06:57:14Z\"}",
        });

        var journal = new FilePictureSortJournal(path);
        journal.Load();

        Assert.Equal(0, journal.EntriesLoaded);
        Assert.Equal(0, journal.EntriesStale);
        Assert.Empty(journal.KnownHashes);
    }

    [Fact]
    public void Load_TreatsEntryWithBlankTargetPath_AsStale()
    {
        using var dir = new TemporaryDirectory();
        var path = Path.Combine(dir.Path, "journal.jsonl");
        File.WriteAllLines(path, new[]
        {
            "{\"schema\":\"picturesortandduplicatecleaner-journal/v1\"}",
            "{\"hash\":\"orphan\",\"targetPath\":\"\",\"movedAtUtc\":\"2026-06-01T06:57:14Z\"}",
        });

        var journal = new FilePictureSortJournal(path);
        journal.Load();

        Assert.Equal(0, journal.EntriesLoaded);
        Assert.Equal(1, journal.EntriesStale);
        Assert.Empty(journal.KnownHashes);
    }

    [Fact]
    public void Load_SkipsSchemaHeaderRegardlessOfCasing()
    {
        using var dir = new TemporaryDirectory();
        var target = dir.CreateFile("kept.jpg", "x").Replace("\\", "\\\\");
        var path = Path.Combine(dir.Path, "journal.jsonl");
        File.WriteAllLines(path, new[]
        {
            "{\"SCHEMA\":\"picturesortandduplicatecleaner-journal/v1\"}",
            "{\"hash\":\"after-header\",\"targetPath\":\"" + target + "\",\"movedAtUtc\":\"2026-06-01T06:57:14Z\"}",
        });

        var journal = new FilePictureSortJournal(path);
        journal.Load();

        Assert.Equal(1, journal.EntriesLoaded);
        Assert.Contains("after-header", journal.KnownHashes);
    }

    [Fact]
    public void Load_OnFileWithOnlySchemaHeader_RecognizesNothing()
    {
        // Mirrors the real-world report: the source directory had no files to move,
        // so only the schema header line was ever written.
        using var dir = new TemporaryDirectory();
        var path = Path.Combine(dir.Path, "journal.jsonl");
        File.WriteAllText(path, "{\"schema\":\"picturesortandduplicatecleaner-journal/v1\"}" + Environment.NewLine);

        var journal = new FilePictureSortJournal(path);
        journal.Load();

        Assert.Equal(0, journal.EntriesLoaded);
        Assert.Equal(0, journal.EntriesStale);
        Assert.Empty(journal.KnownHashes);
    }

    [Fact]
    public void AppendedEntries_SurviveMultipleAppends_AndAllRoundTrip()
    {
        using var dir = new TemporaryDirectory();
        var path = Path.Combine(dir.Path, "journal.jsonl");
        var writer = new FilePictureSortJournal(path);

        var targets = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var target = dir.CreateFile($"file_{i}.jpg", $"content-{i}");
            targets.Add(target);
            writer.Append(new JournalEntry($"hash_{i}", target, DateTime.UtcNow));
        }

        var reader = new FilePictureSortJournal(path);
        reader.Load();

        Assert.Equal(5, reader.EntriesLoaded);
        for (var i = 0; i < 5; i++)
        {
            Assert.Contains($"hash_{i}", reader.KnownHashes);
        }
    }

    [Fact]
    public void Constructor_WithTrailingSeparator_ResolvesToDefaultFileName()
    {
        using var dir = new TemporaryDirectory();
        var withSeparator = dir.Path + Path.DirectorySeparatorChar;

        var journal = new FilePictureSortJournal(withSeparator);

        Assert.Equal(
            Path.Combine(dir.Path, FilePictureSortJournal.DefaultFileName),
            journal.FilePath);
    }
}
