using System.Globalization;
using System.Text;

namespace PictureSortAndDuplicateCleaner.FolderStructure;

public sealed class FolderStructureTemplate
{
    public const string DefaultTemplate = "{yyyy}/{MMMM}/{dd}";
    public const string UnknownDateFolder = "Unknown";

    private static readonly char[] SegmentSeparators = { '/', '\\' };

    // Whitelist of supported tokens. Value = resolver (or null for non-date tokens, currently none).
    private static readonly IReadOnlySet<string> DateFormatTokens = new HashSet<string>(StringComparer.Ordinal)
    {
        "yyyy", "yy", "MM", "MMM", "MMMM", "dd", "ddd", "dddd", "HH", "mm", "ss"
    };

    private static readonly IReadOnlySet<string> SpecialDateTokens = new HashSet<string>(StringComparer.Ordinal)
    {
        "Quarter", "Weekday", "WeekOfYear"
    };

    private readonly IReadOnlyList<IReadOnlyList<TemplatePart>> _segments;
    private readonly string _raw;

    public static FolderStructureTemplate Default { get; } = Parse(DefaultTemplate);

    private FolderStructureTemplate(IReadOnlyList<IReadOnlyList<TemplatePart>> segments, string raw)
    {
        _segments = segments;
        _raw = raw;
    }

    public string RawTemplate => _raw;

    public static FolderStructureTemplate Parse(string template)
    {
        if (template is null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        if (string.IsNullOrWhiteSpace(template))
        {
            throw new ArgumentException("Folder template must not be empty.", nameof(template));
        }

        var rawSegments = template.Split(SegmentSeparators, StringSplitOptions.None);
        var segments = new List<IReadOnlyList<TemplatePart>>(rawSegments.Length);
        var cursor = 0;
        foreach (var rawSegment in rawSegments)
        {
            var segmentStart = cursor;
            cursor += rawSegment.Length + 1; // +1 for the separator that was consumed

            if (rawSegment.Length == 0)
            {
                // Allow leading/trailing slash; collapse empty segments silently.
                continue;
            }

            if (rawSegment == "." || rawSegment == "..")
            {
                throw new ArgumentException(
                    $"Folder template must not contain '.' or '..' segments (position {segmentStart}).",
                    nameof(template));
            }

            segments.Add(ParseSegment(rawSegment, segmentStart, template));
        }

        if (segments.Count == 0)
        {
            throw new ArgumentException("Folder template must contain at least one non-empty segment.", nameof(template));
        }

        return new FolderStructureTemplate(segments, template);
    }

    public string Build(FileInventoryResult file, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        culture ??= CultureInfo.CurrentCulture;

        var takenTime = file.CalculatedTakenTime;
        if (takenTime == DateTime.MinValue)
        {
            return UnknownDateFolder;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < _segments.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(Path.DirectorySeparatorChar);
            }

            foreach (var part in _segments[i])
            {
                builder.Append(part.Resolve(takenTime, culture));
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<TemplatePart> ParseSegment(string segment, int segmentStart, string fullTemplate)
    {
        var parts = new List<TemplatePart>();
        var i = 0;
        var literalStart = 0;
        while (i < segment.Length)
        {
            var ch = segment[i];
            if (ch == '{')
            {
                if (i > literalStart)
                {
                    parts.Add(new LiteralPart(segment[literalStart..i]));
                }

                var close = segment.IndexOf('}', i + 1);
                if (close < 0)
                {
                    throw new ArgumentException(
                        $"Folder template has an unclosed token starting at position {segmentStart + i}.",
                        nameof(fullTemplate));
                }

                var tokenName = segment.Substring(i + 1, close - i - 1);
                if (tokenName.Length == 0)
                {
                    throw new ArgumentException(
                        $"Folder template has an empty token at position {segmentStart + i}.",
                        nameof(fullTemplate));
                }

                if (!DateFormatTokens.Contains(tokenName) && !SpecialDateTokens.Contains(tokenName))
                {
                    throw new ArgumentException(
                        $"Folder template uses unknown token '{{{tokenName}}}' at position {segmentStart + i}. Supported tokens: {SupportedTokensDescription()}.",
                        nameof(fullTemplate));
                }

                parts.Add(new TokenPart(tokenName));
                i = close + 1;
                literalStart = i;
            }
            else if (ch == '}')
            {
                throw new ArgumentException(
                    $"Folder template has an unexpected '}}' at position {segmentStart + i}.",
                    nameof(fullTemplate));
            }
            else
            {
                if (FileNameSegmentValidator.IsInvalidPortableFileNameChar(ch))
                {
                    throw new ArgumentException(
                        $"Folder template contains the invalid path character '{ch}' at position {segmentStart + i}.",
                        nameof(fullTemplate));
                }
                i++;
            }
        }

        if (literalStart < segment.Length)
        {
            parts.Add(new LiteralPart(segment[literalStart..]));
        }

        return parts;
    }

    private static string SupportedTokensDescription()
    {
        var all = DateFormatTokens.Concat(SpecialDateTokens).OrderBy(t => t, StringComparer.Ordinal);
        return string.Join(", ", all.Select(t => "{" + t + "}"));
    }

    private abstract class TemplatePart
    {
        public abstract string Resolve(DateTime takenTime, CultureInfo culture);
    }

    private sealed class LiteralPart : TemplatePart
    {
        private readonly string _value;
        public LiteralPart(string value) { _value = value; }
        public override string Resolve(DateTime takenTime, CultureInfo culture) => _value;
    }

    private sealed class TokenPart : TemplatePart
    {
        private readonly string _token;
        public TokenPart(string token) { _token = token; }

        public override string Resolve(DateTime takenTime, CultureInfo culture)
        {
            if (DateFormatTokens.Contains(_token))
            {
                return takenTime.ToString(_token, culture);
            }

            return _token switch
            {
                "Quarter" => "Q" + (((takenTime.Month - 1) / 3) + 1).ToString(CultureInfo.InvariantCulture),
                "Weekday" => takenTime.ToString("dddd", culture),
                "WeekOfYear" => ISOWeek.GetWeekOfYear(takenTime).ToString("00", CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException($"Unhandled token '{_token}'.")
            };
        }
    }
}
