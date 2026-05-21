namespace PictureSortAndDuplicateCleaner;

/// <summary>
/// Whether the sorter moves (default, destructive) or copies (non-destructive) files
/// from source to target.
/// </summary>
public enum OperationMode
{
    Move = 0,
    Copy = 1,
}
