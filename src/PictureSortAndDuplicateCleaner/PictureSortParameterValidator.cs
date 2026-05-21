namespace PictureSortAndDuplicateCleaner;

using PictureSortAndDuplicateCleaner.Abstractions;

/// <summary>
/// Validates a <see cref="PictureSortParameter"/> against the actual filesystem state
/// and other runtime constraints that cannot be enforced inside the parameter constructor
/// (which is a pure DTO so it can be constructed in tests without touching disk).
/// </summary>
public static class PictureSortParameterValidator
{
    public static void Validate(PictureSortParameter parameter)
        => Validate(parameter, DefaultFileSystem.Instance);

    public static void Validate(PictureSortParameter parameter, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        ArgumentNullException.ThrowIfNull(fileSystem);

        if (parameter.MaxConcurrency <= 0)
        {
            throw new ArgumentException("MAX_CONCURRENCY has to be a value greater 0", nameof(parameter));
        }

        if (parameter.SourceDirectories.Count == 0)
        {
            throw new ArgumentException("At least one source directory must be provided.", nameof(parameter));
        }

        foreach (var sourceDirectory in parameter.SourceDirectories)
        {
            if (!fileSystem.DirectoryExists(sourceDirectory))
            {
                throw new ArgumentException($"The source directory '{sourceDirectory}' does not exist!", nameof(parameter));
            }
        }

        if (!fileSystem.DirectoryExists(parameter.TargetDirectory))
        {
            throw new ArgumentException($"The target directory '{parameter.TargetDirectory}' does not exist!", nameof(parameter));
        }

        ValidateFolderName(parameter.DuplicateFolderName, nameof(parameter.DuplicateFolderName));
        ValidateFolderName(parameter.AlreadyExistingFolderName, nameof(parameter.AlreadyExistingFolderName));
        ValidateSourceTargetContainment(parameter.SourceDirectories, parameter.TargetDirectory);
    }

    private static void ValidateSourceTargetContainment(IReadOnlyList<string> sourceDirectories, string targetDirectory)
    {
        var normalizedTarget = NormalizeDirectoryPath(targetDirectory);
        var comparison = GetPathComparison();

        foreach (var sourceDirectory in sourceDirectories)
        {
            var normalizedSource = NormalizeDirectoryPath(sourceDirectory);
            if (string.Equals(normalizedSource, normalizedTarget, comparison))
            {
                throw new ArgumentException($"Source directory '{sourceDirectory}' must not be the same as the target directory '{targetDirectory}'.", nameof(sourceDirectories));
            }

            if (IsSubdirectoryOf(normalizedTarget, normalizedSource, comparison))
            {
                throw new ArgumentException($"Target directory '{targetDirectory}' must not be inside source directory '{sourceDirectory}'.", nameof(targetDirectory));
            }

            if (IsSubdirectoryOf(normalizedSource, normalizedTarget, comparison))
            {
                throw new ArgumentException($"Source directory '{sourceDirectory}' must not be inside target directory '{targetDirectory}'.", nameof(sourceDirectories));
            }
        }
    }

    private static string NormalizeDirectoryPath(string path)
    {
        var fullPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return fullPath.Length == 0 ? Path.GetPathRoot(Path.GetFullPath(path)) ?? fullPath : fullPath;
    }

    private static bool IsSubdirectoryOf(string candidate, string parent, StringComparison comparison)
    {
        var parentWithSeparator = parent.EndsWith(Path.DirectorySeparatorChar)
            ? parent
            : parent + Path.DirectorySeparatorChar;

        return candidate.StartsWith(parentWithSeparator, comparison);
    }

    private static StringComparison GetPathComparison()
        => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>
    /// Rejects relative/absolute paths and path separators in a single-segment folder name to
    /// prevent the duplicate/already-existing bucket from escaping its parent directory.
    /// </summary>
    private static void ValidateFolderName(string folderName, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            throw new ArgumentException($"{propertyName} must not be empty.", nameof(folderName));
        }

        if (folderName == "." || folderName == "..")
        {
            throw new ArgumentException($"{propertyName} must not be '.' or '..'.", nameof(folderName));
        }

        if (folderName.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{propertyName} must not contain parent-directory traversal ('..'): '{folderName}'.", nameof(folderName));
        }

        if (folderName.IndexOfAny(new[] { '/', '\\' }) >= 0)
        {
            throw new ArgumentException($"{propertyName} must be a single folder segment without path separators: '{folderName}'.", nameof(folderName));
        }

        if (folderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"{propertyName} contains invalid filename characters: '{folderName}'.", nameof(folderName));
        }
    }

    public static bool TryValidate(PictureSortParameter parameter, out string? errorMessage)
        => TryValidate(parameter, DefaultFileSystem.Instance, out errorMessage);

    public static bool TryValidate(PictureSortParameter parameter, IFileSystem fileSystem, out string? errorMessage)
    {
        try
        {
            Validate(parameter, fileSystem);
            errorMessage = null;
            return true;
        }
        catch (ArgumentException ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
