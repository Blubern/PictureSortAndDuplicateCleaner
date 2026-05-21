using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace PictureSortAndDuplicateCleaner.Exif;

public sealed class ExifIfd0DateTimeExtractor : IDateExtractor
{
    public string Name => "ExifIfd0.DateTime";

    public DateExtractionResult? TryExtract(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        var dir = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
        var raw = dir?.GetDescription(ExifDirectoryBase.TagDateTime);
        var parsed = ExifDateParsing.TryParse(raw);
        return parsed.HasValue ? new DateExtractionResult(parsed.Value, raw!) : null;
    }
}
