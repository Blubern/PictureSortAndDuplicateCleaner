using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner;

public static class UniqueFileNameExtension
{
    public const int DefaultMaxCollisionAttempts = 10_000;

    public static string CheckIfFileExistsWhenYesIterateANumberOnTheEnd(this string fullFileName)
        => CheckIfFileExistsWhenYesIterateANumberOnTheEnd(fullFileName, DefaultFileSystem.Instance);

    public static string CheckIfFileExistsWhenYesIterateANumberOnTheEnd(this string fullFileName, IFileSystem fileSystem)
        => CheckIfFileExistsWhenYesIterateANumberOnTheEnd(fullFileName, fileSystem, DefaultMaxCollisionAttempts);

    public static string CheckIfFileExistsWhenYesIterateANumberOnTheEnd(this string fullFileName, IFileSystem fileSystem, int maxAttempts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullFileName);
        ArgumentNullException.ThrowIfNull(fileSystem);
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Maximum collision attempts must be greater than zero.");
        }

        if (!fileSystem.FileExists(fullFileName))
        {
            return fullFileName;
        }
        
        var fileName = Path.GetFileNameWithoutExtension(fullFileName);
        var filePath = Path.GetDirectoryName(fullFileName) ?? string.Empty;
        var fileExtension = Path.GetExtension(fullFileName);
        var i = 0;
        
        while (i < maxAttempts)
        {
            var currentFullFileName = Path.Combine(filePath, $"{fileName}_{i}{fileExtension}");
            if (!fileSystem.FileExists(currentFullFileName))
            {
                return currentFullFileName;
            }

            i++;
        }

        throw new IOException($"Could not find an available file name for '{fullFileName}' after {maxAttempts} attempts.");
    }

    public static IEnumerable<string> EnumerateFileNameCandidates(this string fullFileName, int maxAttempts = DefaultMaxCollisionAttempts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullFileName);
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Maximum collision attempts must be greater than zero.");
        }

        yield return fullFileName;

        var fileName = Path.GetFileNameWithoutExtension(fullFileName);
        var filePath = Path.GetDirectoryName(fullFileName) ?? string.Empty;
        var fileExtension = Path.GetExtension(fullFileName);

        for (var i = 0; i < maxAttempts; i++)
        {
            yield return Path.Combine(filePath, $"{fileName}_{i}{fileExtension}");
        }
    }
}