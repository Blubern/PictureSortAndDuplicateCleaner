using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

/// <summary>
/// Pins the pixel hash contract against committed fixture images. The expected values were
/// produced with SkiaSharp 3.119.4 and verified unchanged on 4.153.1. A failure here means a
/// SkiaSharp (decoder/resampler) or normalization change would invalidate existing
/// <c>p:</c> journal entries — do not just update the values without deciding how to migrate.
/// </summary>
public class PixelContentHasherGoldenTests
{
    private static readonly string FixtureDirectory =
        Path.Combine(AppContext.BaseDirectory, "TestData", "PixelHashGolden");

    [Theory]
    [InlineData("jpeg_420.jpg", "p:ba46bce2709ca5a7")]
    [InlineData("jpeg_444.jpg", "p:821df20bcaa9a853")]
    [InlineData("jpeg_progressive.jpg", "p:ba46bce2709ca5a7")]
    [InlineData("jpeg_gray.jpg", "p:a0fd6148df073916")]
    [InlineData("jpeg_cmyk.jpg", "p:833a640790ac0b5a")]
    [InlineData("jpeg_upscale_97x61.jpg", "p:8a2dfc652eecf8cf")]
    [InlineData("png_rgb8.png", "p:ee625f9b009dbbfc")]
    [InlineData("png_rgb16.png", "p:b1729b5233a78b20")]
    [InlineData("png_palette.png", "p:4b045421b771b999")]
    [InlineData("png_alpha_gradient.png", "p:1ce53585bda13f63")]
    [InlineData("png_upscale_97x61.png", "p:ab90fdb59771ba11")]
    [InlineData("webp_lossy.webp", "p:de83e83f34937fd1")]
    [InlineData("webp_lossless.webp", "p:ee625f9b009dbbfc")]
    [InlineData("gif.gif", "p:789e3e9f00762c35")]
    [InlineData("bmp.bmp", "p:90339eb6044e5138")]
    public async Task FixtureImage_ProducesPinnedPixelHash(string fileName, string expectedHash)
    {
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
