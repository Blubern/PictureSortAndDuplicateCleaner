namespace PictureSortAndDuplicateCleaner;

/// <summary>
/// Controls how strictly two files are considered duplicates of each other.
/// </summary>
public enum DuplicateVerification
{
    /// <summary>
    /// Default. Files are considered identical when their content hash matches.
    /// </summary>
    HashOnly = 0,

    /// <summary>
    /// Files are only considered identical when both content hash AND byte length match.
    /// Defends against the (astronomically unlikely) case of a hash collision and against
    /// truncated files whose hash happens to coincide with the original. When file length
    /// information is unavailable (length == 0 on either side) the verification falls back
    /// to hash-only for that pair so legacy data is never silently mishandled.
    /// </summary>
    HashPlusSize = 1
}
