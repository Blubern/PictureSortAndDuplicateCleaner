using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;
using PictureSortAndDuplicateCleaner.Events;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSorterEventTests
{
    [Fact]
    public async Task EmitsTypedEvents_ForSortStartCompleteAndFileMoved()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        sourceDirectory.CreateFile("photo.jpg", "data", lastWriteUtc: new DateTime(2023, 2, 3, 4, 5, 6));
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);
        var captured = new List<PictureSortEvent>();
        var sink = new Progress<PictureSortEvent>(e => { lock (captured) { captured.Add(e); } });

        await sorter.StartPictureSortAsync(parameter, new TestProgress(), sink, CancellationToken.None);

        // Progress<T> dispatches on the captured synchronization context; give callbacks a beat to land.
        await Task.Delay(50);

        lock (captured)
        {
            Assert.Contains(captured, e => e is SortStartedEvent);
            Assert.Contains(captured, e => e is FileMovedEvent);
            Assert.Contains(captured, e => e is SortCompletedEvent);
        }
    }
}
