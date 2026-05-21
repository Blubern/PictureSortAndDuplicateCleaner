namespace PictureSortAndDuplicateCleaner.Abstractions;

/// <summary>
/// Production clock that returns <see cref="DateTime.UtcNow"/>.
/// </summary>
public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();
    public DateTime UtcNow => DateTime.UtcNow;
}
