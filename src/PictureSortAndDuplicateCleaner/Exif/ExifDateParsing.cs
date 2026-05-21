using System.Globalization;

namespace PictureSortAndDuplicateCleaner.Exif;

internal static class ExifDateParsing
{
    private static readonly string[] AcceptedFormats =
    {
        "yyyy:MM:dd HH:mm:ss",
        "yyyy:MM:dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssK",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss.fffK",
    };

    public static DateTime? TryParse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (DateTime.TryParseExact(trimmed, AcceptedFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedExact))
        {
            return parsedExact;
        }
        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedLoose))
        {
            return parsedLoose;
        }
        return null;
    }
}
