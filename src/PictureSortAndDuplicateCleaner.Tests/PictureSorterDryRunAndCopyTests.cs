using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSorterDryRunAndCopyTests
{
    [Fact]
    public async Task DryRun_LeavesSourceUntouchedAndCreatesNoTargetFiles()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var sourceFile = sourceDirectory.CreateFile("photo.jpg", "image bytes", lastWriteUtc: new DateTime(2024, 1, 2, 3, 4, 5));
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

        Assert.True(fs.FileExists(sourceFile), "dry-run must leave source file in place");
        Assert.Empty(fs.EnumerateFiles(targetDirectory.Path, "*", SearchOption.AllDirectories));
        Assert.Equal(1, result.FilesMovedToTarget);
    }

    [Fact]
    public async Task CopyMode_LeavesSourceAndWritesCopyToTarget()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var takenTime = new DateTime(2024, 6, 7, 8, 9, 10);
        var sourceFile = sourceDirectory.CreateFile("photo.jpg", "image bytes", lastWriteUtc: takenTime);
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

        Assert.True(fs.FileExists(sourceFile), "copy mode must keep the source file");
        var expectedTarget = Path.Combine(
            targetDirectory.Path,
            takenTime.ToString("yyyy"),
            takenTime.ToString("MMMM"),
            takenTime.ToString("dd"),
            "photo.jpg");
        Assert.True(fs.FileExists(expectedTarget), $"expected copy at {expectedTarget}");
        Assert.Equal(1, result.FilesMovedToTarget);
    }
}
