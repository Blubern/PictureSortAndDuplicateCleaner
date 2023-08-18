namespace PictureSort;

public static class UniqueFileNameExtension
{
    public static string CheckIfFileExistsWhenYesIterateANumberOnTheEnd(this string fullFileName)
    {
        if (!File.Exists(fullFileName))
        {
            return fullFileName;
        }
        
        var fileName = Path.GetFileNameWithoutExtension(fullFileName);
        var filePath = Path.GetDirectoryName(fullFileName);
        var fileExtension = Path.GetExtension(fullFileName);
        int i = 0;
        
        while (true)
        {
            var currentFullFileName = Path.Combine(filePath, $"{fileName}_{i}{fileExtension}");
            if (!File.Exists(currentFullFileName))
            {
                return currentFullFileName;
            }

            i++;
        }
    }
}