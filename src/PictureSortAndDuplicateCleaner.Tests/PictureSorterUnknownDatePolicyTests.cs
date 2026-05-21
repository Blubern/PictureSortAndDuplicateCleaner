using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSorterUnknownDatePolicyTests
{
    [Fact]
    public async Task SkipAndCount_LeavesDatelessFileInSource()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        // Plain file with no EXIF metadata — strict Unknown policy treats it as dateless.
        var sourceFile = sourceDirectory.CreateFile("nodate.bin", "raw bytes with no exif");
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            unknownDatePolicy: UnknownDatePolicy.SkipAndCount);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(1, result.FilesWithoutDateSkipped);
        Assert.Equal(0, result.FilesMovedToTarget);
        Assert.True(fs.FileExists(sourceFile));
    }
}
