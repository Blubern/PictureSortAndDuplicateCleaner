using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;
using PictureSortAndDuplicateCleaner.Events;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSorterEdgeCaseTests
{
    [Fact]
    public async Task UnknownDatePolicy_Fail_LeavesFileInSourceAndCountsAsError()
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

        var events = new TestEventProgress();
        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), events, CancellationToken.None);

        Assert.True(fs.FileExists(sourceFile), "Fail policy must leave the dateless file in source.");
        Assert.Empty(fs.EnumerateFiles(targetDirectory.Path, "*", SearchOption.AllDirectories));
        Assert.Equal(1, result.ErrorCount);
        Assert.Equal(0, result.FilesMovedToTarget);
        Assert.Equal(0, result.FilesWithoutDateSkipped);
        Assert.Contains(events.Captured, e => e is FileFailedEvent f && f.Reason == "NoDate");
        Assert.Contains(events.Captured, e => e is FileWithoutDateEvent w && w.Policy == UnknownDatePolicy.Fail);
    }

    [Fact]
    public async Task MixedRun_DatedPlusDuplicatePlusAlreadyExisting_AllCountersConsistent()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        // Pre-seed target with a file whose bytes match `existing.jpg` in source.
        targetDirectory.CreateFile(Path.Combine("archive", "old.jpg"), "already-imported bytes");
        var takenTime = new DateTime(2024, 7, 4, 12, 0, 0);
        sourceDirectory.CreateFile("dated.jpg", "image bytes A", lastWriteUtc: takenTime);
        sourceDirectory.CreateFile("dupA.jpg", "image bytes B", lastWriteUtc: takenTime);
        sourceDirectory.CreateFile("dupB.jpg", "image bytes B", lastWriteUtc: takenTime);
        sourceDirectory.CreateFile("existing.jpg", "already-imported bytes", lastWriteUtc: takenTime);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            2,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: true);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(4, result.SourceFilesFound);
        // dated + one-of-duplicates = 2 moved to target; second dup → !Duplicate; existing → !ExistsInTarget.
        Assert.Equal(2, result.FilesMovedToTarget);
        Assert.Equal(1, result.DuplicateFilesMoved);
        Assert.Equal(1, result.AlreadyExistingFilesMoved);
        Assert.Equal(0, result.ErrorCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public async Task MaxConcurrency_AtBoundaries_AllFilesAreMoved(int concurrency)
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var takenTime = new DateTime(2024, 3, 15, 9, 0, 0);
        for (var i = 0; i < 12; i++)
        {
            sourceDirectory.CreateFile($"photo_{i:00}.jpg", $"content {i}", lastWriteUtc: takenTime);
        }

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            concurrency,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(12, result.SourceFilesFound);
        Assert.Equal(12, result.FilesMovedToTarget);
        Assert.Equal(0, result.ErrorCount);
        var movedFiles = fs.EnumerateFiles(targetDirectory.Path, "*.jpg", SearchOption.AllDirectories).ToArray();
        Assert.Equal(12, movedFiles.Length);
    }

    [Fact]
    public async Task UnicodeAndSpacesInFileName_AreHandled()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var takenTime = new DateTime(2024, 2, 29, 8, 15, 0); // leap day
        var unicodeName = "Foto 📸 äöü.jpg";
        sourceDirectory.CreateFile(unicodeName, "image bytes", lastWriteUtc: takenTime);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        var expected = Path.Combine(
            targetDirectory.Path,
            takenTime.ToString("yyyy"),
            takenTime.ToString("MMMM"),
            takenTime.ToString("dd"),
            unicodeName);
        Assert.True(fs.FileExists(expected), $"expected file at {expected}");
        Assert.Equal(1, result.FilesMovedToTarget);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task LongFileName_IsHandled()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var takenTime = new DateTime(2024, 10, 31, 18, 0, 0);
        var longBaseName = new string('a', 180);
        var fileName = longBaseName + ".jpg";
        sourceDirectory.CreateFile(fileName, "image bytes", lastWriteUtc: takenTime);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(1, result.FilesMovedToTarget);
        Assert.Equal(0, result.ErrorCount);
    }

}
