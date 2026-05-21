using PictureSortAndDuplicateCleaner.Events;

namespace PictureSortAndDuplicateCleaner.Tests;

internal sealed class TestEventProgress : IProgress<PictureSortEvent>
{
    private readonly List<PictureSortEvent> _captured = new();
    private readonly object _gate = new();

    public IReadOnlyList<PictureSortEvent> Captured
    {
        get { lock (_gate) { return _captured.ToArray(); } }
    }

    public void Report(PictureSortEvent value)
    {
        lock (_gate)
        {
            _captured.Add(value);
        }
    }
}
