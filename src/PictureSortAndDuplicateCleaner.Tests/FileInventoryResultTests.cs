using PictureSortAndDuplicateCleaner;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class FileInventoryResultTests
{
    [Fact]
    public void CalculatedTakenTime_UsesOriginalDateFirst()
    {
        var creationTime = new DateTime(2021, 1, 2, 3, 4, 5);
        var lastWriteTime = new DateTime(2022, 2, 3, 4, 5, 6);
        var originalDate = new DateTime(2023, 3, 4, 5, 6, 7);

        var result = new FileInventoryResult(
            "photo.jpg",
            "source",
            "hash",
            creationTime,
            lastWriteTime,
            DateTime.MinValue,
            "2023:03:04 05:06:07",
            originalDate,
            "photo.jpg");

        Assert.Equal(originalDate, result.CalculatedTakenTime);
    }

    [Fact]
    public void CalculatedTakenTime_WithoutOriginalDate_FallsBackToLastWriteTime()
    {
        var creationTime = new DateTime(2021, 1, 2, 3, 4, 5);
        var lastWriteTime = new DateTime(2022, 2, 3, 4, 5, 6);

        var result = new FileInventoryResult(
            "photo.jpg",
            "source",
            "hash",
            creationTime,
            lastWriteTime,
            DateTime.MinValue,
            null,
            null,
            "photo.jpg");

        Assert.Equal(lastWriteTime, result.CalculatedTakenTime);
    }

    [Fact]
    public void GetDateFolderPart_WithDate_ReturnsDateBasedPath()
    {
        var originalDate = new DateTime(2023, 12, 24, 5, 6, 7);
        var result = new FileInventoryResult(
            "photo.jpg",
            "source",
            "hash",
            DateTime.MinValue,
            DateTime.MinValue,
            DateTime.MinValue,
            "2023:12:24 05:06:07",
            originalDate,
            "photo.jpg");

        var expected = string.Join(
            Path.DirectorySeparatorChar,
            originalDate.ToString("yyyy"),
            originalDate.ToString("MMMM"),
            originalDate.ToString("dd"));

#pragma warning disable CS0618 // GetDateFolderPart is obsolete but still tested for backwards compatibility.
        Assert.Equal(expected, result.GetDateFolderPart());
#pragma warning restore CS0618
    }

    [Fact]
    public void GetDateFolderPart_WithoutAnyDate_ReturnsUnknown()
    {
        var result = new FileInventoryResult(
            "photo.jpg",
            "source",
            "hash",
            null,
            null,
            null,
            null,
            null,
            "photo.jpg");

#pragma warning disable CS0618 // GetDateFolderPart is obsolete but still tested for backwards compatibility.
        Assert.Equal("Unknown", result.GetDateFolderPart());
#pragma warning restore CS0618
    }
}