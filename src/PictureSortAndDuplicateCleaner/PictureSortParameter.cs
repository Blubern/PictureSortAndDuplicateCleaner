namespace PictureSortAndDuplicateCleaner;

using PictureSortAndDuplicateCleaner.FolderStructure;

public class PictureSortParameter
{
    public PictureSortParameter(IReadOnlyList<string> sourceDirectories,
        string targetDirectory,
        int maxConcurrency,
        string duplicateFolderName,
        string alreadyExistingFolderName,
        bool inventoryOfTheTargetDirectory,
        IReadOnlyList<string>? sidecarExtensions = null,
        string? journalFilePath = null,
        FolderStructureTemplate? folderTemplate = null,
        UnknownDatePolicy unknownDatePolicy = UnknownDatePolicy.MoveToUnknownFolder,
        bool dryRun = false,
        OperationMode operationMode = OperationMode.Move,
        DuplicateVerification duplicateVerification = DuplicateVerification.HashOnly,
        HashMode hashMode = HashMode.File)
    {
        SourceDirectories = sourceDirectories;
        TargetDirectory = targetDirectory;
        MaxConcurrency = maxConcurrency;
        DuplicateFolderName = duplicateFolderName;
        AlreadyExistingFolderName = alreadyExistingFolderName;
        InventoryOfTheTargetDirectory = inventoryOfTheTargetDirectory;
        MoveDuplicateFilesInSourceDirectory = true;
        MoveDuplicateFilesInTargetDirectory = true;
        SidecarExtensions = NormalizeSidecarExtensions(sidecarExtensions);
        JournalFilePath = string.IsNullOrWhiteSpace(journalFilePath) ? null : journalFilePath;
        FolderTemplate = folderTemplate ?? FolderStructureTemplate.Default;
        UnknownDatePolicy = unknownDatePolicy;
        DryRun = dryRun;
        OperationMode = operationMode;
        DuplicateVerification = duplicateVerification;
        HashMode = hashMode;
    }

    public IReadOnlyList<string> SourceDirectories { get; }

    public string TargetDirectory { get; }

    public int MaxConcurrency { get; }

    public bool MoveDuplicateFilesInSourceDirectory { get; }
    
    public bool MoveDuplicateFilesInTargetDirectory { get; }
    
    public string DuplicateFolderName { get; }
    
    public string AlreadyExistingFolderName { get; }
    
    public bool InventoryOfTheTargetDirectory { get; }

    public IReadOnlyList<string> SidecarExtensions { get; }

    public string? JournalFilePath { get; }

    public FolderStructureTemplate FolderTemplate { get; }

    public UnknownDatePolicy UnknownDatePolicy { get; }

    public bool DryRun { get; }

    public OperationMode OperationMode { get; }

    public DuplicateVerification DuplicateVerification { get; }

    public HashMode HashMode { get; }

    public override string ToString()
    {
        return $"{nameof(SourceDirectories)}: {string.Join(",", SourceDirectories)}, {nameof(TargetDirectory)}: {TargetDirectory}, {nameof(MaxConcurrency)}: {MaxConcurrency}, {nameof(MoveDuplicateFilesInSourceDirectory)}: {MoveDuplicateFilesInSourceDirectory}, {nameof(MoveDuplicateFilesInTargetDirectory)}: {MoveDuplicateFilesInTargetDirectory}, {nameof(DuplicateFolderName)}: {DuplicateFolderName}, {nameof(AlreadyExistingFolderName)}: {AlreadyExistingFolderName}, {nameof(InventoryOfTheTargetDirectory)}: {InventoryOfTheTargetDirectory}, {nameof(SidecarExtensions)}: [{string.Join(",", SidecarExtensions)}], {nameof(JournalFilePath)}: {JournalFilePath ?? "<disabled>"}, {nameof(FolderTemplate)}: {FolderTemplate.RawTemplate}, {nameof(UnknownDatePolicy)}: {UnknownDatePolicy}, {nameof(DryRun)}: {DryRun}, {nameof(OperationMode)}: {OperationMode}, {nameof(DuplicateVerification)}: {DuplicateVerification}, {nameof(HashMode)}: {HashMode}";
    }

    private static IReadOnlyList<string> NormalizeSidecarExtensions(IReadOnlyList<string>? raw)
    {
        if (raw is null || raw.Count == 0)
        {
            return Array.Empty<string>();
        }

        var normalized = new List<string>(raw.Count);
        foreach (var entry in raw)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            var value = entry.Trim();
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