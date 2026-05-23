using System.Globalization;

namespace PictureSortAndDuplicateCleaner.Cmd;

public sealed class PictureSortEnvironmentOptions
{
    public const string PictureSourceVariable = "PICTURE_SOURCE";
    public const string PictureTargetVariable = "PICTURE_TARGET";
    public const string MaxConcurrencyVariable = "MAX_CONCURRENCY";
    public const string DuplicateFolderNameVariable = "DUPLICATE_FOLDER_NAME";
    public const string AlreadyExistingFolderNameVariable = "ALREADY_EXISTING_FOLDER_NAME";
    public const string CultureNameVariable = "CULTURE_NAME";
    public const string InventoryOfTheTargetDirectoryVariable = "INVENTOR_OF_THE_TARGET_DIRECTORY";
    public const string LoggingTargetVariable = "LOGGING_TARGET";
    public const string SidecarExtensionsVariable = "SIDECAR_EXTENSIONS";
    public const string JournalFileVariable = "JOURNAL_FILE";
    public const string FolderTemplateVariable = "FOLDER_TEMPLATE";
    public const string UnknownDatePolicyVariable = "UNKNOWN_DATE_POLICY";
    public const string DryRunVariable = "DRY_RUN";
    public const string OperationModeVariable = "OPERATION_MODE";
    public const string DuplicateVerificationVariable = "DUPLICATE_VERIFICATION";
    public const string HashModeVariable = "HASH_MODE";

    public const string DefaultDuplicateFolderName = "!Duplicate";
    public const string DefaultAlreadyExistingFolderName = "!ExistsInTarget";
    public const string DefaultCultureName = "en-US";
    public const string DefaultLoggingTarget = "pictureSortLogging.txt";

    private PictureSortEnvironmentOptions(
        IReadOnlyList<string> sourceDirectories,
        string targetDirectory,
        int maxConcurrency,
        string duplicateFolderName,
        string alreadyExistingFolderName,
        string cultureName,
        bool inventoryOfTheTargetDirectory,
        string loggingTarget)
    {
        SourceDirectories = sourceDirectories;
        TargetDirectory = targetDirectory;
        MaxConcurrency = maxConcurrency;
        DuplicateFolderName = duplicateFolderName;
        AlreadyExistingFolderName = alreadyExistingFolderName;
        CultureName = cultureName;
        InventoryOfTheTargetDirectory = inventoryOfTheTargetDirectory;
        LoggingTarget = loggingTarget;
        SidecarExtensions = Array.Empty<string>();
        JournalFilePath = null;
        FolderTemplate = PictureSortAndDuplicateCleaner.FolderStructure.FolderStructureTemplate.Default;
        UnknownDatePolicy = PictureSortAndDuplicateCleaner.UnknownDatePolicy.MoveToUnknownFolder;
        DryRun = false;
        OperationMode = PictureSortAndDuplicateCleaner.OperationMode.Move;
        DuplicateVerification = PictureSortAndDuplicateCleaner.DuplicateVerification.HashOnly;
        HashMode = PictureSortAndDuplicateCleaner.HashMode.File;
    }

    public IReadOnlyList<string> SourceDirectories { get; }
    public string TargetDirectory { get; }
    public int MaxConcurrency { get; }
    public string DuplicateFolderName { get; }
    public string AlreadyExistingFolderName { get; }
    public string CultureName { get; }
    public bool InventoryOfTheTargetDirectory { get; }
    public string LoggingTarget { get; }
    public IReadOnlyList<string> SidecarExtensions { get; private init; }
    public string? JournalFilePath { get; private init; }
    public PictureSortAndDuplicateCleaner.FolderStructure.FolderStructureTemplate FolderTemplate { get; private init; }
    public PictureSortAndDuplicateCleaner.UnknownDatePolicy UnknownDatePolicy { get; private init; }
    public bool DryRun { get; private init; }
    public PictureSortAndDuplicateCleaner.OperationMode OperationMode { get; private init; }
    public PictureSortAndDuplicateCleaner.DuplicateVerification DuplicateVerification { get; private init; }
    public PictureSortAndDuplicateCleaner.HashMode HashMode { get; private init; }

    public static bool TryCreate(
        Func<string, string?> getEnvironmentVariable,
        out PictureSortEnvironmentOptions? options,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);

