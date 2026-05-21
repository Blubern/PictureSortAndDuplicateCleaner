using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class InventoryDirectoryInMemoryTests
{
    [Fact]
    public async Task InventoryADirectoryAsync_OverInMemoryFileSystem_ReturnsHashesAndLengths()
    {
        var fs = new InMemoryFileSystem();
        var dir = Path.Combine(Path.GetTempPath(), "inv-imfs");
        fs.AddFile(Path.Combine(dir, "a.bin"), "alpha");
        fs.AddFile(Path.Combine(dir, "b.bin"), "beta-bytes");
        fs.AddFile(Path.Combine(dir, "sub", "c.bin"), "gamma");

        var inventory = new InventoryDirectory(fs);
        var result = await inventory.InventoryADirectoryAsync(
            new[] { dir },
            maxConcurrency: 2,
            progress: new TestProgress(),
            addExifAndFileInformation: false,
            sidecarExtensions: Array.Empty<string>(),
            cancellationToken: CancellationToken.None);

        Assert.Equal(3, result.Files.Count);
        Assert.All(result.Files, f => Assert.True(f.Length > 0, $"Expected non-zero length for {f.FullPath}"));
        Assert.Equal(3, result.Files.Select(f => f.Hash).Distinct().Count());
    }

    [Fact]
    public async Task InventoryADirectoryAsync_DetectsSidecarsViaInMemoryFileSystem()
    {
        var fs = new InMemoryFileSystem();
        var dir = Path.Combine(Path.GetTempPath(), "inv-imfs-sidecar");
        fs.AddFile(Path.Combine(dir, "photo.jpg"), "img-bytes");
        fs.AddFile(Path.Combine(dir, "photo.xmp"), "<xmp/>");

        var inventory = new InventoryDirectory(fs);
        var result = await inventory.InventoryADirectoryAsync(
            new[] { dir },
            maxConcurrency: 1,
            progress: new TestProgress(),
            addExifAndFileInformation: false,
            sidecarExtensions: new[] { ".xmp" },
            cancellationToken: CancellationToken.None);

        Assert.Single(result.Files);
        Assert.Single(result.Sidecars);
    }
}
