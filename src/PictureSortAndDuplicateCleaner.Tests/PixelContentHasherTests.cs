using PictureSortAndDuplicateCleaner.Abstractions;
using SkiaSharp;

namespace PictureSortAndDuplicateCleaner.Tests;

public class PixelContentHasherTests
{
    private static (PixelContentHasher Hasher, InMemoryFileSystem Fs, List<string> Warnings) CreateHasher()
    {
        var fs = new InMemoryFileSystem();
        var fallback = new FileBytesContentHasher(fs);
        var warnings = new List<string>();
        var hasher = new PixelContentHasher(fs, fallback, warnings.Add);
        return (hasher, fs, warnings);
    }

    private static Action<SKCanvas> DrawPatternA => canvas =>
    {
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.CornflowerBlue, IsAntialias = false };
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                if (((x * 31) ^ (y * 17)) % 3 == 0)
                {
                    canvas.DrawRect(x, y, 1, 1, paint);
                }
            }
        }
    };

    private static Action<SKCanvas> DrawPatternSkyBlue => canvas =>
    {
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { Color = SKColors.SkyBlue, IsAntialias = false };
        for (var y = 0; y < 48; y++)
        {
            for (var x = 0; x < 48; x++)
            {
                if (((x + y * 5) & 0x07) == 0)
                {
                    canvas.DrawRect(x, y, 1, 1, paint);
                }
            }
        }
    };

    [Fact]
    public async Task SamePixels_DifferentPngEncoding_ProducesSameHash()
    {
        var (hasher, fs, warnings) = CreateHasher();

        var fastBytes = TestImageFactory.CreatePng(64, 64, DrawPatternA, zlibLevel: 1);
        var maxBytes = TestImageFactory.CreatePng(64, 64, DrawPatternA, zlibLevel: 9);

        // Sanity: distinct PNG encoding parameters produce distinct byte sequences.
        Assert.NotEqual(fastBytes, maxBytes);

        fs.AddFile("C:/img/a.png", fastBytes);
        fs.AddFile("C:/img/b.png", maxBytes);

        var hashA = await hasher.ComputeHashAsync("C:/img/a.png", CancellationToken.None);
        var hashB = await hasher.ComputeHashAsync("C:/img/b.png", CancellationToken.None);

        Assert.StartsWith(PixelContentHasher.PixelHashPrefix, hashA);
        Assert.Equal(hashA, hashB);
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task DifferentPixels_ProducesDifferentHash()
    {
        var (hasher, fs, _) = CreateHasher();

        fs.AddFile("C:/img/red.png", TestImageFactory.CreateSolidPng(64, 64, SKColors.Red));
        fs.AddFile("C:/img/green.png", TestImageFactory.CreateSolidPng(64, 64, SKColors.Lime));

        var hashRed = await hasher.ComputeHashAsync("C:/img/red.png", CancellationToken.None);
        var hashGreen = await hasher.ComputeHashAsync("C:/img/green.png", CancellationToken.None);

        Assert.NotEqual(hashRed, hashGreen);
        Assert.StartsWith(PixelContentHasher.PixelHashPrefix, hashRed);
        Assert.StartsWith(PixelContentHasher.PixelHashPrefix, hashGreen);
    }

    [Fact]
    public async Task NonImageBytes_FallsBackToFileHash_WithoutPrefix()
    {
        var (hasher, fs, warnings) = CreateHasher();

        // Pseudo HEIC: extension SkiaSharp does not decode, contents nothing close to a real image.
        var nonsense = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01, 0x02, 0x03 };
        fs.AddFile("C:/img/photo.heic", nonsense);

        var hash = await hasher.ComputeHashAsync("C:/img/photo.heic", CancellationToken.None);

        Assert.False(hash.StartsWith(PixelContentHasher.PixelHashPrefix),
            $"Fallback hash must not be tagged as a pixel hash, got '{hash}'.");
        Assert.NotEmpty(warnings);
        Assert.Contains("photo.heic", warnings[0]);
    }

    [Fact]
    public async Task NonImageExtension_IsHashedAsFileBytesWithoutDecodeAttempt()
    {
        var (hasher, fs, warnings) = CreateHasher();

        fs.AddFile("C:/notes/readme.txt", "hello world");

        var pixelModeHash = await hasher.ComputeHashAsync("C:/notes/readme.txt", CancellationToken.None);
        var directFileHash = await new FileBytesContentHasher(fs).ComputeHashAsync("C:/notes/readme.txt", CancellationToken.None);

        Assert.Equal(directFileHash, pixelModeHash);
        Assert.False(pixelModeHash.StartsWith(PixelContentHasher.PixelHashPrefix));
        // No decode attempted => no warning emitted.
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task PixelHash_AndFileHash_OccupyDifferentNamespaces()
    {
        var (pixelHasher, fs, _) = CreateHasher();
        var fileHasher = new FileBytesContentHasher(fs);

        fs.AddFile("C:/img/a.png", TestImageFactory.CreateSolidPng(32, 32, SKColors.Magenta));

        var pixelHash = await pixelHasher.ComputeHashAsync("C:/img/a.png", CancellationToken.None);
        var fileHash = await fileHasher.ComputeHashAsync("C:/img/a.png", CancellationToken.None);

        Assert.NotEqual(pixelHash, fileHash);
        Assert.StartsWith(PixelContentHasher.PixelHashPrefix, pixelHash);
        Assert.False(fileHash.StartsWith(PixelContentHasher.PixelHashPrefix));
    }

    [Fact]
    public async Task SamePixels_DifferentExifMetadata_ProducesSameHash()
    {
        var (hasher, fs, warnings) = CreateHasher();

        var baseJpeg = TestImageFactory.CreateJpeg(64, 64, DrawPatternA, quality: 90);

        // Two synthetic EXIF blobs that differ in every byte. The JPEG decoder ignores
        // APP1 segments when reconstructing pixels, so the decoded bitmaps stay identical.
        var jpegWithExifA = TestImageFactory.InjectExifApp1(baseJpeg, payload: new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05 });
        var jpegWithExifB = TestImageFactory.InjectExifApp1(baseJpeg, payload: new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF });

        // Sanity: the two JPEG files have different bytes, otherwise the test would prove nothing.
        Assert.NotEqual(jpegWithExifA, jpegWithExifB);

        fs.AddFile("C:/img/a.jpg", jpegWithExifA);
        fs.AddFile("C:/img/b.jpg", jpegWithExifB);

        var hashA = await hasher.ComputeHashAsync("C:/img/a.jpg", CancellationToken.None);
        var hashB = await hasher.ComputeHashAsync("C:/img/b.jpg", CancellationToken.None);

        Assert.StartsWith(PixelContentHasher.PixelHashPrefix, hashA);
        Assert.Equal(hashA, hashB);
        Assert.Empty(warnings);
    }

    [Fact]
    public async Task SamePixels_DifferentExifMetadata_FileHasher_DetectsDifference()
    {
        // Complement to the pixel-mode test above: confirm that the file-bytes hasher
        // does see the EXIF difference, i.e. the EXIF injection is actually effective.
        var fs = new InMemoryFileSystem();
        var fileHasher = new FileBytesContentHasher(fs);

        var baseJpeg = TestImageFactory.CreateJpeg(64, 64, DrawPatternA, quality: 90);
        fs.AddFile("C:/img/a.jpg", TestImageFactory.InjectExifApp1(baseJpeg, new byte[] { 0x01, 0x02, 0x03 }));
        fs.AddFile("C:/img/b.jpg", TestImageFactory.InjectExifApp1(baseJpeg, new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }));

        var hashA = await fileHasher.ComputeHashAsync("C:/img/a.jpg", CancellationToken.None);
        var hashB = await fileHasher.ComputeHashAsync("C:/img/b.jpg", CancellationToken.None);

        Assert.NotEqual(hashA, hashB);
    }

    [Fact]
    public async Task InventoryDirectory_WithPixelHasher_DetectsRecompressedDuplicate()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("C:/in/a.png", TestImageFactory.CreatePng(48, 48, DrawPatternSkyBlue, zlibLevel: 1));
        fs.AddFile("C:/in/b.png", TestImageFactory.CreatePng(48, 48, DrawPatternSkyBlue, zlibLevel: 9));

        var fallback = new FileBytesContentHasher(fs);
        var pixelHasher = new PixelContentHasher(fs, fallback);
        var inventory = new InventoryDirectory(fs, pixelHasher);

        var progress = new Progress<string>(_ => { });
        var result = await inventory.InventoryADirectoryAsync(
            new[] { "C:/in" },
            maxConcurrency: 1,
            progress,
            addExifAndFileInformation: false,
            sidecarExtensions: Array.Empty<string>(),
            CancellationToken.None);

        Assert.Equal(2, result.Files.Count);
        Assert.Equal(result.Files[0].Hash, result.Files[1].Hash);
    }

    [Fact]
    public async Task InventoryDirectory_WithFileHasher_DoesNotDetectRecompressedDuplicate()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile("C:/in/a.png", TestImageFactory.CreatePng(48, 48, DrawPatternSkyBlue, zlibLevel: 1));
        fs.AddFile("C:/in/b.png", TestImageFactory.CreatePng(48, 48, DrawPatternSkyBlue, zlibLevel: 9));

        var inventory = new InventoryDirectory(fs); // default file-bytes hasher

        var progress = new Progress<string>(_ => { });
        var result = await inventory.InventoryADirectoryAsync(
            new[] { "C:/in" },
            maxConcurrency: 1,
            progress,
            addExifAndFileInformation: false,
            sidecarExtensions: Array.Empty<string>(),
            CancellationToken.None);

        Assert.Equal(2, result.Files.Count);
        Assert.NotEqual(result.Files[0].Hash, result.Files[1].Hash);
    }
}