        options = null;
        errorMessage = null;

        var rawSource = getEnvironmentVariable(PictureSourceVariable);
        if (string.IsNullOrWhiteSpace(rawSource))
        {
            errorMessage = $"Environment variable {PictureSourceVariable} must be set.";
            return false;
        }

        var sources = rawSource
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (sources.Count == 0)
        {
            errorMessage = $"Environment variable {PictureSourceVariable} must contain at least one directory.";
            return false;
        }

        var target = getEnvironmentVariable(PictureTargetVariable);
        if (string.IsNullOrWhiteSpace(target))
        {
            errorMessage = $"Environment variable {PictureTargetVariable} must be set.";
            return false;
        }

        var rawInventoryFlag = getEnvironmentVariable(InventoryOfTheTargetDirectoryVariable);
        var inventoryOfTheTargetDirectory = true;
        if (!string.IsNullOrWhiteSpace(rawInventoryFlag) && !bool.TryParse(rawInventoryFlag, out inventoryOfTheTargetDirectory))
        {
            errorMessage = $"Environment variable {InventoryOfTheTargetDirectoryVariable} must be '{bool.TrueString}' or '{bool.FalseString}'.";
            return false;
        }

        var maxConcurrency = Environment.ProcessorCount;
        var rawMaxConcurrency = getEnvironmentVariable(MaxConcurrencyVariable);
        if (!string.IsNullOrWhiteSpace(rawMaxConcurrency))
        {
            if (!int.TryParse(rawMaxConcurrency, NumberStyles.Integer, CultureInfo.InvariantCulture, out maxConcurrency) || maxConcurrency <= 0)
            {
                errorMessage = $"Environment variable {MaxConcurrencyVariable} must be a positive integer.";
                return false;
            }
        }

        var cultureName = ValueOrDefault(getEnvironmentVariable(CultureNameVariable), DefaultCultureName);
        if (!CultureInfo.GetCultures(CultureTypes.AllCultures).Any(c => string.Equals(c.Name, cultureName, StringComparison.OrdinalIgnoreCase)))
        {
            errorMessage = $"Environment variable {CultureNameVariable} must be a valid culture name (got '{cultureName}').";
            return false;
        }

        PictureSortAndDuplicateCleaner.FolderStructure.FolderStructureTemplate folderTemplate;
        var folderTemplateRaw = getEnvironmentVariable(FolderTemplateVariable);
        if (string.IsNullOrWhiteSpace(folderTemplateRaw))
        {
            folderTemplate = PictureSortAndDuplicateCleaner.FolderStructure.FolderStructureTemplate.Default;
        }
        else
        {
            try
            {
                folderTemplate = PictureSortAndDuplicateCleaner.FolderStructure.FolderStructureTemplate.Parse(folderTemplateRaw);
            }
            catch (ArgumentException ex)
            {
                errorMessage = $"{FolderTemplateVariable}: {ex.Message}";
                return false;
            }
        }

        var unknownDatePolicy = PictureSortAndDuplicateCleaner.UnknownDatePolicy.MoveToUnknownFolder;
        var unknownDatePolicyRaw = getEnvironmentVariable(UnknownDatePolicyVariable);
        if (!string.IsNullOrWhiteSpace(unknownDatePolicyRaw))
        {
            switch (unknownDatePolicyRaw.Trim().ToLowerInvariant())
            {
                case "move":
                case "movetounknownfolder":
                    unknownDatePolicy = PictureSortAndDuplicateCleaner.UnknownDatePolicy.MoveToUnknownFolder;
                    break;
                case "skip":
                case "skipandcount":
                    unknownDatePolicy = PictureSortAndDuplicateCleaner.UnknownDatePolicy.SkipAndCount;
                    break;
                case "fail":
                    unknownDatePolicy = PictureSortAndDuplicateCleaner.UnknownDatePolicy.Fail;
                    break;
                default:
                    errorMessage = $"Environment variable {UnknownDatePolicyVariable} must be one of: move, skip, fail (got '{unknownDatePolicyRaw}').";
                    return false;
            }
        }

        var dryRun = false;
        var dryRunRaw = getEnvironmentVariable(DryRunVariable);
        if (!string.IsNullOrWhiteSpace(dryRunRaw) && !bool.TryParse(dryRunRaw, out dryRun))
        {
            errorMessage = $"Environment variable {DryRunVariable} must be '{bool.TrueString}' or '{bool.FalseString}'.";
            return false;
        }

