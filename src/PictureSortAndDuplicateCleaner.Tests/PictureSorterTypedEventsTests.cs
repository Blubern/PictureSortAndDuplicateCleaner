using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;
using PictureSortAndDuplicateCleaner.Events;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSorterTypedEventsTests
{
    [Fact]
    public async Task EmitsInventoryStartedAndCompleted_ForSourceAndTarget()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        sourceDirectory.CreateFile("a.jpg", "x", lastWriteUtc: new DateTime(2023, 1, 1, 0, 0, 0));
        sourceDirectory.CreateFile("b.jpg", "y", lastWriteUtc: new DateTime(2023, 1, 2, 0, 0, 0));
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: true);
        var captured = new List<PictureSortEvent>();
        var sink = new Progress<PictureSortEvent>(e => { lock (captured) { captured.Add(e); } });

        await sorter.StartPictureSortAsync(parameter, new TestProgress(), sink, CancellationToken.None);
        await Task.Delay(50);

        lock (captured)
        {
            Assert.Equal(2, captured.OfType<InventoryStartedEvent>().Count());
            var completed = captured.OfType<InventoryCompletedEvent>().ToList();
            Assert.Equal(2, completed.Count);
            Assert.Contains(completed, e => e.Directory == sourceDirectory.Path && e.Primaries == 2);
            Assert.Contains(completed, e => e.Directory == targetDirectory.Path && e.Primaries == 0);
        }
    }

    [Fact]
    public async Task EmitsDuplicateDetectedEvent_WhenSourceContainsDuplicates()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        sourceDirectory.CreateFile("a.jpg", "same", lastWriteUtc: new DateTime(2023, 1, 1, 0, 0, 0));
        sourceDirectory.CreateFile("b.jpg", "same", lastWriteUtc: new DateTime(2023, 1, 1, 0, 0, 0));
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
        await Task.Delay(50);

        lock (captured)
        {
            Assert.NotEmpty(captured.OfType<DuplicateDetectedEvent>());
        }
    }

    [Fact]
    public async Task EmitsAlreadyExistsInTargetEvent_WhenSourceFileAlreadyInTarget()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        sourceDirectory.CreateFile("a.jpg", "payload", lastWriteUtc: new DateTime(2023, 1, 1, 0, 0, 0));
        targetDirectory.CreateFile("a.jpg", "payload", lastWriteUtc: new DateTime(2023, 1, 1, 0, 0, 0));
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: true);
        var captured = new List<PictureSortEvent>();
        var sink = new Progress<PictureSortEvent>(e => { lock (captured) { captured.Add(e); } });

        await sorter.StartPictureSortAsync(parameter, new TestProgress(), sink, CancellationToken.None);
        await Task.Delay(50);

        lock (captured)
        {
            var ev = captured.OfType<AlreadyExistsInTargetEvent>().ToList();
            Assert.NotEmpty(ev);
            Assert.False(ev[0].ViaJournal);
        }
    }

    [Fact]
    public async Task EmitsOrphanSidecarEvent_WhenSidecarHasNoPrimary()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        sourceDirectory.CreateFile("a.jpg", "x", lastWriteUtc: new DateTime(2023, 1, 1, 0, 0, 0));
        sourceDirectory.CreateFile("orphan.xmp", "<x/>");
        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: new[] { ".xmp" });
        var captured = new List<PictureSortEvent>();
        var sink = new Progress<PictureSortEvent>(e => { lock (captured) { captured.Add(e); } });

        await sorter.StartPictureSortAsync(parameter, new TestProgress(), sink, CancellationToken.None);
        await Task.Delay(50);

        lock (captured)
        {
            var orphans = captured.OfType<OrphanSidecarEvent>().ToList();
            Assert.Single(orphans);
            Assert.EndsWith("orphan.xmp", orphans[0].SidecarPath);
        }
    }
}
