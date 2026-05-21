namespace PictureSortAndDuplicateCleaner;

/// <summary>
/// A single file-level failure captured during a sort run. Surfaced via
/// <see cref="PictureSortResult.Errors"/> so callers can retry or surface
/// the affected files instead of only seeing an aggregated count.
/// </summary>
public sealed record PictureSortError(string SourcePath, string Reason, string Message);
