using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using MetadataExtractor.Formats.Xmp;
using PictureSortAndDuplicateCleaner.Exif;
using XmpCore;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class ExifExtractorTests
{
    private const string ExifDate = "2023:05:15 10:20:30";
    private static readonly DateTime ExpectedDate = new(2023, 5, 15, 10, 20, 30, DateTimeKind.Unspecified);

    [Fact]
    public void ExifSubDateTimeOriginalExtractor_ParsesTagDateTimeOriginal()
    {
        var sub = new ExifSubIfdDirectory();
        sub.Set(ExifDirectoryBase.TagDateTimeOriginal, ExifDate);

        var result = new ExifSubDateTimeOriginalExtractor().TryExtract(new MetadataExtractor.Directory[] { sub });

        Assert.NotNull(result);
        Assert.Equal(ExpectedDate, result!.Date);
        Assert.Equal(ExifDate, result.RawValue);
    }

    [Fact]
    public void ExifSubDateTimeOriginalExtractor_ReturnsNullWhenAbsent()
    {
        var sub = new ExifSubIfdDirectory();
        var result = new ExifSubDateTimeOriginalExtractor().TryExtract(new MetadataExtractor.Directory[] { sub });
        Assert.Null(result);
    }

    [Fact]
    public void ExifSubDateTimeDigitizedExtractor_ParsesTagDateTimeDigitized()
    {
        var sub = new ExifSubIfdDirectory();
        sub.Set(ExifDirectoryBase.TagDateTimeDigitized, ExifDate);

        var result = new ExifSubDateTimeDigitizedExtractor().TryExtract(new MetadataExtractor.Directory[] { sub });

        Assert.NotNull(result);
        Assert.Equal(ExpectedDate, result!.Date);
    }

    [Fact]
    public void ExifIfd0DateTimeExtractor_ParsesTagDateTime()
    {
        var ifd0 = new ExifIfd0Directory();
        ifd0.Set(ExifDirectoryBase.TagDateTime, ExifDate);

        var result = new ExifIfd0DateTimeExtractor().TryExtract(new MetadataExtractor.Directory[] { ifd0 });

        Assert.NotNull(result);
        Assert.Equal(ExpectedDate, result!.Date);
    }

    [Fact]
    public void ExifIfd0DateTimeExtractor_ReturnsNullWhenDirectoryAbsent()
    {
        var result = new ExifIfd0DateTimeExtractor().TryExtract(Array.Empty<MetadataExtractor.Directory>());
        Assert.Null(result);
    }

    [Fact]
    public void QuickTimeCreationDateExtractor_ParsesTagCreated()
    {
        var qt = new QuickTimeMovieHeaderDirectory();
        qt.Set(QuickTimeMovieHeaderDirectory.TagCreated, ExifDate);

        var result = new QuickTimeCreationDateExtractor().TryExtract(new MetadataExtractor.Directory[] { qt });

        Assert.NotNull(result);
        Assert.Equal(ExpectedDate, result!.Date);
    }

    [Fact]
    public void QuickTimeCreationDateExtractor_ReturnsNullWhenAbsent()
    {
        var result = new QuickTimeCreationDateExtractor().TryExtract(Array.Empty<MetadataExtractor.Directory>());
        Assert.Null(result);
    }

    [Fact]
    public void XmpCreateDateExtractor_ParsesXmpCreateDate()
    {
        var meta = XmpMetaFactory.Create();
        meta.SetProperty("http://ns.adobe.com/xap/1.0/", "CreateDate", "2023-05-15T10:20:30");
        var xmp = new XmpDirectory();
        xmp.SetXmpMeta(meta);

        var result = new XmpCreateDateExtractor().TryExtract(new MetadataExtractor.Directory[] { xmp });

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2023, 5, 15, 10, 20, 30, DateTimeKind.Unspecified), result!.Date);
    }

    [Fact]
    public void XmpCreateDateExtractor_FallsBackToExifDateTimeOriginal()
    {
        var meta = XmpMetaFactory.Create();
        meta.SetProperty("http://ns.adobe.com/exif/1.0/", "DateTimeOriginal", "2024-01-02T03:04:05");
        var xmp = new XmpDirectory();
        xmp.SetXmpMeta(meta);

        var result = new XmpCreateDateExtractor().TryExtract(new MetadataExtractor.Directory[] { xmp });

        Assert.NotNull(result);
        Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Unspecified), result!.Date);
    }

    [Fact]
    public void XmpCreateDateExtractor_ReturnsNullWhenXmpMetaIsNull()
    {
        var xmp = new XmpDirectory();
        var result = new XmpCreateDateExtractor().TryExtract(new MetadataExtractor.Directory[] { xmp });
        Assert.Null(result);
    }
}
