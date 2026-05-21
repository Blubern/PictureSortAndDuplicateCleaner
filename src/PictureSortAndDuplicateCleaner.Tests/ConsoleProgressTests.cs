using PictureSortAndDuplicateCleaner.Cmd;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class ConsoleProgressTests
{
    [Fact]
    public async Task Report_WritesValueToSerilogAtDebugLevel()
    {
        var sink = new CapturingSink();
        var previousLogger = Log.Logger;
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(sink)
            .CreateLogger();
        try
        {
            IProgress<string> progress = new ConsoleProgress();
            progress.Report("hello world");
            // Progress<T>.OnReport is dispatched via SynchronizationContext/ThreadPool — yield to let it run.
            await WaitForAsync(() => sink.Events.Count > 0);
        }
        finally
        {
            Log.Logger = previousLogger;
        }

        var entry = Assert.Single(sink.Events);
        Assert.Equal(LogEventLevel.Debug, entry.Level);
        Assert.Equal("hello world", entry.RenderMessage());
    }

    private static async Task WaitForAsync(Func<bool> predicate)
    {
        for (var i = 0; i < 100 && !predicate(); i++)
        {
            await Task.Delay(10);
        }
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent)
        {
            lock (Events)
            {
                Events.Add(logEvent);
            }
        }
    }
}
