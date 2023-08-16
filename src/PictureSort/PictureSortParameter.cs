namespace PictureSort;

public class PictureSortParameter
{
    public PictureSortParameter(string sourceDirectory, string targetDirectory)
    : this(sourceDirectory, targetDirectory, 1)
    {
    }

    public PictureSortParameter(string sourceDirectory, string targetDirectory, uint maxConcurrency)
    {
        SourceDirectory = sourceDirectory;
        TargetDirectory = targetDirectory;
        MaxConcurrency = maxConcurrency;
        DuplicateFolderName = "!Duplicate";
        MoveDuplicateFilesInSourceDirectory = true;
        MoveDuplicateFilesInTargetDirectory = true;
        DuplicateFilesTargetFolderInSourceDirectory = Path.Combine(SourceDirectory, DuplicateFolderName);
        DuplicateFilesTargetFolderInTargetDirectory = Path.Combine(TargetDirectory, DuplicateFolderName);
        AlreadyExistingFolderName = "!ExistsInTarget";
        AlreadyExistingFolder = Path.Combine(SourceDirectory, AlreadyExistingFolderName);
    }

    public string SourceDirectory { get; }

    public string TargetDirectory { get; }

    public uint MaxConcurrency { get; }

    public bool MoveDuplicateFilesInSourceDirectory { get; }
    
    public bool MoveDuplicateFilesInTargetDirectory { get; }
    
    public string DuplicateFolderName { get; }
    
    public string DuplicateFilesTargetFolderInSourceDirectory { get; }
    
    public string DuplicateFilesTargetFolderInTargetDirectory { get; }
    
    public string AlreadyExistingFolderName { get; }
    
    public string AlreadyExistingFolder { get; }
    
    public override string ToString()
    {
        return $"{nameof(SourceDirectory)}: {SourceDirectory}, {nameof(TargetDirectory)}: {TargetDirectory}, {nameof(MaxConcurrency)}: {MaxConcurrency}";
    }
}