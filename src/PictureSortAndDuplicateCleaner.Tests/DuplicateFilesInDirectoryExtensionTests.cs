using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class DuplicateFilesInDirectoryExtensionTests
{
    [Fact]
    public void MarkDuplicatesAndCollectMoveCandidates_WithDuplicateHash_MarksAllButFirstAsIgnoredAndReturnsCandidates()
    {
        var fs = new InMemoryFileSystem();
        var temporaryDirectory = new InMemoryDirectory(fs);
        var firstFile = temporaryDirectory.CreateFile("first.jpg", "same content");
        var secondFile = temporaryDirectory.CreateFile("second.jpg", "same content");
        var inventory = new[]
        {
            new FileInventoryResult(firstFile, temporaryDirectory.Path, "same-hash", "first.jpg"),
            new FileInventoryResult(secondFile, temporaryDirectory.Path, "same-hash", "second.jpg")
        };

        var candidates = inventory.MarkDuplicatesAndCollectMoveCandidates(
            new TestProgress(),
            duplicateFolderName: "!Duplicate",
            CancellationToken.None);

        Assert.False(inventory[0].IsIgnored);
        Assert.True(inventory[1].IsIgnored);
        Assert.Single(candidates);
        Assert.Equal(inventory[1], candidates[0].File);
        Assert.Equal(
            System.IO.Path.Combine(temporaryDirectory.Path, "!Duplicate", "same-hash"),
            candidates[0].TargetDirectory);
        // Extension method does NOT physically move files anymore — that's the orchestrator's job.
        Assert.True(fs.FileExists(firstFile));
        Assert.True(fs.FileExists(secondFile));
    }

    [Fact]
    public void MarkDuplicatesAndCollectMoveCandidates_WithUniqueHashes_ReturnsNoCandidatesAndDoesNotMark()
    {
        var fs = new InMemoryFileSystem();
        var temporaryDirectory = new InMemoryDirectory(fs);
        var firstFile = temporaryDirectory.CreateFile("first.jpg", "first content");
        var secondFile = temporaryDirectory.CreateFile("second.jpg", "second content");
        var inventory = new[]
        {
            new FileInventoryResult(firstFile, temporaryDirectory.Path, "first-hash", "first.jpg"),
            new FileInventoryResult(secondFile, temporaryDirectory.Path, "second-hash", "second.jpg")
        };

        var candidates = inventory.MarkDuplicatesAndCollectMoveCandidates(
            new TestProgress(),
            duplicateFolderName: "!Duplicate",
            CancellationToken.None);

        Assert.Empty(candidates);
        Assert.All(inventory, result => Assert.False(result.IsIgnored));
        Assert.True(fs.FileExists(firstFile));
        Assert.True(fs.FileExists(secondFile));
    }

    [Fact]
    public void MarkDuplicatesAndCollectMoveCandidates_WithThreeDuplicates_MarksTwoAndEmitsTwoCandidates()
    {
        var fs = new InMemoryFileSystem();
        var temporaryDirectory = new InMemoryDirectory(fs);
        var firstFile = temporaryDirectory.CreateFile("first.jpg", "same content");
        var secondFile = temporaryDirectory.CreateFile("second.jpg", "same content");
        var thirdFile = temporaryDirectory.CreateFile("third.jpg", "same content");
        var inventory = new[]
        {
            new FileInventoryResult(firstFile, temporaryDirectory.Path, "same-hash", "first.jpg"),
            new FileInventoryResult(secondFile, temporaryDirectory.Path, "same-hash", "second.jpg"),
            new FileInventoryResult(thirdFile, temporaryDirectory.Path, "same-hash", "third.jpg")
        };

        var candidates = inventory.MarkDuplicatesAndCollectMoveCandidates(
            new TestProgress(),
            duplicateFolderName: "!Duplicate",
            CancellationToken.None);

        Assert.False(inventory[0].IsIgnored);
        Assert.True(inventory[1].IsIgnored);
        Assert.True(inventory[2].IsIgnored);
        Assert.Equal(2, candidates.Count);
        Assert.True(fs.FileExists(firstFile));
        Assert.True(fs.FileExists(secondFile));
        Assert.True(fs.FileExists(thirdFile));
    }
}
