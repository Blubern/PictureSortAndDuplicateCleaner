using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;
using PictureSortAndDuplicateCleaner.FolderStructure;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSorterFolderTemplateTests
{
    [Fact]
    public async Task StartPictureSortAsync_WithCustomTemplate_PlacesFileAccordingToTemplate()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var takenTime = new DateTime(2024, 5, 19, 10, 30, 0);
        sourceDirectory.CreateFile("photo.jpg", "image bytes", lastWriteUtc: takenTime);

        var template = FolderStructureTemplate.Parse("{yyyy}/{Quarter}/{MM}");
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: null,
            journalFilePath: null,
            folderTemplate: template);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        var expected = System.IO.Path.Combine(targetDirectory.Path, "2024", "Q2", "05", "photo.jpg");
        Assert.True(fs.FileExists(expected), $"Expected file at {expected}");
        Assert.Equal(1, result.FilesMovedToTarget);
    }
}
