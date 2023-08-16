namespace PictureSort;

using System.Linq;

public static class DuplicateFilesInDirectoryExtension
{
    public static Task<List<FileInventoryResult>> CheckForDuplicateFilesAndMoveAsync(
        this IReadOnlyList<FileInventoryResult> inventoryResult,
        string directory,
        IProgress<string> progress,
        bool moveDuplicateFiles,
        string duplicateFilesTargetFolder,
        CancellationToken cancellationToken)
    {
        var removedDuplicateFiles = new List<FileInventoryResult>();
        
        progress.Report($"Check for duplicated files in the Directory: {directory}.");
        var duplicateFilesSourceDictionary = inventoryResult
            .GroupBy(a => a.Hash)
            .ToDictionary(a => a.Key, a => a.ToList())
            .Where(a => a.Value.Count > 1)
            .Select(a => a)
            .ToList()
            .AsReadOnly();
       
        if (duplicateFilesSourceDictionary.Count > 0)
        {
            progress.Report($"We found duplicated Files (Directory {directory}):");
           
            foreach (var duplicatedFileGroup in duplicateFilesSourceDictionary)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                for (var i = 0; i < duplicatedFileGroup.Value.Count; i++)
                {
                    var duplicateFileTargetDirectory = Path.Combine(duplicateFilesTargetFolder,  duplicatedFileGroup.Key);
                    var duplicateFile = duplicatedFileGroup.Value[i];
                    progress.Report($"{duplicatedFileGroup.Key} ({duplicatedFileGroup.Value.Count}) - {duplicateFile}");
                    if (i > 0 && moveDuplicateFiles)
                    {
                        var targetFullPath = Path.Combine(duplicateFileTargetDirectory, duplicateFile.OriginalFileName);
                        Directory.CreateDirectory(duplicateFileTargetDirectory);
                        progress.Report("We move the file to the duplicate file directory:");
                        progress.Report($"{duplicateFile.FullPath} => {targetFullPath}.");
                        File.Move(duplicateFile.FullPath, targetFullPath);
                        
                        removedDuplicateFiles.Add(duplicateFile);
                    }
                }
            }
        }
        
        return Task.FromResult(inventoryResult.Except(removedDuplicateFiles).ToList());
    }
}