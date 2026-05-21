using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSorterDryRunCopyComboTests
{
    [Fact]
    public async Task DryRun_PlusCopyMode_LeavesSourceAndTargetUntouched()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var sourceFile = sourceDirectory.CreateFile("photo.jpg", "image bytes", lastWriteUtc: new DateTime(2024, 1, 1, 12, 0, 0));

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            dryRun: true,
            operationMode: OperationMode.Copy);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.True(fs.FileExists(sourceFile));
        Assert.Empty(fs.EnumerateFiles(targetDirectory.Path, "*", SearchOption.AllDirectories));
        Assert.Equal(1, result.FilesMovedToTarget);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task DryRun_WithJournalConfigured_DoesNotWriteAnyJournalEntries()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var journalDirectory = new InMemoryDirectory(fs);
        var sourceFile = sourceDirectory.CreateFile("photo.jpg", "image bytes", lastWriteUtc: new DateTime(2024, 1, 1, 12, 0, 0));
        var journalPath = Path.Combine(journalDirectory.Path, "journal.jsonl");

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: null,
            journalFilePath: journalPath,
            dryRun: true);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.True(fs.FileExists(sourceFile), "dry-run must leave the source file untouched.");
        Assert.Equal(0, result.JournalEntriesWritten);
        if (fs.FileExists(journalPath))
        {
            // If the file was created (header only) it must not contain a data entry.
            var lines = fs.ReadAllLines(journalPath).Where(l => !l.Contains("\"schema\"") && l.Length > 0).ToArray();
            Assert.Empty(lines);
        }
    }

    [Fact]
    public async Task DryRun_WithDuplicates_DoesNotMoveDuplicatesEither()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var takenTime = new DateTime(2024, 4, 1, 10, 0, 0);
        var a = sourceDirectory.CreateFile("a.jpg", "same bytes", lastWriteUtc: takenTime);
        var b = sourceDirectory.CreateFile("b.jpg", "same bytes", lastWriteUtc: takenTime);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            dryRun: true);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.True(fs.FileExists(a));
        Assert.True(fs.FileExists(b));
        Assert.False(fs.DirectoryExists(Path.Combine(sourceDirectory.Path, "!Duplicate")),
            "dry-run must not create the duplicate-folder structure on disk.");
        Assert.Empty(fs.EnumerateFiles(targetDirectory.Path, "*", SearchOption.AllDirectories));
        Assert.Equal(1, result.DuplicateFilesMoved);
        Assert.Equal(1, result.FilesMovedToTarget);
    }

    [Fact]
    public async Task CopyMode_WithDuplicates_KeepsSourceUntouchedAndCopiesOneToTarget()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var takenTime = new DateTime(2024, 4, 1, 10, 0, 0);
        var a = sourceDirectory.CreateFile("a.jpg", "same bytes", lastWriteUtc: takenTime);
        var b = sourceDirectory.CreateFile("b.jpg", "same bytes", lastWriteUtc: takenTime);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            operationMode: OperationMode.Copy);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.True(fs.FileExists(a), "copy mode must keep source intact.");
        Assert.True(fs.FileExists(b), "copy mode must keep source intact.");
        Assert.False(fs.DirectoryExists(Path.Combine(sourceDirectory.Path, "!Duplicate")),
            "copy mode must not reorganize source duplicates.");
        var copiedFiles = fs.EnumerateFiles(targetDirectory.Path, "*.jpg", SearchOption.AllDirectories).ToArray();
        Assert.Single(copiedFiles);
        Assert.Equal(1, result.FilesMovedToTarget);
    }

    [Fact]
    public async Task CopyMode_PlusJournalKnowingHash_SkipsCopyForKnownFile()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var journalDirectory = new InMemoryDirectory(fs);
        var journalPath = Path.Combine(journalDirectory.Path, "journal.jsonl");

        // First run: copy a file to populate the journal.
        var firstSource = sourceDirectory.CreateFile("a.jpg", "image bytes", lastWriteUtc: new DateTime(2024, 5, 1, 9, 0, 0));
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var firstParameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: null,
            journalFilePath: journalPath,
            operationMode: OperationMode.Copy);
        var firstResult = await sorter.StartPictureSortAsync(firstParameter, new TestProgress(), CancellationToken.None);
        Assert.Equal(1, firstResult.JournalEntriesWritten);
        Assert.True(fs.FileExists(firstSource), "copy keeps source.");

        // Second run: same bytes, but target inventory disabled — journal must rescue dedupe.
        // Delete first source so we can re-create it without name collision tricks.
        fs.Delete(firstSource);
        var secondSource = sourceDirectory.CreateFile("a.jpg", "image bytes", lastWriteUtc: new DateTime(2024, 5, 1, 9, 0, 0));
        var secondParameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: null,
            journalFilePath: journalPath,
            operationMode: OperationMode.Copy);
        var secondResult = await sorter.StartPictureSortAsync(secondParameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(1, secondResult.JournalEntriesLoaded);
        Assert.Equal(0, secondResult.FilesMovedToTarget);
        Assert.Equal(1, secondResult.AlreadyExistingFilesMoved);
        Assert.True(fs.FileExists(secondSource), "copy mode keeps source even when skipped.");
        var copiedFiles = fs.EnumerateFiles(targetDirectory.Path, "a*.jpg", SearchOption.AllDirectories).ToArray();
        Assert.Single(copiedFiles); // only the first run's copy
    }
}
