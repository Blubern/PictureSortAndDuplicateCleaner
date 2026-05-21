using PictureSortAndDuplicateCleaner.Exif;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class DateExtractorChainTests
{
    private static readonly DateTime EarliestDate = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MiddleDate = new(2022, 6, 6, 6, 6, 6, DateTimeKind.Utc);
    private static readonly DateTime LatestDate = new(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    [Fact]
    public void Extract_WhenMultipleExtractorsMatch_ReturnsFirstHit()
    {
        var chain = new DateExtractorChain(new IDateExtractor[]
        {
            new FakeExtractor("First", new DateExtractionResult(EarliestDate, "first-raw")),
            new FakeExtractor("Second", new DateExtractionResult(LatestDate, "second-raw")),
        });

        var (date, raw, source) = chain.Extract(Array.Empty<MetadataExtractor.Directory>());

        Assert.Equal(EarliestDate, date);
        Assert.Equal("first-raw", raw);
        Assert.Equal("First", source);
    }

    [Fact]
    public void Extract_SkipsExtractorsThatReturnNullAndReturnsFirstNonNull()
    {
        var chain = new DateExtractorChain(new IDateExtractor[]
        {
            new FakeExtractor("First", null),
            new FakeExtractor("Second", null),
            new FakeExtractor("Third", new DateExtractionResult(MiddleDate, "third-raw")),
            new FakeExtractor("Fourth", new DateExtractionResult(LatestDate, "fourth-raw")),
        });

        var (date, raw, source) = chain.Extract(Array.Empty<MetadataExtractor.Directory>());

        Assert.Equal(MiddleDate, date);
        Assert.Equal("third-raw", raw);
        Assert.Equal("Third", source);
    }

    [Fact]
    public void Extract_WhenNoExtractorMatches_ReturnsAllNull()
    {
        var chain = new DateExtractorChain(new IDateExtractor[]
        {
            new FakeExtractor("First", null),
            new FakeExtractor("Second", null),
        });

        var (date, raw, source) = chain.Extract(Array.Empty<MetadataExtractor.Directory>());

        Assert.Null(date);
        Assert.Null(raw);
        Assert.Null(source);
    }

    [Fact]
    public void Default_OrdersOriginalBeforeDigitizedBeforeIfd0BeforeQuickTimeBeforeXmp()
    {
        // Reflect the implementation-defined priority order using the extractor type names.
        var defaultChain = DateExtractorChain.Default;
        var extractorsField = typeof(DateExtractorChain).GetField(
            "_extractors",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var extractors = (IReadOnlyList<IDateExtractor>)extractorsField!.GetValue(defaultChain)!;

        var names = extractors.Select(e => e.GetType().Name).ToArray();

        Assert.Equal(new[]
        {
            "ExifSubDateTimeOriginalExtractor",
            "ExifSubDateTimeDigitizedExtractor",
            "ExifIfd0DateTimeExtractor",
            "QuickTimeCreationDateExtractor",
            "XmpCreateDateExtractor",
        }, names);
    }

    private sealed class FakeExtractor : IDateExtractor
    {
        private readonly DateExtractionResult? _result;
        public FakeExtractor(string name, DateExtractionResult? result)
        {
            Name = name;
            _result = result;
        }

        public string Name { get; }

        public DateExtractionResult? TryExtract(IReadOnlyList<MetadataExtractor.Directory> directories) => _result;
    }
}
