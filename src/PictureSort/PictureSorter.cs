
namespace PictureSort;

public class PictureSorter
{
    private readonly InventoryDirectory _inventoryDirectory;

    public PictureSorter(InventoryDirectory inventoryDirectory)
    {
        _inventoryDirectory = inventoryDirectory;
    }

    public async Task<PictureSortResult> StartPictureSortAsync(
        PictureSortParameter pictureSortParameter,
        IProgress<string> progress, 
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        progress.Report($"Starting the Picture sort process. With the following Parameter {pictureSortParameter}.");
        
        progress.Report($"Making a inventor of the Source Directory.");
        var sourceDirectoryInventory = await _inventoryDirectory.InventoryADirectoryAsync(
            pictureSortParameter.SourceDirectory,
            (int) pictureSortParameter.MaxConcurrency,
            progress,
            cancellationToken);

        progress.Report($"Check for duplicated files in the Source Directory.");
        var groupedSourceDictionaryFiles = sourceDirectoryInventory
            .GroupBy(a => a.Hash)
            .ToDictionary(a => a.Key, a => a.ToList().AsReadOnly());

        var duplicateFilesSourceDictionary =
            groupedSourceDictionaryFiles
                .Where(a => a.Value.Count > 1)
                .Select(a => a)
                .ToList();
        
        if (duplicateFilesSourceDictionary.Count > 0)
        {
            progress.Report("We found duplicated Files:");
            foreach (var duplicatedFileGroup in duplicateFilesSourceDictionary)
            {
            }
        }
        
        progress.Report($"Making a inventor of the Target Directory.");
        var targetDirectoryInventory = await _inventoryDirectory.InventoryADirectoryAsync(
            pictureSortParameter.TargetDirectory,
            (int) pictureSortParameter.MaxConcurrency,
            progress,
            cancellationToken);
        
        
        
        
        return await Task.FromResult(new PictureSortResult());
    }
}