        var operationMode = PictureSortAndDuplicateCleaner.OperationMode.Move;
        var operationModeRaw = getEnvironmentVariable(OperationModeVariable);
        if (!string.IsNullOrWhiteSpace(operationModeRaw))
        {
            switch (operationModeRaw.Trim().ToLowerInvariant())
            {
                case "move":
                    operationMode = PictureSortAndDuplicateCleaner.OperationMode.Move;
                    break;
                case "copy":
                    operationMode = PictureSortAndDuplicateCleaner.OperationMode.Copy;
                    break;
                default:
                    errorMessage = $"Environment variable {OperationModeVariable} must be one of: move, copy (got '{operationModeRaw}').";
                    return false;
            }
        }

        var duplicateVerification = PictureSortAndDuplicateCleaner.DuplicateVerification.HashOnly;
        var duplicateVerificationRaw = getEnvironmentVariable(DuplicateVerificationVariable);
        if (!string.IsNullOrWhiteSpace(duplicateVerificationRaw))
        {
            switch (duplicateVerificationRaw.Trim().ToLowerInvariant())
            {
                case "hash":
                case "hashonly":
                    duplicateVerification = PictureSortAndDuplicateCleaner.DuplicateVerification.HashOnly;
                    break;
                case "hashplussize":
                case "hash-plus-size":
                case "hash_plus_size":
                    duplicateVerification = PictureSortAndDuplicateCleaner.DuplicateVerification.HashPlusSize;
                    break;
                default:
                    errorMessage = $"Environment variable {DuplicateVerificationVariable} must be one of: hash, hashPlusSize (got '{duplicateVerificationRaw}').";
                    return false;
            }
        }

        var hashMode = PictureSortAndDuplicateCleaner.HashMode.File;
        var hashModeRaw = getEnvironmentVariable(HashModeVariable);
        if (!string.IsNullOrWhiteSpace(hashModeRaw))
        {
            switch (hashModeRaw.Trim().ToLowerInvariant())
            {
                case "file":
                case "filehash":
                case "file-hash":
                case "file_hash":
                    hashMode = PictureSortAndDuplicateCleaner.HashMode.File;
                    break;
                case "pixel":
                case "pixelhash":
                case "pixel-hash":
                case "pixel_hash":
                    hashMode = PictureSortAndDuplicateCleaner.HashMode.Pixel;
                    break;
                default:
                    errorMessage = $"Environment variable {HashModeVariable} must be one of: file, pixel (got '{hashModeRaw}').";
                    return false;
            }
        }

        options = new PictureSortEnvironmentOptions(
            sources,
            target,
            maxConcurrency,
            ValueOrDefault(getEnvironmentVariable(DuplicateFolderNameVariable), DefaultDuplicateFolderName),
            ValueOrDefault(getEnvironmentVariable(AlreadyExistingFolderNameVariable), DefaultAlreadyExistingFolderName),
            cultureName,
            inventoryOfTheTargetDirectory,
            ValueOrDefault(getEnvironmentVariable(LoggingTargetVariable), DefaultLoggingTarget))
        {
            SidecarExtensions = ParseSidecarExtensions(getEnvironmentVariable(SidecarExtensionsVariable)),
            JournalFilePath = ParseJournalFilePath(getEnvironmentVariable(JournalFileVariable)),
            FolderTemplate = folderTemplate,
            UnknownDatePolicy = unknownDatePolicy,
            DryRun = dryRun,
            OperationMode = operationMode,
            DuplicateVerification = duplicateVerification,
            HashMode = hashMode
        };

        return true;
    }

    private static string ValueOrDefault(string? value, string defaultValue)
        => string.IsNullOrWhiteSpace(value) ? defaultValue : value;

    private static string? ParseJournalFilePath(string? raw)
        => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();

    private static IReadOnlyList<string> ParseSidecarExtensions(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<string>();
        }

        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var normalized = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            var value = part;
            if (!value.StartsWith('.'))
            {
                value = "." + value;
            }
            value = value.ToLowerInvariant();
            if (!normalized.Contains(value))
            {
                normalized.Add(value);
            }
        }

        return normalized.AsReadOnly();
    }
}
