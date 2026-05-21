namespace PictureSortAndDuplicateCleaner.Abstractions;

/// <summary>
/// Abstraction over time so journal timestamps and other now-dependent logic
/// can be made deterministic in tests via <see cref="FixedClock"/>.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
