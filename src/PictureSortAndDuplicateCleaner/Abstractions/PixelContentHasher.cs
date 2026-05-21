using System.IO.Hashing;
using SkiaSharp;

namespace PictureSortAndDuplicateCleaner.Abstractions;

/// <summary>
/// Decodes an image with SkiaSharp, normalizes it to a fixed 256x256 Rgba8888 pixel buffer,
/// and hashes that buffer with XxHash3. The result is prefixed with "p:" so it never collides
/// with the bare-hex file-bytes hashes in the journal or duplicate index.
/// </summary>
/// <remarks>
/// <para>Two images that share the same pixel content but differ in EXIF/XMP metadata,
/// PNG compression level, or lossless container all hash to the same value here.
/// Lossy re-encodings (e.g. resaved JPEGs) typically do not — for that, perceptual hashing
/// would be required, which is intentionally out of scope.</para>
/// <para>If SkiaSharp cannot decode the file (HEIC, RAW, non-image, corrupt) the hasher
/// transparently falls back to the provided <see cref="IContentHasher"/>. No exception is
/// surfaced; callers see a regular file-bytes hash for that file.</para>
/// <para>The chosen normalization (256x256, Rgba8888, linear/linear sampling) is part of
/// the hash contract. Changing it invalidates previously written journal entries with the
/// <c>p:</c> prefix.</para>
/// </remarks>
public sealed class PixelContentHasher : IContentHasher
{
    public const string PixelHashPrefix = "p:";

    private const int NormalizedWidth = 256;
    private const int NormalizedHeight = 256;

    private static readonly SKImageInfo NormalizedInfo = new(
        NormalizedWidth, NormalizedHeight, SKColorType.Rgba8888, SKAlphaType.Unpremul);

    private static readonly SKSamplingOptions PinnedSamplingOptions = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    /// <summary>File extensions that are never pixel-decoded. Anything else is attempted.</summary>
    private static readonly HashSet<string> NonImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".xmp", ".aae", ".json", ".xml", ".log", ".md", ".csv", ".yaml", ".yml",
    };

    private readonly IFileSystem _fileSystem;
    private readonly IContentHasher _fallbackHasher;
    private readonly Action<string>? _onFallback;

    public PixelContentHasher(IFileSystem fileSystem, IContentHasher fallbackHasher, Action<string>? onFallback = null)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _fallbackHasher = fallbackHasher ?? throw new ArgumentNullException(nameof(fallbackHasher));
        _onFallback = onFallback;
    }

    public async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(filePath);
        if (!string.IsNullOrEmpty(extension) && NonImageExtensions.Contains(extension))
        {
            return await _fallbackHasher.ComputeHashAsync(filePath, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var pixelHash = TryComputePixelHash(filePath, out var failureReason);
        if (pixelHash is not null)
        {
            return pixelHash;
        }

        _onFallback?.Invoke($"Pixel hash unavailable for '{filePath}' ({failureReason}); falling back to file-bytes hash.");
        return await _fallbackHasher.ComputeHashAsync(filePath, cancellationToken);
    }

    private string? TryComputePixelHash(string filePath, out string failureReason)
    {
        SKBitmap? source = null;
        SKBitmap? normalized = null;
        try
        {
            using (var stream = _fileSystem.OpenRead(filePath))
            using (var managed = new SKManagedStream(stream, disposeManagedStream: false))
            {
                source = SKBitmap.Decode(managed);
            }

            if (source is null)
            {
                failureReason = "SkiaSharp could not decode the file";
                return null;
            }

            normalized = source.Resize(NormalizedInfo, PinnedSamplingOptions);
            if (normalized is null)
            {
                failureReason = "SkiaSharp could not normalize the decoded bitmap";
                return null;
            }

            var pixels = normalized.GetPixelSpan();
            if (pixels.IsEmpty)
            {
                failureReason = "Normalized bitmap exposed no pixel buffer";
                return null;
            }

            var hasher = new XxHash3();
            hasher.Append(pixels);
            failureReason = string.Empty;
            return PixelHashPrefix + Convert.ToHexString(hasher.GetCurrentHash()).ToLowerInvariant();
        }
        catch (Exception ex)
        {
            failureReason = ex.GetType().Name + ": " + ex.Message;
            return null;
        }
        finally
        {
            normalized?.Dispose();
            source?.Dispose();
        }
    }
}
