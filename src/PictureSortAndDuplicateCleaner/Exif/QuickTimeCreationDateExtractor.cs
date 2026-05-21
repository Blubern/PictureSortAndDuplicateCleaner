using MetadataExtractor;
using MetadataExtractor.Formats.QuickTime;

namespace PictureSortAndDuplicateCleaner.Exif;

public sealed class QuickTimeCreationDateExtractor : IDateExtractor
{
    public string Name => "QuickTime.CreationDate";

    public DateExtractionResult? TryExtract(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        foreach (var dir in directories.OfType<QuickTimeMovieHeaderDirectory>())
        {
            var raw = dir.GetDescription(QuickTimeMovieHeaderDirectory.TagCreated);
            var parsed = ExifDateParsing.TryParse(raw);
            if (parsed.HasValue)
            {
                return new DateExtractionResult(parsed.Value, raw!);
            }
        }
        return null;
    }
}
