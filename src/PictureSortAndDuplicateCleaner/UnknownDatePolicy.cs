namespace PictureSortAndDuplicateCleaner;

/// <summary>
/// Controls how files without a determinable taken-time (no EXIF date, no usable
/// file timestamps) are handled when moving to the target.
/// </summary>
public enum UnknownDatePolicy
{
    /// <summary>
    /// Default. Files land in an <c>Unknown/</c> subfolder of the target — unchanged legacy behavior.
    /// </summary>
    MoveToUnknownFolder = 0,

    /// <summary>
    /// File stays in the source directory and is counted in <c>PictureSortResult.FilesWithoutDateSkipped</c>.
    /// </summary>
    SkipAndCount = 1,

    /// <summary>
    /// File stays in the source directory and is counted as an error.
    /// </summary>
    Fail = 2,
}
