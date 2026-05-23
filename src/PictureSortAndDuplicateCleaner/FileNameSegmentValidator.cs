namespace PictureSortAndDuplicateCleaner;

internal static class FileNameSegmentValidator
{
    private static readonly char[] PortableInvalidFileNameChars = { '<', '>', ':', '"', '|', '?', '*' };

    public static bool IsInvalidPortableFileNameChar(char value)
        => char.IsControl(value)
            || PortableInvalidFileNameChars.Contains(value)
            || Path.GetInvalidFileNameChars().Contains(value);

    public static int IndexOfInvalidPortableFileNameChar(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (IsInvalidPortableFileNameChar(value[i]))
            {
                return i;
            }
        }

        return -1;
    }
}