using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;
using PictureSortAndDuplicateCleaner.Journal;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class AlreadyExistingVerifierBehaviorTests
{
    [Fact]
    public async Task HashPlusSize_AvoidsFlaggingTargetCollisionWithDifferentLength()
    {
        var fs = new InMemoryFileSystem();
        var src = new InMemoryDirectory(fs);
        var tgt = new InMemoryDirectory(fs);

        var srcFile = new FileInventoryResult(Path.Combine(src.Path, "s.bin"), src.Path, "h1", "s.bin", length: 100);
        var tgtFile = new FileInventoryResult(Path.Combine(tgt.Path, "t.bin"), tgt.Path, "h1", "t.bin", length: 200);

        var verifier = AlreadyExistingVerifier.Build(new[] { tgtFile }, NullPictureSortJournal.Instance);

        Assert.True(verifier.IsAlreadyInTarget(srcFile, DuplicateVerification.HashOnly));
        Assert.False(verifier.IsAlreadyInTarget(srcFile, DuplicateVerification.HashPlusSize));
        await Task.CompletedTask;
    }

    [Fact]
    public void JournalHashes_AlwaysMatchUnderHashPlusSize_DueToUnknownLengthFallback()
    {
        var src = new FileInventoryResult("/x/s.bin", "/x", "hjournal", "s.bin", length: 12345);

        var journal = new InMemoryTestJournal("hjournal");
        var verifier = AlreadyExistingVerifier.Build(Array.Empty<FileInventoryResult>(), journal);

        Assert.True(verifier.IsAlreadyInTarget(src, DuplicateVerification.HashPlusSize));
    }
}
