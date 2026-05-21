using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSortParameterTests
{
    private static PictureSortParameter MakeParameter(string source, string target, int maxConcurrency = 1)
        => new(new[] { source }, target, maxConcurrency, "!Duplicate", "!ExistsInTarget", true);

    [Fact]
    public void Constructor_IsPureDtoAndDoesNotTouchDisk()
    {
        var missingSource = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var missingTarget = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var parameter = MakeParameter(missingSource, missingTarget);

        Assert.Equal(missingSource, Assert.Single(parameter.SourceDirectories));
        Assert.Equal(missingTarget, parameter.TargetDirectory);
    }

    [Fact]
    public void Validate_WithExistingDirectories_DoesNotThrow()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);

        var parameter = MakeParameter(sourceDirectory.Path, targetDirectory.Path);

        PictureSortParameterValidator.Validate(parameter, fs);
    }

    [Fact]
    public void Validate_WithMissingSourceDirectory_ThrowsArgumentException()
    {
        var fs = new InMemoryFileSystem();
        var targetDirectory = new InMemoryDirectory(fs);
        var missingSourceDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var parameter = MakeParameter(missingSourceDirectory, targetDirectory.Path);

        var ex = Assert.Throws<ArgumentException>(() => PictureSortParameterValidator.Validate(parameter, fs));
        Assert.Contains("source directory", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_WithMissingTargetDirectory_ThrowsArgumentException()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var missingTargetDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var parameter = MakeParameter(sourceDirectory.Path, missingTargetDirectory);

        var ex = Assert.Throws<ArgumentException>(() => PictureSortParameterValidator.Validate(parameter, fs));
        Assert.Contains("target directory", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithNonPositiveMaxConcurrency_ThrowsArgumentException(int maxConcurrency)
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);

        var parameter = MakeParameter(sourceDirectory.Path, targetDirectory.Path, maxConcurrency);

        var ex = Assert.Throws<ArgumentException>(() => PictureSortParameterValidator.Validate(parameter, fs));
        Assert.Contains("MAX_CONCURRENCY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryValidate_WithMissingSource_ReturnsFalseWithErrorMessage()
    {
        var fs = new InMemoryFileSystem();
        var targetDirectory = new InMemoryDirectory(fs);
        var missingSourceDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        var parameter = MakeParameter(missingSourceDirectory, targetDirectory.Path);

        var success = PictureSortParameterValidator.TryValidate(parameter, fs, out var error);

        Assert.False(success);
        Assert.NotNull(error);
        Assert.Contains("source directory", error!, StringComparison.OrdinalIgnoreCase);
    }
}
