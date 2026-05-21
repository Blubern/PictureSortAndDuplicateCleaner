using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSortParameterFolderNameValidationTests
{
    private static PictureSortParameter ParameterWith(string duplicateFolderName, string alreadyExistingFolderName, string source, string target)
        => new(
            new[] { source },
            target,
            1,
            duplicateFolderName,
            alreadyExistingFolderName,
            inventoryOfTheTargetDirectory: false);

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("../escape")]
    [InlineData("..\\escape")]
    [InlineData("nested/path")]
    [InlineData("nested\\path")]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsBadDuplicateFolderName(string bad)
    {
        var fs = new InMemoryFileSystem();
        var source = new InMemoryDirectory(fs);
        var target = new InMemoryDirectory(fs);
        var parameter = ParameterWith(bad, "!ExistsInTarget", source.Path, target.Path);

        var ex = Assert.Throws<ArgumentException>(() => PictureSortParameterValidator.Validate(parameter, fs));
        Assert.Contains("DuplicateFolderName", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("dir/sub")]
    public void Validate_RejectsBadAlreadyExistingFolderName(string bad)
    {
        var fs = new InMemoryFileSystem();
        var source = new InMemoryDirectory(fs);
        var target = new InMemoryDirectory(fs);
        var parameter = ParameterWith("!Duplicate", bad, source.Path, target.Path);

        var ex = Assert.Throws<ArgumentException>(() => PictureSortParameterValidator.Validate(parameter, fs));
        Assert.Contains("AlreadyExistingFolderName", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("!Duplicate", "!ExistsInTarget")]
    [InlineData("dup", "existing")]
    [InlineData("_archive", "_skipped")]
    public void Validate_AcceptsSafeSingleSegmentFolderNames(string dup, string existing)
    {
        var fs = new InMemoryFileSystem();
        var source = new InMemoryDirectory(fs);
        var target = new InMemoryDirectory(fs);
        var parameter = ParameterWith(dup, existing, source.Path, target.Path);

        var exception = Record.Exception(() => PictureSortParameterValidator.Validate(parameter, fs));
        Assert.Null(exception);
    }

    [Fact]
    public void Validate_RejectsTargetInsideSourceDirectory()
    {
        var fs = new InMemoryFileSystem();
        var source = new InMemoryDirectory(fs);
        var target = Path.Combine(source.Path, "target");
        fs.CreateDirectory(target);
        var parameter = ParameterWith("!Duplicate", "!ExistsInTarget", source.Path, target);

        var ex = Assert.Throws<ArgumentException>(() => PictureSortParameterValidator.Validate(parameter, fs));
        Assert.Contains("Target directory", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsSourceInsideTargetDirectory()
    {
        var fs = new InMemoryFileSystem();
        var target = new InMemoryDirectory(fs);
        var source = Path.Combine(target.Path, "source");
        fs.CreateDirectory(source);
        var parameter = ParameterWith("!Duplicate", "!ExistsInTarget", source, target.Path);

        var ex = Assert.Throws<ArgumentException>(() => PictureSortParameterValidator.Validate(parameter, fs));
        Assert.Contains("Source directory", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsSourceSameAsTargetDirectory()
    {
        var fs = new InMemoryFileSystem();
        var source = new InMemoryDirectory(fs);
        var parameter = ParameterWith("!Duplicate", "!ExistsInTarget", source.Path, source.Path);

        var ex = Assert.Throws<ArgumentException>(() => PictureSortParameterValidator.Validate(parameter, fs));
        Assert.Contains("same as the target", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
