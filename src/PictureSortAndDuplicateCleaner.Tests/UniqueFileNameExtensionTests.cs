using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class UniqueFileNameExtensionTests
{
    [Fact]
    public void CheckIfFileExistsWhenYesIterateANumberOnTheEnd_WhenFileDoesNotExist_ReturnsOriginalPath()
    {
        var fs = new InMemoryFileSystem();
        var directory = new InMemoryDirectory(fs);
        var filePath = System.IO.Path.Combine(directory.Path, "photo.jpg");

        var result = filePath.CheckIfFileExistsWhenYesIterateANumberOnTheEnd(fs);

        Assert.Equal(filePath, result);
    }

    [Fact]
    public void CheckIfFileExistsWhenYesIterateANumberOnTheEnd_WhenFileExists_AddsCounterSuffix()
    {
        var fs = new InMemoryFileSystem();
        var directory = new InMemoryDirectory(fs);
        var filePath = directory.CreateFile("photo.jpg");

        var result = filePath.CheckIfFileExistsWhenYesIterateANumberOnTheEnd(fs);

        Assert.Equal(System.IO.Path.Combine(directory.Path, "photo_0.jpg"), result);
    }

    [Fact]
    public void CheckIfFileExistsWhenYesIterateANumberOnTheEnd_WhenCounterPathExists_IncrementsCounter()
    {
        var fs = new InMemoryFileSystem();
        var directory = new InMemoryDirectory(fs);
        var filePath = directory.CreateFile("photo.jpg");
        directory.CreateFile("photo_0.jpg");

        var result = filePath.CheckIfFileExistsWhenYesIterateANumberOnTheEnd(fs);

        Assert.Equal(System.IO.Path.Combine(directory.Path, "photo_1.jpg"), result);
    }

    [Fact]
    public void CheckIfFileExistsWhenYesIterateANumberOnTheEnd_ThrowsWhenAttemptsAreExhausted()
    {
        var fs = new InMemoryFileSystem();
        var directory = new InMemoryDirectory(fs);
        var filePath = directory.CreateFile("photo.jpg");
        directory.CreateFile("photo_0.jpg");
        directory.CreateFile("photo_1.jpg");

        var ex = Assert.Throws<IOException>(() => filePath.CheckIfFileExistsWhenYesIterateANumberOnTheEnd(fs, maxAttempts: 2));

        Assert.Contains("after 2 attempts", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
