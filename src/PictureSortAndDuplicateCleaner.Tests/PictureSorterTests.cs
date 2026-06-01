using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSorterTests
{
    [Fact]
    public async Task StartPictureSortAsync_WithSourceFile_MovesFileToDateBasedTargetFolder()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var takenTime = new DateTime(2024, 5, 19, 10, 30, 0);
        var sourceFile = sourceDirectory.CreateFile(System.IO.Path.Combine("camera", "photo.jpg"), "image bytes", lastWriteUtc: takenTime);
        fs.SetCreationTime(sourceFile, takenTime.AddDays(-1));
        var progress = new TestProgress();
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);

        var result = await sorter.StartPictureSortAsync(parameter, progress, CancellationToken.None);

        var expectedTargetFile = System.IO.Path.Combine(
            targetDirectory.Path,
            takenTime.ToString("yyyy"),
            takenTime.ToString("MMMM"),
            takenTime.ToString("dd"),
            "photo.jpg");

        Assert.False(fs.FileExists(sourceFile));
        Assert.True(fs.FileExists(expectedTargetFile));
        Assert.False(fs.DirectoryExists(System.IO.Path.Combine(sourceDirectory.Path, "camera")));
        Assert.Contains(progress.Messages, message => message.Contains("target directory", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, result.SourceFilesFound);
        Assert.Equal(0, result.TargetFilesFound);
        Assert.Equal(1, result.FilesMovedToTarget);
        Assert.Equal(0, result.TotalFilesIgnored);
        Assert.Equal(0, result.DuplicateFilesMoved);
        Assert.Equal(0, result.AlreadyExistingFilesMoved);
    }

    [Fact]
    public async Task StartPictureSortAsync_WithDuplicateFiles_MovesOneFileToTargetAndOneToDuplicateFolder()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var takenTime = new DateTime(2024, 5, 19, 10, 30, 0);
        var firstSourceFile = sourceDirectory.CreateFile("photo-a.jpg", "same image bytes", lastWriteUtc: takenTime);
        var secondSourceFile = sourceDirectory.CreateFile("photo-b.jpg", "same image bytes", lastWriteUtc: takenTime);
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            2,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        var sortedFiles = fs.EnumerateFiles(targetDirectory.Path, "*.jpg", SearchOption.AllDirectories).ToArray();
        var duplicateFiles = fs.EnumerateFiles(
            System.IO.Path.Combine(sourceDirectory.Path, "!Duplicate"),
            "*.jpg",
            SearchOption.AllDirectories).ToArray();

        Assert.Single(sortedFiles);
        Assert.Single(duplicateFiles);
        Assert.False(fs.FileExists(firstSourceFile) && fs.FileExists(secondSourceFile));
        Assert.Equal(2, result.SourceFilesFound);
        Assert.Equal(0, result.TargetFilesFound);
        Assert.Equal(1, result.FilesMovedToTarget);
        Assert.Equal(1, result.SourceFilesIgnored);
        Assert.Equal(1, result.TotalFilesIgnored);
        Assert.Equal(1, result.DuplicateFilesMoved);
        Assert.Equal(0, result.AlreadyExistingFilesMoved);
    }

    [Fact]
    public async Task StartPictureSortAsync_WhenSameHashExistsInTarget_MovesSourceFileToAlreadyExistingFolder()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var sourceFile = sourceDirectory.CreateFile("incoming.jpg", "already imported image bytes");
        var targetFile = targetDirectory.CreateFile(System.IO.Path.Combine("archive", "existing.jpg"), "already imported image bytes");
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: true);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        var alreadyExistingFiles = fs.EnumerateFiles(
            System.IO.Path.Combine(sourceDirectory.Path, "!ExistsInTarget"),
            "incoming.jpg",
            SearchOption.AllDirectories).ToArray();

        Assert.Single(alreadyExistingFiles);
        Assert.False(fs.FileExists(sourceFile));
        Assert.True(fs.FileExists(targetFile));
        Assert.Empty(fs.EnumerateFiles(targetDirectory.Path, "incoming.jpg", SearchOption.AllDirectories));
        Assert.Equal(1, result.SourceFilesFound);
        Assert.Equal(1, result.TargetFilesFound);
        Assert.Equal(0, result.FilesMovedToTarget);
        Assert.Equal(1, result.SourceFilesIgnored);
        Assert.Equal(1, result.TotalFilesIgnored);
        Assert.Equal(0, result.DuplicateFilesMoved);
        Assert.Equal(1, result.AlreadyExistingFilesMoved);
    }

    [Fact]
    public async Task StartPictureSortAsync_WhenDuplicatesExistOnlyInTarget_MovesThemToSourceDuplicateInTargetFolder()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var firstTargetFile = targetDirectory.CreateFile("archive/first.jpg", "same content");
        var secondTargetFile = targetDirectory.CreateFile("archive/second.jpg", "same content");
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!DuplicateInSource",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: true,
            duplicateInTargetFolderName: "!DuplicateInTarget");

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        var targetDuplicateFiles = fs.EnumerateFiles(
            System.IO.Path.Combine(sourceDirectory.Path, "!DuplicateInTarget"),
            "*.jpg",
            SearchOption.AllDirectories).ToArray();

        var remainingTargetFiles = fs.EnumerateFiles(targetDirectory.Path, "*.jpg", SearchOption.AllDirectories).ToArray();

        Assert.Single(targetDuplicateFiles);
        Assert.Single(remainingTargetFiles);
        Assert.Equal(1, result.DuplicateFilesMoved);
    }

    [Fact]
    public async Task StartPictureSortAsync_WithMultipleSourceDirectories_MovesAllFilesIntoTarget()
    {
        var fs = new InMemoryFileSystem();
        var firstSource = new InMemoryDirectory(fs);
        var secondSource = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var takenTime = new DateTime(2024, 5, 19, 10, 30, 0);
        firstSource.CreateFile("alpha.jpg", "alpha bytes", lastWriteUtc: takenTime);
        secondSource.CreateFile("beta.jpg", "beta bytes", lastWriteUtc: takenTime);
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { firstSource.Path, secondSource.Path },
            targetDirectory.Path,
            2,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        var movedFiles = fs.EnumerateFiles(targetDirectory.Path, "*.jpg", SearchOption.AllDirectories).ToArray();
        Assert.Equal(2, movedFiles.Length);
        Assert.Contains(movedFiles, file => file.EndsWith("alpha.jpg", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(movedFiles, file => file.EndsWith("beta.jpg", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, result.SourceFilesFound);
        Assert.Equal(2, result.FilesMovedToTarget);
        Assert.Equal(0, result.ErrorCount);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public async Task StartPictureSortAsync_WithThreeDuplicates_MovesOneToTargetAndTwoToDuplicateFolder()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var takenTime = new DateTime(2024, 5, 19, 10, 30, 0);
        sourceDirectory.CreateFile("a.jpg", "shared bytes", lastWriteUtc: takenTime);
        sourceDirectory.CreateFile("b.jpg", "shared bytes", lastWriteUtc: takenTime);
        sourceDirectory.CreateFile("c.jpg", "shared bytes", lastWriteUtc: takenTime);
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            2,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        var sortedFiles = fs.EnumerateFiles(targetDirectory.Path, "*.jpg", SearchOption.AllDirectories).ToArray();
        var duplicateFiles = fs.EnumerateFiles(
            System.IO.Path.Combine(sourceDirectory.Path, "!Duplicate"),
            "*.jpg",
            SearchOption.AllDirectories).ToArray();

        Assert.Single(sortedFiles);
        Assert.Equal(2, duplicateFiles.Length);
        Assert.Equal(3, result.SourceFilesFound);
        Assert.Equal(1, result.FilesMovedToTarget);
        Assert.Equal(2, result.DuplicateFilesMoved);
        Assert.Equal(2, result.SourceFilesIgnored);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task StartPictureSortAsync_WhenCancelledBeforeStart_ThrowsOperationCanceledException()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        sourceDirectory.CreateFile("photo.jpg", "bytes");
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sorter.StartPictureSortAsync(parameter, new TestProgress(), cancellationTokenSource.Token));

        Assert.Empty(fs.EnumerateFiles(targetDirectory.Path, "*.jpg", SearchOption.AllDirectories));
    }
}