
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
        var sourceDirectoryInventoryOriginal = await _inventoryDirectory.InventoryADirectoryAsync(
            pictureSortParameter.SourceDirectory,
            (int) pictureSortParameter.MaxConcurrency,
            progress,
            cancellationToken);
        
        progress.Report($"Making a inventor of the Target Directory.");
        var targetDirectoryInventoryOriginal = await _inventoryDirectory.InventoryADirectoryAsync(
            pictureSortParameter.TargetDirectory,
            (int) pictureSortParameter.MaxConcurrency,
            progress,
            cancellationToken);

        var sourceDirectoryInventory = await sourceDirectoryInventoryOriginal.CheckForDuplicateFilesAndMoveAsync(
            directory: pictureSortParameter.SourceDirectory,
            progress: progress,
            moveDuplicateFiles: pictureSortParameter.MoveDuplicateFilesInSourceDirectory,
            duplicateFilesTargetFolder: pictureSortParameter.DuplicateFilesTargetFolderInSourceDirectory,
            cancellationToken: cancellationToken);
        
        var targetDirectoryInventory = await targetDirectoryInventoryOriginal.CheckForDuplicateFilesAndMoveAsync(
            directory: pictureSortParameter.TargetDirectory, 
            progress: progress,
            moveDuplicateFiles: pictureSortParameter.MoveDuplicateFilesInTargetDirectory,
            duplicateFilesTargetFolder: pictureSortParameter.DuplicateFilesTargetFolderInTargetDirectory,
            cancellationToken: cancellationToken);

        sourceDirectoryInventory = await CheckForExistingFilesInTargetAsync(
            sourceDirectoryInventory: sourceDirectoryInventory,
            targetDirectoryInventory: targetDirectoryInventory,
            alreadyExistingFolder: pictureSortParameter.AlreadyExistingFolder,
            progress: progress,
            cancellationToken: cancellationToken);

        await MovePicturesToTheTargetFolderAsync(
            sourceDirectoryInventory,
            pictureSortParameter.TargetDirectory,
            progress,
            (int) pictureSortParameter.MaxConcurrency,
            cancellationToken);
        
        
        return await Task.FromResult(new PictureSortResult());
    }

    private async Task<List<FileInventoryResult>> CheckForExistingFilesInTargetAsync(List<FileInventoryResult> sourceDirectoryInventory, IReadOnlyList<FileInventoryResult> targetDirectoryInventory, string alreadyExistingFolder, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Checking for files who already exists in the target.");

        var removedExistingFiles = new List<FileInventoryResult>();
        
        var sourceDirectoryHashes = sourceDirectoryInventory.Select(a => a.Hash).Distinct().ToList();
        var targetDirectoryHashes = targetDirectoryInventory.Select(a => a.Hash).Distinct().ToList();

        var hashesAlreadyExistsInTarget = sourceDirectoryHashes.Intersect(targetDirectoryHashes).ToList();

        if (hashesAlreadyExistsInTarget.Any())
        {
            progress.Report($"We found {hashesAlreadyExistsInTarget.Count} files who already exists in the target.");

            foreach (var hash in hashesAlreadyExistsInTarget)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var filesAlreadyExists = sourceDirectoryInventory.Where(a => a.Hash.Equals(hash)).ToList();

                foreach (var fileInventoryResult in sourceDirectoryInventory)
                {
                    var targetFullDirectoryPath = Path.Combine(alreadyExistingFolder, fileInventoryResult.Hash); 
                    var targetFullPath = Path.Combine(targetFullDirectoryPath, fileInventoryResult.OriginalFileName);
                    Directory.CreateDirectory(targetFullDirectoryPath);
                    progress.Report("We move the file to the already existing file directory:");
                    progress.Report($"{fileInventoryResult.FullPath} => {targetFullPath}.");
                    File.Move(fileInventoryResult.FullPath, targetFullPath);
                }
                
                removedExistingFiles.AddRange(filesAlreadyExists);
            }
        }

        return await Task.FromResult(sourceDirectoryInventory.Except(removedExistingFiles).ToList());
    }

    private async Task MovePicturesToTheTargetFolderAsync(List<FileInventoryResult> sourceDirectoryInventory, string targetFolder, IProgress<string> progress, int maxConcurrency, CancellationToken cancellationToken)
    {
        if (sourceDirectoryInventory.Count == 0)
        {
            return;
        }
        
        var chunkSize = sourceDirectoryInventory.Count;
        if (maxConcurrency < chunkSize)
            chunkSize = (chunkSize + (maxConcurrency - 1)) / maxConcurrency;
        
        var chunks = sourceDirectoryInventory.Chunk(chunkSize);

        progress.Report($"Found: {sourceDirectoryInventory.Count} total files in the Source Directory. We Split it in {maxConcurrency} parts with a chunk size {chunkSize}.");

        var chunkTasks = chunks.Select((chunk, index) => MovePicturesToTheTargetFolderChunkedAsync(chunk, targetFolder, progress, index, cancellationToken)).ToList();

        await Task.WhenAll(chunkTasks);
    }

    private Task MovePicturesToTheTargetFolderChunkedAsync(FileInventoryResult[] sourceDirectoryInventory, string targetFolder, IProgress<string> progress, int taskNumber, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            for (var i = 0; i < sourceDirectoryInventory.Length; i++)
            {
                var fileInventoryResult = sourceDirectoryInventory[i];
                var targetFullDirectoryPath = Path.Combine(targetFolder, fileInventoryResult.GetDateFolderPart());
                var targetFullPath = Path.Combine(targetFullDirectoryPath, fileInventoryResult.OriginalFileName);
                Directory.CreateDirectory(targetFullDirectoryPath);
                progress.Report(
                    $"Task: {taskNumber} - ({i + 1}/{sourceDirectoryInventory.Length + 1}) - We move the file to the target directory:");
                progress.Report($"Task: {taskNumber} - {fileInventoryResult.FullPath} => {targetFullPath}.");
                File.Move(fileInventoryResult.FullPath, targetFullPath);
            }
        });
    }
}