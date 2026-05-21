using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class InMemoryFileSystemTests
{
    [Fact]
    public void AddFile_ThenFileExists_AndContentRoundTrips()
    {
        var fs = new InMemoryFileSystem();
        var path = Path.Combine("C:", "tmp", "a.txt");
        fs.AddFile(path, "hello");

        Assert.True(fs.FileExists(path));
        using var s = fs.OpenRead(path);
        using var reader = new StreamReader(s);
        Assert.Equal("hello", reader.ReadToEnd());
        Assert.Equal(5, fs.GetFileLength(path));
    }

    [Fact]
    public void EnumerateFiles_TopOnlyVsAllDirectories_RespectsSearchOption()
    {
        var fs = new InMemoryFileSystem();
        var root = Path.Combine(Path.GetTempPath(), "imfs-test");
        fs.AddFile(Path.Combine(root, "top.txt"), "1");
        fs.AddFile(Path.Combine(root, "sub", "nested.txt"), "2");

        var top = fs.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly).ToList();
        var all = fs.EnumerateFiles(root, "*", SearchOption.AllDirectories).ToList();

        Assert.Single(top);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Move_RemovesSourceAndCreatesDestination_AndRefusesOverwrite()
    {
        var fs = new InMemoryFileSystem();
        var root = Path.Combine(Path.GetTempPath(), "imfs-move");
        var src = Path.Combine(root, "a.txt");
        var dst = Path.Combine(root, "b.txt");
        fs.AddFile(src, "x");

        fs.Move(src, dst);

        Assert.False(fs.FileExists(src));
        Assert.True(fs.FileExists(dst));

        fs.AddFile(src, "y");
        Assert.Throws<IOException>(() => fs.Move(src, dst));
    }

    [Fact]
    public void TryMove_WhenDestinationExists_ReturnsFalseAndLeavesSource()
    {
        var fs = new InMemoryFileSystem();
        var root = Path.Combine(Path.GetTempPath(), "imfs-trymove-conflict");
        var src = Path.Combine(root, "a.txt");
        var dst = Path.Combine(root, "b.txt");
        fs.AddFile(src, "source");
        fs.AddFile(dst, "destination");

        var moved = fs.TryMove(src, dst);

        Assert.False(moved);
        Assert.True(fs.FileExists(src));
        Assert.True(fs.FileExists(dst));
    }

    [Fact]
    public void TryMove_WhenSourceIsMissing_Throws()
    {
        var fs = new InMemoryFileSystem();
        var root = Path.Combine(Path.GetTempPath(), "imfs-trymove-missing");

        Assert.Throws<FileNotFoundException>(() => fs.TryMove(Path.Combine(root, "missing.txt"), Path.Combine(root, "target.txt")));
    }

    [Fact]
    public void OpenWrite_CommitsOnDispose()
    {
        var fs = new InMemoryFileSystem();
        var path = Path.Combine(Path.GetTempPath(), "imfs-write", "w.txt");
        using (var s = fs.OpenWrite(path))
        {
            s.Write(new byte[] { 1, 2, 3 }, 0, 3);
        }
        Assert.True(fs.FileExists(path));
        Assert.Equal(3, fs.GetFileLength(path));
    }

    [Fact]
    public void EnumerateDirectories_ReturnsDistinctImmediateSubdirectories()
    {
        var fs = new InMemoryFileSystem();
        var root = Path.Combine(Path.GetTempPath(), "imfs-dirs");
        fs.AddFile(Path.Combine(root, "alpha", "a.txt"), "1");
        fs.AddFile(Path.Combine(root, "alpha", "nested", "b.txt"), "2");
        fs.AddFile(Path.Combine(root, "beta", "c.txt"), "3");

        var dirs = fs.EnumerateDirectories(root)
            .Select(Path.GetFileName)
            .OrderBy(s => s)
            .ToArray();

        Assert.Equal(new[] { "alpha", "beta" }, dirs);
    }

    [Fact]
    public void EnumerateFiles_WithQuestionMarkPattern_MatchesSingleChar()
    {
        var fs = new InMemoryFileSystem();
        var root = Path.Combine(Path.GetTempPath(), "imfs-pattern");
        fs.AddFile(Path.Combine(root, "a.txt"), "1");
        fs.AddFile(Path.Combine(root, "bb.txt"), "2");

        var matched = fs.EnumerateFiles(root, "?.txt", SearchOption.TopDirectoryOnly).ToList();
        Assert.Single(matched);
        Assert.EndsWith("a.txt", matched[0]);
    }
}
