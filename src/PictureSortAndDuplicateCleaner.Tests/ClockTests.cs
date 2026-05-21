using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class ClockTests
{
    [Fact]
    public void FixedClock_AdvanceMovesTimeForward()
    {
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), clock.UtcNow);
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(new DateTime(2026, 1, 1, 0, 5, 0, DateTimeKind.Utc), clock.UtcNow);
    }

    [Fact]
    public void FixedClock_ForcesUtcKind()
    {
        var clock = new FixedClock(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Unspecified));
        Assert.Equal(DateTimeKind.Utc, clock.UtcNow.Kind);
    }

    [Fact]
    public async Task PictureSorter_UsesInjectedClockForJournalTimestamps()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var journalDirectory = new InMemoryDirectory(fs);
        sourceDirectory.CreateFile("photo.jpg", "image bytes");

        var fixedTime = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);
        var clock = new FixedClock(fixedTime);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs, clock);
        var journalFile = Path.Combine(journalDirectory.Path, "journal.jsonl");
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            journalFilePath: journalFile);

        await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.True(fs.FileExists(journalFile));
        var lines = fs.ReadAllLines(journalFile);
        var entry = Assert.Single(lines, l => l.Contains("\"hash\""));
        Assert.Contains("2026-05-20T12:00:00", entry);
    }
}
