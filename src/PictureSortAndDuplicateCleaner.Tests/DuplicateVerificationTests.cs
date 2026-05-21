using PictureSortAndDuplicateCleaner;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class DuplicateVerificationTests
{
    private static FileInventoryResult Make(string dir, string name, string hash, long length)
        => new(Path.Combine(dir, name), dir, hash, name, length);

    [Fact]
    public void HashOnly_TreatsSameHashDifferentLengthAsDuplicates()
    {
        var a = Make("d", "a.bin", "h1", 100);
        var b = Make("d", "b.bin", "h1", 200);
        var inventory = new[] { a, b };

        var candidates = ((IReadOnlyList<FileInventoryResult>)inventory)
            .MarkDuplicatesAndCollectMoveCandidates(
                new TestProgress(), "!Dup", CancellationToken.None, DuplicateVerification.HashOnly);

        Assert.Single(candidates);
        Assert.True(b.IsIgnored);
    }

    [Fact]
    public void HashPlusSize_DoesNotTreatSameHashDifferentLengthAsDuplicates()
    {
        var a = Make("d", "a.bin", "h1", 100);
        var b = Make("d", "b.bin", "h1", 200);
        var inventory = new[] { a, b };

        var candidates = ((IReadOnlyList<FileInventoryResult>)inventory)
            .MarkDuplicatesAndCollectMoveCandidates(
                new TestProgress(), "!Dup", CancellationToken.None, DuplicateVerification.HashPlusSize);

        Assert.Empty(candidates);
        Assert.False(a.IsIgnored);
        Assert.False(b.IsIgnored);
    }

    [Fact]
    public void HashPlusSize_TreatsSameHashSameLengthAsDuplicates()
    {
        var a = Make("d", "a.bin", "h1", 100);
        var b = Make("d", "b.bin", "h1", 100);
        var inventory = new[] { a, b };

        var candidates = ((IReadOnlyList<FileInventoryResult>)inventory)
            .MarkDuplicatesAndCollectMoveCandidates(
                new TestProgress(), "!Dup", CancellationToken.None, DuplicateVerification.HashPlusSize);

        Assert.Single(candidates);
        Assert.True(b.IsIgnored);
    }

    [Fact]
    public void HashPlusSize_FallsBackToHashOnlyWhenAnyLengthUnknown()
    {
        var a = Make("d", "a.bin", "h1", 0); // unknown length
        var b = Make("d", "b.bin", "h1", 200);
        var inventory = new[] { a, b };

        var candidates = ((IReadOnlyList<FileInventoryResult>)inventory)
            .MarkDuplicatesAndCollectMoveCandidates(
                new TestProgress(), "!Dup", CancellationToken.None, DuplicateVerification.HashPlusSize);

        Assert.Single(candidates);
    }
}
