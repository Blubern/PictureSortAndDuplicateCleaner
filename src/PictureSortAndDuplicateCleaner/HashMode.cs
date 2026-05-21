namespace PictureSortAndDuplicateCleaner;

/// <summary>
/// Selects how PicSorter fingerprints a file for duplicate detection.
/// </summary>
public enum HashMode
{
    /// <summary>
    /// Hash the raw bytes of the file with XxHash3. Default and historical behavior.
    /// Two files differing only in EXIF metadata are NOT detected as duplicates.
    /// </summary>
    File = 0,

    /// <summary>
    /// Decode the image with SkiaSharp, normalize to a fixed 256x256 Rgba8888 pixel buffer
    /// and hash that buffer with XxHash3. Two images with identical pixel content but
    /// different EXIF/XMP metadata produce the same hash. Resulting hashes are prefixed
    /// with <c>p:</c>. Falls back to <see cref="File"/> on a per-file basis when the
    /// image cannot be decoded (HEIC, RAW, non-image, corrupt).
    /// </summary>
    Pixel = 1,
}
