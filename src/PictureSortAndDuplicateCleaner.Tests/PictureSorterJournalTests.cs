using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;
using PictureSortAndDuplicateCleaner.Journal;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSorterJournalTests
{
    [Fact]
    public async Task StartPictureSortAsync_WithoutJournal_ProducesZeroJournalStats()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        sourceDirectory.CreateFile("photo.jpg", "image bytes");

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(0, result.JournalEntriesLoaded);
        Assert.Equal(0, result.JournalEntriesWritten);
        Assert.Equal(0, result.JournalEntriesStale);
    }

    [Fact]
    public async Task StartPictureSortAsync_WithJournalEnabled_AppendsOneEntryPerMovedPrimary()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var journalDirectory = new InMemoryDirectory(fs);
        sourceDirectory.CreateFile("a.jpg", "image bytes A");
        sourceDirectory.CreateFile("b.jpg", "image bytes B");
        var journalPath = System.IO.Path.Combine(journalDirectory.Path, "journal.jsonl");

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: null,
            journalFilePath: journalPath);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.True(fs.FileExists(journalPath));
        Assert.Equal(0, result.JournalEntriesLoaded);
        Assert.Equal(2, result.JournalEntriesWritten);
        var lines = fs.ReadAllLines(journalPath);
        Assert.Equal(FilePictureSortJournal.SchemaVersion, "picturesortandduplicatecleaner-journal/v1");
        Assert.Contains(lines, l => l.Contains("\"schema\""));
        Assert.Equal(3, lines.Count); // header + 2 entries
    }

    [Fact]
    public async Task StartPictureSortAsync_WithJournalKnowingHash_RoutesFileToAlreadyExistsEvenWithoutTargetInventory()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var journalDirectory = new InMemoryDirectory(fs);
        var journalPath = System.IO.Path.Combine(journalDirectory.Path, "journal.jsonl");

        // Pre-populate the journal by running a first sort.
        var firstSource = sourceDirectory.CreateFile("a.jpg", "duplicate image bytes");
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var firstParameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: null,
            journalFilePath: journalPath);
        var firstResult = await sorter.StartPictureSortAsync(firstParameter, new TestProgress(), CancellationToken.None);
        Assert.Equal(1, firstResult.JournalEntriesWritten);
        Assert.False(fs.FileExists(firstSource));

        // Drop the same image into source again — but DO NOT inventory the target.
        var secondSource = sourceDirectory.CreateFile("a.jpg", "duplicate image bytes");
        var secondParameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: null,
            journalFilePath: journalPath);
        var secondResult = await sorter.StartPictureSortAsync(secondParameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(1, secondResult.JournalEntriesLoaded);
        Assert.Equal(1, secondResult.AlreadyExistingFilesMoved);
        var existingRoot = System.IO.Path.Combine(sourceDirectory.Path, "!ExistsInTarget");
        Assert.True(fs.DirectoryExists(existingRoot));
        Assert.Single(fs.EnumerateFiles(existingRoot, "a.jpg", SearchOption.AllDirectories).ToArray());
    }

    [Fact]
    public async Task StartPictureSortAsync_WhenJournalEntryFileMissing_CountsItStaleAndDoesNotFilter()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var journalDirectory = new InMemoryDirectory(fs);
        var journalPath = System.IO.Path.Combine(journalDirectory.Path, "journal.jsonl");
        // Write a stale entry pointing to a non-existent file.
        var staleJournal = new FilePictureSortJournal(journalPath, fs);
        staleJournal.Append(new JournalEntry("deadbeef", System.IO.Path.Combine(targetDirectory.Path, "missing.jpg"), DateTime.UtcNow));

        sourceDirectory.CreateFile("photo.jpg", "image bytes");

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: null,
            journalFilePath: journalPath);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(0, result.JournalEntriesLoaded);
        Assert.Equal(1, result.JournalEntriesStale);
        Assert.Equal(1, result.JournalEntriesWritten);
    }

    [Fact]
    public void FilePictureSortJournal_WhenGivenExistingDirectoryPath_UsesDefaultFileName()
    {
        var fs = new InMemoryFileSystem();
        var directory = new InMemoryDirectory(fs);

        var journal = new FilePictureSortJournal(directory.Path, fs);

        Assert.Equal(System.IO.Path.Combine(directory.Path, FilePictureSortJournal.DefaultFileName), journal.FilePath);
    }
}
