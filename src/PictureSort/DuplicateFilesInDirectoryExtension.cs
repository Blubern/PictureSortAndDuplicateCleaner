namespace PictureSort;

using System.Linq;

public static class DuplicateFilesInDirectoryExtension
{
    public static Task CheckForDuplicateFilesAndMoveAsync(
        this IReadOnlyList<FileInventoryResult> inventoryResult,
        IProgress<string> progress,
        bool moveDuplicateFiles,
        string duplicateFolderName,
        CancellationToken cancellationToken)
    {
        progress.Report($"Check for duplicated files.");
        var duplicateFilesSourceDictionary = inventoryResult
            .GroupBy(a => a.Hash)
            .ToDictionary(a => a.Key, a => a.ToList())
            .Where(a => a.Value.Count > 1)
            .Select(a => a)
            .ToList()
            .AsReadOnly();
       
        if (duplicateFilesSourceDictionary.Count > 0)
        {
            progress.Report($"We found duplicated Files.");

            for (var id = 0; id < duplicateFilesSourceDictionary.Count; id++)
            {
                var duplicatedFileGroup = duplicateFilesSourceDictionary[id];
                cancellationToken.ThrowIfCancellationRequested();

                for (var i = 0; i < duplicatedFileGroup.Value.Count; i++)
                {
                    var duplicateFile = duplicatedFileGroup.Value[i];
                    var duplicateFileTargetDirectory = Path.Combine(duplicateFile.OriginalDirectory, duplicateFolderName, duplicatedFileGroup.Key);
                    progress.Report($"{id+1}/{duplicateFilesSourceDictionary.Count} -  {duplicatedFileGroup.Key} ({duplicatedFileGroup.Value.Count}) - {duplicateFile}");
                    if (i > 0 && moveDuplicateFiles)
                    {
                        var targetFullPath = Path.Combine(duplicateFileTargetDirectory, duplicateFile.OriginalFileName);
                        targetFullPath = targetFullPath.CheckIfFileExistsWhenYesIterateANumberOnTheEnd();
                        Directory.CreateDirectory(duplicateFileTargetDirectory);
                        progress.Report($"We move the file to the duplicate file directory: {duplicateFile.FullPath} => {targetFullPath}.");
                        File.Move(duplicateFile.FullPath, targetFullPath);

                        var ignoreMessage = $"This file is a duplicate file - Duplicate Files are: {string.Join(",", duplicatedFileGroup.Value.Select(a => a.FullPath))}.";
                        duplicateFile.SetIgnored(ignoreMessage);
                        progress.Report(ignoreMessage);
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}