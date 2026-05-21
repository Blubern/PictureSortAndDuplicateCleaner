using PictureSortAndDuplicateCleaner.Exif;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class ExifDateParsingTests
{
    [Theory]
    [InlineData("2024:05:19 10:30:00")]
    [InlineData("2024:05:19 10:30:00.500")]
    [InlineData("2024-05-19 10:30:00")]
    [InlineData("2024-05-19T10:30:00")]
    [InlineData("2024-05-19T10:30:00Z")]
    [InlineData("2024-05-19T10:30:00+02:00")]
    [InlineData("2024-05-19T10:30:00.123")]
    [InlineData("2024-05-19T10:30:00.123Z")]
    public void TryParse_AcceptsSupportedFormats(string raw)
    {
        var parsed = ExifDateParsing.TryParse(raw);

        Assert.NotNull(parsed);
        Assert.Equal(DateTimeKind.Utc, parsed!.Value.Kind);
        Assert.Equal(2024, parsed.Value.Year);
        Assert.Equal(5, parsed.Value.Month);
        Assert.Equal(19, parsed.Value.Day);
    }

    [Fact]
    public void TryParse_NormalizesTimezoneOffsetToUtc()
    {
        var parsed = ExifDateParsing.TryParse("2024-05-19T10:30:00+02:00");

        Assert.NotNull(parsed);
        Assert.Equal(8, parsed!.Value.Hour); // 10:30 +02 → 08:30 UTC
        Assert.Equal(30, parsed.Value.Minute);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a date")]
    [InlineData("0000:00:00 00:00:00")]
    [InlineData("garbage")]
    public void TryParse_RejectsInvalidInputs(string? raw)
    {
        Assert.Null(ExifDateParsing.TryParse(raw));
    }

    [Fact]
    public void TryParse_TrimsWhitespace()
    {
        var parsed = ExifDateParsing.TryParse("  2024:05:19 10:30:00  ");

        Assert.NotNull(parsed);
        Assert.Equal(new DateTime(2024, 5, 19, 10, 30, 0, DateTimeKind.Utc), parsed!.Value);
    }
}
