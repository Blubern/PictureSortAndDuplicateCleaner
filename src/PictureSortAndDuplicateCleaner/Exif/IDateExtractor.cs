using MetadataExtractor;

namespace PictureSortAndDuplicateCleaner.Exif;

/// <summary>
/// A single best-effort source for extracting an original capture timestamp from a file''s
/// metadata. Returns <c>null</c> when this source has no usable value for the given file.
/// </summary>
public interface IDateExtractor
{
    string Name { get; }

    DateExtractionResult? TryExtract(IReadOnlyList<MetadataExtractor.Directory> directories);
}
