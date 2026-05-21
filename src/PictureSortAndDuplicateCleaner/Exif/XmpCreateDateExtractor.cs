using MetadataExtractor;
using MetadataExtractor.Formats.Xmp;

namespace PictureSortAndDuplicateCleaner.Exif;

public sealed class XmpCreateDateExtractor : IDateExtractor
{
    public string Name => "XMP.CreateDate";

    public DateExtractionResult? TryExtract(IReadOnlyList<MetadataExtractor.Directory> directories)
    {
        foreach (var xmp in directories.OfType<XmpDirectory>())
        {
            if (xmp.XmpMeta is null)
            {
                continue;
            }
            try
            {
                var props = new[] { "xmp:CreateDate", "exif:DateTimeOriginal", "photoshop:DateCreated" };
                foreach (var prop in props)
                {
                    var parts = prop.Split(':');
                    var ns = parts[0] switch
                    {
                        "xmp" => "http://ns.adobe.com/xap/1.0/",
                        "exif" => "http://ns.adobe.com/exif/1.0/",
                        "photoshop" => "http://ns.adobe.com/photoshop/1.0/",
                        _ => null,
                    };
                    if (ns is null)
                    {
                        continue;
                    }
                    var raw = xmp.XmpMeta.GetPropertyString(ns, parts[1]);
                    var parsed = ExifDateParsing.TryParse(raw);
                    if (parsed.HasValue)
                    {
                        return new DateExtractionResult(parsed.Value, raw!);
                    }
                }
            }
            catch
            {
                // XMP read errors are non-fatal; fall through to next directory.
            }
        }
        return null;
    }
}
