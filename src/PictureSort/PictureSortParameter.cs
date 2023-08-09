namespace PictureSort;

public class PictureSortParameter
{
    public PictureSortParameter(string sourceDirectory, string targetDirectory)
    {
        SourceDirectory = sourceDirectory;
        TargetDirectory = targetDirectory;
        MaxConcurrency = 1;
    }

    public PictureSortParameter(string sourceDirectory, string targetDirectory, uint maxConcurrency)
    {
        SourceDirectory = sourceDirectory;
        TargetDirectory = targetDirectory;
        MaxConcurrency = maxConcurrency;
    }

    public string SourceDirectory { get; }

    public string TargetDirectory { get; }

    public uint MaxConcurrency { get; }

    public override string ToString()
    {
        return $"{nameof(SourceDirectory)}: {SourceDirectory}, {nameof(TargetDirectory)}: {TargetDirectory}, {nameof(MaxConcurrency)}: {MaxConcurrency}";
    }
}