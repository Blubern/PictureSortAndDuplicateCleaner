using MetadataExtractor;

namespace PictureSortAndDuplicateCleaner.Exif;

/// <summary>
/// Runs an ordered chain of <see cref="IDateExtractor"/>s and returns the first hit, plus
/// the name of the source for diagnostics.
/// </summary>
public sealed class DateExtractorChain
{
    public static readonly DateExtractorChain Default = new(new IDateExtractor[]
    {
        new ExifSubDateTimeOriginalExtractor(),
        new ExifSubDateTimeDigitizedExtractor(),
        new ExifIfd0DateTimeExtractor(),
        new QuickTimeCreationDateExtractor(),
        new XmpCreateDateExtractor(),
    });

    private readonly IReadOnlyList<IDateExtractor> _extractors;

    public DateExtractorChain(IReadOnlyList<IDateExtractor> extractors)
    {
        _extractors = extractors ?? throw new ArgumentNullException(nameof(extractors));
    }

    public (DateTime? Date, string? RawValue, string? Source) Extract(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        foreach (var extractor in _extractors)
        {
            var result = extractor.TryExtract(directories);
            if (result is not null)
            {
                return (result.Date, result.RawValue, extractor.Name);
            }
        }
        return (null, null, null);
    }
}
