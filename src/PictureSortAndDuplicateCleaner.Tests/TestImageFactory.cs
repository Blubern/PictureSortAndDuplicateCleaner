using SkiaSharp;

namespace PictureSortAndDuplicateCleaner.Tests;

/// <summary>
/// Generates small image byte buffers at runtime via SkiaSharp so tests do not need
/// to ship binary blobs in the repository. Output is deterministic for a given input.
/// </summary>
internal static class TestImageFactory
{
    public static byte[] CreatePng(int width, int height, Action<SKCanvas> draw, int zlibLevel = 6)
    {
        using var bitmap = DrawBitmap(width, height, draw);
        using var pixmap = bitmap.PeekPixels()
            ?? throw new InvalidOperationException("Could not peek pixels from bitmap.");
        using var ms = new MemoryStream();
        using var wstream = new SKManagedWStream(ms);
        var options = new SKPngEncoderOptions(SKPngEncoderFilterFlags.AllFilters, zlibLevel);
        if (!pixmap.Encode(wstream, options))
        {
            throw new InvalidOperationException("SkiaSharp PNG encoder returned false.");
        }
        return ms.ToArray();
    }

    public static byte[] CreateJpeg(int width, int height, Action<SKCanvas> draw, int quality = 90)
    {
        using var bitmap = DrawBitmap(width, height, draw);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        return data.ToArray();
    }

    /// <summary>
    /// Returns a copy of <paramref name="jpeg"/> with an EXIF APP1 segment injected immediately
    /// after the SOI marker. The segment carries the "Exif\0\0" identifier followed by
    /// <paramref name="payload"/>. The decoded pixel data of the result is identical to the
    /// decoded pixel data of the input — JPEG decoders skip APP1 segments when reconstructing
    /// pixels. Useful to simulate "same picture, different EXIF metadata" without depending on
    /// an external EXIF-writing library.
    /// </summary>
    public static byte[] InjectExifApp1(byte[] jpeg, byte[] payload)
    {
        if (jpeg.Length < 2 || jpeg[0] != 0xFF || jpeg[1] != 0xD8)
        {
            throw new ArgumentException("Input is not a JPEG (missing SOI marker).", nameof(jpeg));
        }

        var identifier = new byte[] { 0x45, 0x78, 0x69, 0x66, 0x00, 0x00 }; // "Exif\0\0"
        var segmentLength = 2 + identifier.Length + payload.Length; // length bytes count themselves
        if (segmentLength > 0xFFFF)
        {
            throw new ArgumentException("Payload too large for a single APP1 segment.", nameof(payload));
        }

        using var ms = new MemoryStream(jpeg.Length + segmentLength + 4);
        ms.WriteByte(0xFF);
        ms.WriteByte(0xD8);
        ms.WriteByte(0xFF);
        ms.WriteByte(0xE1);
        ms.WriteByte((byte)((segmentLength >> 8) & 0xFF));
        ms.WriteByte((byte)(segmentLength & 0xFF));
        ms.Write(identifier, 0, identifier.Length);
        ms.Write(payload, 0, payload.Length);
        ms.Write(jpeg, 2, jpeg.Length - 2);
        return ms.ToArray();
    }

    public static byte[] CreateSolidPng(int width, int height, SKColor color, int zlibLevel = 6)
        => CreatePng(width, height, canvas => canvas.Clear(color), zlibLevel);

    private static SKBitmap DrawBitmap(int width, int height, Action<SKCanvas> draw)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            draw(canvas);
            canvas.Flush();
        }
        return bitmap;
    }
}
