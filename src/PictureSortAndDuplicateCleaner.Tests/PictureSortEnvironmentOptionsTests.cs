using PictureSortAndDuplicateCleaner.Cmd;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSortEnvironmentOptionsTests
{
    [Fact]
    public void TryCreate_WithMissingPictureSource_ReturnsErrorMessage()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains(PictureSortEnvironmentOptions.PictureSourceVariable, error);
    }

    [Fact]
    public void TryCreate_WithMissingPictureTarget_ReturnsErrorMessage()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains(PictureSortEnvironmentOptions.PictureTargetVariable, error);
    }

    [Fact]
    public void TryCreate_WithInvalidBoolean_ReturnsErrorMessage()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target",
            [PictureSortEnvironmentOptions.InventoryOfTheTargetDirectoryVariable] = "yes"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains(PictureSortEnvironmentOptions.InventoryOfTheTargetDirectoryVariable, error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("not-a-number")]
    public void TryCreate_WithInvalidMaxConcurrency_ReturnsErrorMessage(string value)
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target",
            [PictureSortEnvironmentOptions.MaxConcurrencyVariable] = value
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains(PictureSortEnvironmentOptions.MaxConcurrencyVariable, error);
    }

    [Fact]
    public void TryCreate_WithInvalidCulture_ReturnsErrorMessage()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target",
            [PictureSortEnvironmentOptions.CultureNameVariable] = "definitely-not-a-culture"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains(PictureSortEnvironmentOptions.CultureNameVariable, error);
    }

    [Fact]
    public void TryCreate_WithMinimalConfiguration_AppliesDefaults()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal("C:/target", options!.TargetDirectory);
        Assert.Equal(Environment.ProcessorCount, options.MaxConcurrency);
        Assert.True(options.InventoryOfTheTargetDirectory);
        Assert.Equal(PictureSortEnvironmentOptions.DefaultDuplicateFolderName, options.DuplicateFolderName);
        Assert.Equal(PictureSortEnvironmentOptions.DefaultAlreadyExistingFolderName, options.AlreadyExistingFolderName);
        Assert.Equal(PictureSortEnvironmentOptions.DefaultCultureName, options.CultureName);
        Assert.Equal(PictureSortEnvironmentOptions.DefaultLoggingTarget, options.LoggingTarget);
        Assert.Equal(DuplicateVerification.HashOnly, options.DuplicateVerification);
    }

    [Fact]
    public void TryCreate_WithMultipleSemicolonSeparatedSources_ReturnsAllSources()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = " C:/first ; C:/second ;C:/third",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(new[] { "C:/first", "C:/second", "C:/third" }, options!.SourceDirectories);
    }

    [Fact]
    public void TryCreate_WithCustomValues_UsesProvidedValues()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target",
            [PictureSortEnvironmentOptions.MaxConcurrencyVariable] = "4",
            [PictureSortEnvironmentOptions.DuplicateFolderNameVariable] = "_dupes",
            [PictureSortEnvironmentOptions.AlreadyExistingFolderNameVariable] = "_existing",
            [PictureSortEnvironmentOptions.CultureNameVariable] = "de-DE",
            [PictureSortEnvironmentOptions.InventoryOfTheTargetDirectoryVariable] = bool.FalseString,
            [PictureSortEnvironmentOptions.LoggingTargetVariable] = "custom.log"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(options);
        Assert.Equal(4, options!.MaxConcurrency);
        Assert.Equal("_dupes", options.DuplicateFolderName);
        Assert.Equal("_existing", options.AlreadyExistingFolderName);
        Assert.Equal("de-DE", options.CultureName);
        Assert.False(options.InventoryOfTheTargetDirectory);
        Assert.Equal("custom.log", options.LoggingTarget);
    }

    [Theory]
    [InlineData("hash", DuplicateVerification.HashOnly)]
    [InlineData("HashOnly", DuplicateVerification.HashOnly)]
    [InlineData("hashPlusSize", DuplicateVerification.HashPlusSize)]
    [InlineData("hash-plus-size", DuplicateVerification.HashPlusSize)]
    [InlineData("hash_plus_size", DuplicateVerification.HashPlusSize)]
    public void TryCreate_WithValidDuplicateVerification_ParsesValue(string raw, DuplicateVerification expected)
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target",
            [PictureSortEnvironmentOptions.DuplicateVerificationVariable] = raw
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out _);

        Assert.True(success);
        Assert.Equal(expected, options!.DuplicateVerification);
    }

    [Fact]
    public void TryCreate_WithInvalidDuplicateVerification_ReturnsErrorMessage()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target",
            [PictureSortEnvironmentOptions.DuplicateVerificationVariable] = "byte-for-byte"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains(PictureSortEnvironmentOptions.DuplicateVerificationVariable, error);
    }

    [Fact]
    public void TryCreate_WithoutHashMode_DefaultsToFile()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out _);

        Assert.True(success);
        Assert.Equal(HashMode.File, options!.HashMode);
    }

    [Theory]
    [InlineData("file", HashMode.File)]
    [InlineData("FILE", HashMode.File)]
    [InlineData("fileHash", HashMode.File)]
    [InlineData("file-hash", HashMode.File)]
    [InlineData("file_hash", HashMode.File)]
    [InlineData("pixel", HashMode.Pixel)]
    [InlineData("Pixel", HashMode.Pixel)]
    [InlineData("pixelHash", HashMode.Pixel)]
    [InlineData("pixel-hash", HashMode.Pixel)]
    [InlineData("pixel_hash", HashMode.Pixel)]
    public void TryCreate_WithValidHashMode_ParsesValue(string raw, HashMode expected)
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target",
            [PictureSortEnvironmentOptions.HashModeVariable] = raw
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out _);

        Assert.True(success);
        Assert.Equal(expected, options!.HashMode);
    }

    [Fact]
    public void TryCreate_WithInvalidHashMode_ReturnsErrorMessage()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target",
            [PictureSortEnvironmentOptions.HashModeVariable] = "perceptual"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains(PictureSortEnvironmentOptions.HashModeVariable, error);
    }

    [Fact]
    public void TryCreate_WithoutSidecarExtensions_DefaultsToEmptyList()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out var error);

        Assert.True(success);
        Assert.NotNull(options);
        Assert.Empty(options!.SidecarExtensions);
    }

    [Fact]
    public void TryCreate_WithSidecarExtensions_NormalizesPrefixAndCase()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target",
            [PictureSortEnvironmentOptions.SidecarExtensionsVariable] = " xmp ; .AAE ; .json ; xmp "
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out var error);

        Assert.True(success);
        Assert.NotNull(options);
        Assert.Equal(new[] { ".xmp", ".aae", ".json" }, options!.SidecarExtensions);
    }

    [Fact]
    public void TryCreate_WithoutJournalFile_LeavesPathNull()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out _);

        Assert.True(success);
        Assert.Null(options!.JournalFilePath);
    }

    [Fact]
    public void TryCreate_WithJournalFile_TrimsAndStoresValue()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target",
            [PictureSortEnvironmentOptions.JournalFileVariable] = "  C:/logs/picturesortandduplicatecleaner.jsonl  "
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out _);

        Assert.True(success);
        Assert.Equal("C:/logs/picturesortandduplicatecleaner.jsonl", options!.JournalFilePath);
    }

    [Fact]
    public void TryCreate_WithoutFolderTemplate_UsesDefault()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out _);

        Assert.True(success);
        Assert.Equal(PictureSortAndDuplicateCleaner.FolderStructure.FolderStructureTemplate.DefaultTemplate, options!.FolderTemplate.RawTemplate);
    }

    [Fact]
    public void TryCreate_WithCustomFolderTemplate_ParsesValue()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target",
            [PictureSortEnvironmentOptions.FolderTemplateVariable] = "{yyyy}/{Quarter}/{MM}"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out _);

        Assert.True(success);
        Assert.Equal("{yyyy}/{Quarter}/{MM}", options!.FolderTemplate.RawTemplate);
    }

    [Fact]
    public void TryCreate_WithInvalidFolderTemplate_Fails()
    {
        var environment = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target",
            [PictureSortEnvironmentOptions.FolderTemplateVariable] = "{yyyy}/{Bogus}"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(environment), out var options, out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains(PictureSortEnvironmentOptions.FolderTemplateVariable, error);
        Assert.Contains("Bogus", error);
    }

    private static Func<string, string?> Lookup(IDictionary<string, string?> environment)
    {
        return name => environment.TryGetValue(name, out var value) ? value : null;
    }
}
