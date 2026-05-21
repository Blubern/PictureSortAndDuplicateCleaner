namespace PictureSortAndDuplicateCleaner.Tests;

internal sealed class TestProgress : IProgress<string>
{
    private readonly object _lock = new();
    private readonly List<string> _messages = new();

    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_lock)
            {
                return _messages.ToList();
            }
        }
    }

    public void Report(string value)
    {
        lock (_lock)
        {
            _messages.Add(value);
        }
    }
}