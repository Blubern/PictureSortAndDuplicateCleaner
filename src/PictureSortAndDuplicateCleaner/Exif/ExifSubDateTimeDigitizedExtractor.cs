using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace PictureSortAndDuplicateCleaner.Exif;

public sealed class ExifSubDateTimeDigitizedExtractor : IDateExtractor
{
    public string Name => "ExifSubIfd.DateTimeDigitized";

    public DateExtractionResult? TryExtract(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        var dir = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var raw = dir?.GetDescription(ExifDirectoryBase.TagDateTimeDigitized);
        var parsed = ExifDateParsing.TryParse(raw);
        return parsed.HasValue ? new DateExtractionResult(parsed.Value, raw!) : null;
    }
}
