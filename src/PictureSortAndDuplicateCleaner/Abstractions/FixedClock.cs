namespace PictureSortAndDuplicateCleaner.Abstractions;

/// <summary>
/// Deterministic clock for tests. Increment <see cref="UtcNow"/> manually
/// via <see cref="Advance"/> to simulate time passing between operations.
/// </summary>
public sealed class FixedClock : IClock
{
    public FixedClock(DateTime initialUtcNow)
    {
        if (initialUtcNow.Kind != DateTimeKind.Utc)
        {
            initialUtcNow = DateTime.SpecifyKind(initialUtcNow, DateTimeKind.Utc);
        }
        UtcNow = initialUtcNow;
    }

    public DateTime UtcNow { get; private set; }

    public void Advance(TimeSpan delta) => UtcNow = UtcNow.Add(delta);
    public void Set(DateTime utcNow) => UtcNow = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
}
