
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
            true,
            cancellationToken);

        progress.Report($"Making a inventor of the Target Directory.");
        var targetDirectoryInventoryOriginal = await _inventoryDirectory.InventoryADirectoryAsync(
            pictureSortParameter.TargetDirectory,
            (int) pictureSortParameter.MaxConcurrency,
            progress,
            false,
            cancellationToken);

        await sourceDirectoryInventoryOriginal.CheckForDuplicateFilesAndMoveAsync(
            directory: pictureSortParameter.SourceDirectory,
            progress: progress,
            moveDuplicateFiles: pictureSortParameter.MoveDuplicateFilesInSourceDirectory,
            duplicateFilesTargetFolder: pictureSortParameter.DuplicateFilesTargetFolderInSourceDirectory,
            cancellationToken: cancellationToken);

        await targetDirectoryInventoryOriginal.CheckForDuplicateFilesAndMoveAsync(
            directory: pictureSortParameter.TargetDirectory,
            progress: progress,
            moveDuplicateFiles: pictureSortParameter.MoveDuplicateFilesInTargetDirectory,
            duplicateFilesTargetFolder: pictureSortParameter.DuplicateFilesTargetFolderInTargetDirectory,
            cancellationToken: cancellationToken);

        await CheckForExistingFilesInTargetAsync(
            sourceDirectoryInventory: sourceDirectoryInventoryOriginal,
            targetDirectoryInventory: targetDirectoryInventoryOriginal,
            alreadyExistingFolder: pictureSortParameter.AlreadyExistingFolder,
            progress: progress,
            cancellationToken: cancellationToken);

        await MovePicturesToTheTargetFolderAsync(
            sourceDirectoryInventoryOriginal,
            pictureSortParameter.TargetDirectory,
            progress,
            (int) pictureSortParameter.MaxConcurrency,
            cancellationToken);

        DeleteEmptyDirectory(pictureSortParameter.SourceDirectory);

        return await Task.FromResult(new PictureSortResult());
    }

    private Task CheckForExistingFilesInTargetAsync(IReadOnlyList<FileInventoryResult> sourceDirectoryInventory,
        IReadOnlyList<FileInventoryResult> targetDirectoryInventory, string alreadyExistingFolder,
        IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Checking for files who already exists in the target.");

        var sourceDirectoryHashes =
            sourceDirectoryInventory.Where(a => !a.IsIgnored).Select(a => a.Hash).Distinct().ToList();
        var targetDirectoryHashes =
            targetDirectoryInventory.Where(a => !a.IsIgnored).Select(a => a.Hash).Distinct().ToList();

        var hashesAlreadyExistsInTarget = sourceDirectoryHashes.Intersect(targetDirectoryHashes).ToList();

        if (hashesAlreadyExistsInTarget.Any())
        {
            progress.Report($"We found {hashesAlreadyExistsInTarget.Count} files who already exists in the target.");

            foreach (var hash in hashesAlreadyExistsInTarget)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filesAlreadyExists = sourceDirectoryInventory.Where(a => a.Hash.Equals(hash)).ToList();

                foreach (var fileInventoryResult in filesAlreadyExists)
                {
                    var targetFullDirectoryPath = Path.Combine(alreadyExistingFolder, fileInventoryResult.Hash);
                    var targetFullPath = Path.Combine(targetFullDirectoryPath, fileInventoryResult.OriginalFileName);
                    targetFullPath = targetFullPath.CheckIfFileExistsWhenYesIterateANumberOnTheEnd();
                    Directory.CreateDirectory(targetFullDirectoryPath);
                    progress.Report(
                        $"We move the file to the already existing file directory:  {fileInventoryResult.FullPath} => {targetFullPath} - {fileInventoryResult.Hash}.");
                    File.Move(fileInventoryResult.FullPath, targetFullPath);
                    var ignoreMessage = "The File exists already in the Target.";
                    fileInventoryResult.SetIgnored(ignoreMessage);
                }
            }
        }

        return Task.CompletedTask;
    }

    private async Task MovePicturesToTheTargetFolderAsync(IReadOnlyList<FileInventoryResult> sourceDirectoryInventory,
        string targetFolder, IProgress<string> progress, int maxConcurrency, CancellationToken cancellationToken)
    {
        if (sourceDirectoryInventory.Count == 0)
        {
            return;
        }

        var sourceList = sourceDirectoryInventory.Where(a => !a.IsIgnored).ToList();

        var chunkSize = sourceList.Count;
        if (maxConcurrency < chunkSize)
            chunkSize = (chunkSize + (maxConcurrency - 1)) / maxConcurrency;

        var chunks = sourceList.Chunk(chunkSize);

        progress.Report(
            $"Found: {sourceList.Count} total files in the Source Directory. We Split it in {maxConcurrency} parts with a chunk size {chunkSize}.");

        var chunkTasks = chunks.Select((chunk, index) =>
                MovePicturesToTheTargetFolderChunkedAsync(chunk, targetFolder, progress, index, cancellationToken))
            .ToList();

        await Task.WhenAll(chunkTasks);
    }

    private Task MovePicturesToTheTargetFolderChunkedAsync(FileInventoryResult[] sourceDirectoryInventory,
        string targetFolder, IProgress<string> progress, int taskNumber, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            for (var i = 0; i < sourceDirectoryInventory.Length; i++)
            {
                var fileInventoryResult = sourceDirectoryInventory[i];
                var targetFullDirectoryPath = Path.Combine(targetFolder, fileInventoryResult.GetDateFolderPart());
                var targetFullPath = Path.Combine(targetFullDirectoryPath, fileInventoryResult.OriginalFileName);
                targetFullPath = targetFullPath.CheckIfFileExistsWhenYesIterateANumberOnTheEnd();
                Directory.CreateDirectory(targetFullDirectoryPath);
                progress.Report(
                    $"Task: {taskNumber} - ({i + 1}/{sourceDirectoryInventory.Length + 1}) - We move the file to the target directory: {fileInventoryResult.FullPath} => {targetFullPath} - {fileInventoryResult.Hash}.");
                File.Move(fileInventoryResult.FullPath, targetFullPath);
            }
        });
    }

    private void DeleteEmptyDirectory(string startDirectory)
    {
        foreach (var currentDirectory in Directory.GetDirectories(startDirectory))
        {
            DeleteEmptyDirectory(currentDirectory);
            if (Directory.GetFiles(currentDirectory).Length == 0 &&
                Directory.GetDirectories(currentDirectory).Length == 0)
            {
                Directory.Delete(currentDirectory, false);
            }
        }
    }
}