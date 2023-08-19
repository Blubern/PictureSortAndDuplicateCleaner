namespace PictureSort;

public class PictureSortParameter
{
    public PictureSortParameter(
        IReadOnlyList<string> sourceDirectories,
        string targetDirectory,
        int maxConcurrency,
        string duplicateFolderName,
        string alreadyExistingFolderName)
    {
        SourceDirectories = sourceDirectories;
        TargetDirectory = targetDirectory;
        MaxConcurrency = maxConcurrency;
        DuplicateFolderName = duplicateFolderName;
        AlreadyExistingFolderName = alreadyExistingFolderName;
        MoveDuplicateFilesInSourceDirectory = true;
        MoveDuplicateFilesInTargetDirectory = true;

        foreach (var sourceDirectory in SourceDirectories)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                throw new ArgumentException($"The source directory '{sourceDirectory}' does not exist!");
            }
        }
        
        if (!Directory.Exists(targetDirectory))
        {
            throw new ArgumentException($"The target directory '{targetDirectory}' does not exist!");
        }
        
        if (maxConcurrency < 0)
        {
            throw new ArgumentException("MAX_CONCURRENCY has to be a value greater 0");
        }
    }

    public IReadOnlyList<string> SourceDirectories { get; }

    public string TargetDirectory { get; }

    public int MaxConcurrency { get; }

    public bool MoveDuplicateFilesInSourceDirectory { get; }
    
    public bool MoveDuplicateFilesInTargetDirectory { get; }
    
    public string DuplicateFolderName { get; }
    
    public string AlreadyExistingFolderName { get; }

    public override string ToString()
    {
        return $"{nameof(SourceDirectories)}: {SourceDirectories}, {nameof(TargetDirectory)}: {TargetDirectory}, {nameof(MaxConcurrency)}: {MaxConcurrency}, {nameof(MoveDuplicateFilesInSourceDirectory)}: {MoveDuplicateFilesInSourceDirectory}, {nameof(MoveDuplicateFilesInTargetDirectory)}: {MoveDuplicateFilesInTargetDirectory}, {nameof(DuplicateFolderName)}: {DuplicateFolderName}, {nameof(AlreadyExistingFolderName)}: {AlreadyExistingFolderName}";
    }
}