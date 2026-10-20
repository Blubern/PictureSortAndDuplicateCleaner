using System.Runtime.InteropServices;
using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

/// <summary>
/// Pins the pixel hash contract against committed fixture images. The expected values were
/// produced with SkiaSharp 3.119.4 and verified unchanged on 4.153.1. A failure here means a
/// SkiaSharp (decoder/resampler) or normalization change would invalidate existing
/// <c>p:</c> journal entries — do not just update the values without deciding how to migrate.
/// </summary>
/// <remarks>
/// Skia's SIMD code paths differ per CPU architecture, so pixel hashes are only stable within
/// one architecture: arm64 values were verified on macOS and Linux, x64 values on Linux.
/// </remarks>
public class PixelContentHasherGoldenTests
{
    private static readonly string FixtureDirectory =
        Path.Combine(AppContext.BaseDirectory, "TestData", "PixelHashGolden");

    [Theory]
    [InlineData("jpeg_420.jpg", "p:ba46bce2709ca5a7", "p:20849000b0148c2d")]
    [InlineData("jpeg_444.jpg", "p:821df20bcaa9a853", "p:057fbd9e83f98226")]
    [InlineData("jpeg_progressive.jpg", "p:ba46bce2709ca5a7", "p:20849000b0148c2d")]
    [InlineData("jpeg_gray.jpg", "p:a0fd6148df073916", "p:e3f2185247584f7b")]
    [InlineData("jpeg_cmyk.jpg", "p:833a640790ac0b5a", "p:5798647dc940cf5c")]
    [InlineData("jpeg_upscale_97x61.jpg", "p:8a2dfc652eecf8cf", "p:eed9bb7d5e50f3af")]
    [InlineData("png_rgb8.png", "p:ee625f9b009dbbfc", "p:1c4ced6b0a8b3df6")]
    [InlineData("png_rgb16.png", "p:b1729b5233a78b20", "p:c5027436543ac883")]
    [InlineData("png_palette.png", "p:4b045421b771b999", "p:0c7dedc01e33cbaa")]
    [InlineData("png_alpha_gradient.png", "p:1ce53585bda13f63", "p:e1b1fb9e9fb638ef")]
    [InlineData("png_upscale_97x61.png", "p:ab90fdb59771ba11", "p:ab90fdb59771ba11")]
    [InlineData("webp_lossy.webp", "p:de83e83f34937fd1", "p:7273e8020c806f24")]
    [InlineData("webp_lossless.webp", "p:ee625f9b009dbbfc", "p:1c4ced6b0a8b3df6")]
    [InlineData("gif.gif", "p:789e3e9f00762c35", "p:723764bf141a9a0b")]
    [InlineData("bmp.bmp", "p:90339eb6044e5138", "p:c14b992e3d853969")]
    public async Task FixtureImage_ProducesPinnedPixelHash(string fileName, string expectedArm64, string expectedX64)
    {
        var expectedHash = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => expectedArm64,
            Architecture.X64 => expectedX64,
            _ => null,
        };
        Assert.SkipWhen(expectedHash is null, $"No pinned pixel hashes for {RuntimeInformation.ProcessArchitecture}.");

        var fs = new InMemoryFileSystem();
        var path = "C:/golden/" + fileName;
        fs.AddFile(path, await File.ReadAllBytesAsync(Path.Combine(FixtureDirectory, fileName), TestContext.Current.CancellationToken));
        var warnings = new List<string>();
        var hasher = new PixelContentHasher(fs, new FileBytesContentHasher(fs), warnings.Add);

        var hash = await hasher.ComputeHashAsync(path, TestContext.Current.CancellationToken);

        Assert.Empty(warnings);
        Assert.Equal(expectedHash, hash);
    }
}
