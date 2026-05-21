using System.Globalization;
using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.FolderStructure;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class FolderStructureTemplateTests
{
    private static FileInventoryResult FileAt(DateTime date)
    {
        return new FileInventoryResult(
            "photo.jpg",
            "source",
            "hash",
            creationTime: null,
            lastWriteTime: null,
            lastAccessTime: null,
            originalDateAsString: null,
            originalDate: date,
            originalFileName: "photo.jpg");
    }

    private static FileInventoryResult FileWithoutDate()
    {
        return new FileInventoryResult(
            "photo.jpg",
            "source",
            "hash",
            creationTime: null,
            lastWriteTime: null,
            lastAccessTime: null,
            originalDateAsString: null,
            originalDate: null,
            originalFileName: "photo.jpg");
    }

    [Fact]
    public void Default_MatchesLegacyDateFolderPart()
    {
        var date = new DateTime(2024, 5, 19, 10, 30, 0);
        var file = FileAt(date);

        var built = FolderStructureTemplate.Default.Build(file, CultureInfo.GetCultureInfo("en-US"));

        var expected = string.Join(Path.DirectorySeparatorChar, "2024", "May", "19");
        Assert.Equal(expected, built);
    }

    [Fact]
    public void Build_WithoutDate_ReturnsUnknown()
    {
        var file = FileWithoutDate();

        var built = FolderStructureTemplate.Default.Build(file);

        Assert.Equal("Unknown", built);
    }

    [Fact]
    public void Parse_AcceptsSlashAndBackslashSeparators()
    {
        var date = new DateTime(2024, 5, 19);
        var fileA = FileAt(date);
        var fileB = FileAt(date);

        var withSlash = FolderStructureTemplate.Parse("{yyyy}/{MM}").Build(fileA, CultureInfo.InvariantCulture);
        var withBackslash = FolderStructureTemplate.Parse(@"{yyyy}\{MM}").Build(fileB, CultureInfo.InvariantCulture);

        Assert.Equal(withSlash, withBackslash);
        Assert.Equal(string.Join(Path.DirectorySeparatorChar, "2024", "05"), withSlash);
    }

    [Fact]
    public void Parse_AllowsLiteralsAroundTokens()
    {
        var file = FileAt(new DateTime(2024, 5, 19));

        var built = FolderStructureTemplate.Parse("photos-{yyyy}/m{MM}").Build(file, CultureInfo.InvariantCulture);

        Assert.Equal(string.Join(Path.DirectorySeparatorChar, "photos-2024", "m05"), built);
    }

    [Theory]
    [InlineData(1, "Q1")]
    [InlineData(3, "Q1")]
    [InlineData(4, "Q2")]
    [InlineData(7, "Q3")]
    [InlineData(12, "Q4")]
    public void Build_QuarterToken(int month, string expected)
    {
        var file = FileAt(new DateTime(2024, month, 15));

        var built = FolderStructureTemplate.Parse("{Quarter}").Build(file, CultureInfo.InvariantCulture);

        Assert.Equal(expected, built);
    }

    [Fact]
    public void Build_WeekOfYearToken_ReturnsIso8601Week()
    {
        // 2024-01-01 is in ISO week 1 of 2024 (Monday).
        var file = FileAt(new DateTime(2024, 1, 1));

        var built = FolderStructureTemplate.Parse("{WeekOfYear}").Build(file, CultureInfo.InvariantCulture);

        Assert.Equal("01", built);
    }

    [Fact]
    public void Build_WeekdayToken_UsesCulture()
    {
        var file = FileAt(new DateTime(2024, 5, 19)); // Sunday

        var en = FolderStructureTemplate.Parse("{Weekday}").Build(file, CultureInfo.GetCultureInfo("en-US"));
        var de = FolderStructureTemplate.Parse("{Weekday}").Build(file, CultureInfo.GetCultureInfo("de-DE"));

        Assert.Equal("Sunday", en);
        Assert.Equal("Sonntag", de);
    }

    [Fact]
    public void Parse_EmptyTemplate_Throws()
    {
        Assert.Throws<ArgumentException>(() => FolderStructureTemplate.Parse("   "));
    }

    [Fact]
    public void Parse_NullTemplate_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FolderStructureTemplate.Parse(null!));
    }

    [Fact]
    public void Parse_UnknownToken_ThrowsWithPosition()
    {
        var ex = Assert.Throws<ArgumentException>(() => FolderStructureTemplate.Parse("{yyyy}/{Bogus}/{dd}"));

        Assert.Contains("{Bogus}", ex.Message);
        Assert.Contains("position 7", ex.Message);
    }

    [Fact]
    public void Parse_UnclosedToken_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => FolderStructureTemplate.Parse("{yyyy/{MM}"));

        Assert.Contains("unclosed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_EmptyToken_Throws()
    {
        Assert.Throws<ArgumentException>(() => FolderStructureTemplate.Parse("{yyyy}/{}"));
    }

    [Fact]
    public void Parse_ParentDirectorySegment_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => FolderStructureTemplate.Parse("{yyyy}/../{MM}"));

        Assert.Contains("'.'", ex.Message);
    }

    [Fact]
    public void Parse_InvalidPathCharacter_Throws()
    {
        Assert.Throws<ArgumentException>(() => FolderStructureTemplate.Parse("{yyyy}/foo<bar>"));
    }

    [Fact]
    public void Parse_LeadingAndTrailingSlashesAreIgnored()
    {
        var file = FileAt(new DateTime(2024, 5, 19));

        var built = FolderStructureTemplate.Parse("/{yyyy}/{MM}/").Build(file, CultureInfo.InvariantCulture);

        Assert.Equal(string.Join(Path.DirectorySeparatorChar, "2024", "05"), built);
    }
}
