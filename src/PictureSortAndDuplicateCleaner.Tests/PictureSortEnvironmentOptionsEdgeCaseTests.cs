using PictureSortAndDuplicateCleaner.Cmd;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSortEnvironmentOptionsEdgeCaseTests
{
    [Fact]
    public void TryCreate_WithInvalidDryRun_ReturnsErrorMessage()
    {
        var env = Minimal();
        env[PictureSortEnvironmentOptions.DryRunVariable] = "maybe";

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(env), out var options, out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains(PictureSortEnvironmentOptions.DryRunVariable, error);
    }

    [Fact]
    public void TryCreate_WithDryRunTrue_SetsDryRun()
    {
        var env = Minimal();
        env[PictureSortEnvironmentOptions.DryRunVariable] = bool.TrueString;

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(env), out var options, out _);

        Assert.True(success);
        Assert.True(options!.DryRun);
    }

    [Fact]
    public void TryCreate_WithInvalidOperationMode_ReturnsErrorMessage()
    {
        var env = Minimal();
        env[PictureSortEnvironmentOptions.OperationModeVariable] = "teleport";

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(env), out var options, out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains(PictureSortEnvironmentOptions.OperationModeVariable, error);
    }

    [Theory]
    [InlineData("copy", OperationMode.Copy)]
    [InlineData("COPY", OperationMode.Copy)]
    [InlineData(" move ", OperationMode.Move)]
    public void TryCreate_WithValidOperationMode_ParsesValue(string raw, OperationMode expected)
    {
        var env = Minimal();
        env[PictureSortEnvironmentOptions.OperationModeVariable] = raw;

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(env), out var options, out _);

        Assert.True(success);
        Assert.Equal(expected, options!.OperationMode);
    }

    [Fact]
    public void TryCreate_WithInvalidUnknownDatePolicy_ReturnsErrorMessage()
    {
        var env = Minimal();
        env[PictureSortEnvironmentOptions.UnknownDatePolicyVariable] = "explode";

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(env), out var options, out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.NotNull(error);
        Assert.Contains(PictureSortEnvironmentOptions.UnknownDatePolicyVariable, error);
    }

    [Theory]
    [InlineData("move", UnknownDatePolicy.MoveToUnknownFolder)]
    [InlineData("MoveToUnknownFolder", UnknownDatePolicy.MoveToUnknownFolder)]
    [InlineData("skip", UnknownDatePolicy.SkipAndCount)]
    [InlineData("SkipAndCount", UnknownDatePolicy.SkipAndCount)]
    [InlineData("fail", UnknownDatePolicy.Fail)]
    public void TryCreate_WithValidUnknownDatePolicy_ParsesValue(string raw, UnknownDatePolicy expected)
    {
        var env = Minimal();
        env[PictureSortEnvironmentOptions.UnknownDatePolicyVariable] = raw;

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(env), out var options, out _);

        Assert.True(success);
        Assert.Equal(expected, options!.UnknownDatePolicy);
    }

    [Fact]
    public void TryCreate_WithBlankPictureSource_ReturnsErrorMessage()
    {
        var env = new Dictionary<string, string?>
        {
            [PictureSortEnvironmentOptions.PictureSourceVariable] = "   ",
            [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target"
        };

        var success = PictureSortEnvironmentOptions.TryCreate(Lookup(env), out var options, out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.Contains(PictureSortEnvironmentOptions.PictureSourceVariable, error);
    }

    [Fact]
    public void TryCreate_WithNullGetEnvironmentVariableDelegate_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PictureSortEnvironmentOptions.TryCreate(null!, out _, out _));
    }

    private static Dictionary<string, string?> Minimal() => new()
    {
        [PictureSortEnvironmentOptions.PictureSourceVariable] = "C:/source",
        [PictureSortEnvironmentOptions.PictureTargetVariable] = "C:/target"
    };

    private static Func<string, string?> Lookup(IDictionary<string, string?> env)
        => name => env.TryGetValue(name, out var value) ? value : null;
}
