using System.Globalization;
using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.FolderStructure;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class FolderStructureTemplateNullDateTests
{
    private static FileInventoryResult FileWithoutDate() =>
        new(
            "photo.jpg",
            "source",
            "hash",
            creationTime: null,
            lastWriteTime: null,
            lastAccessTime: null,
            originalDateAsString: null,
            originalDate: null,
            originalFileName: "photo.jpg");

    [Theory]
    [InlineData("{yyyy}/{MM}")]
    [InlineData("{yyyy}/{Quarter}")]
    [InlineData("{Weekday}/{WeekOfYear}")]
    [InlineData("archive/{yyyy}-{MM}-{dd}")]
    [InlineData("{yyyy}/{MMMM}/{dd}")]
    public void Build_WithoutDate_AlwaysReturnsUnknownRegardlessOfTemplate(string template)
    {
        var parsed = FolderStructureTemplate.Parse(template);
        var built = parsed.Build(FileWithoutDate(), CultureInfo.InvariantCulture);

        Assert.Equal(FolderStructureTemplate.UnknownDateFolder, built);
    }
}
