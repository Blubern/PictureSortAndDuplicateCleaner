using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace PictureSortAndDuplicateCleaner.Exif;

public sealed class ExifSubDateTimeOriginalExtractor : IDateExtractor
{
    public string Name => "ExifSubIfd.DateTimeOriginal";

    public DateExtractionResult? TryExtract(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        var dir = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        var raw = dir?.GetDescription(ExifDirectoryBase.TagDateTimeOriginal);
        var parsed = ExifDateParsing.TryParse(raw);
        return parsed.HasValue ? new DateExtractionResult(parsed.Value, raw!) : null;
    }
}
