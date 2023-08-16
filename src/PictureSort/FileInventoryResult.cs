namespace PictureSort;

public sealed class FileInventoryResult
{
    public FileInventoryResult(string fullPath, string hash, DateTime? creationTime, DateTime? lastWriteTime, DateTime? lastAccessTime, string? originalDateAsString, DateTime? originalDate, string originalFileName)
    {
        FullPath = fullPath;
        Hash = hash;
        CreationTime = creationTime;
        LastWriteTime = lastWriteTime;
        LastAccessTime = lastAccessTime;
        OriginalDateAsString = originalDateAsString;
        OriginalDate = originalDate;
        OriginalFileName = originalFileName;
        IgnoredReason = string.Empty;
        IsIgnored = false;
    }

    public string FullPath { get; }
    
    public string Hash { get; }

    public DateTime? CreationTime { get; }

    public DateTime? LastWriteTime { get; }

    public DateTime? LastAccessTime { get; }
            
    public string? OriginalDateAsString { get; }
    
    public DateTime? OriginalDate { get; }
    
    public string OriginalFileName { get; }

    public bool IsIgnored { get; private set; }
    
    public string IgnoredReason { get; private set; }
    
    public DateTime CalculatedTakenTime
    {
        get
        {
            if (OriginalDate.HasValue)
                return OriginalDate.Value;
            if (LastWriteTime.HasValue)
                return LastWriteTime.Value;
            if (CreationTime.HasValue)
                return CreationTime.Value;
            
            return DateTime.MinValue;
        }
    }

    public string GetDateFolderPart()
    {
        return CalculatedTakenTime.ToString("yyyy")
            + Path.PathSeparator
            + CalculatedTakenTime.ToString("MMMM");
    }

    public void SetIgnored(string reason)
    {
        IsIgnored = true;
        IgnoredReason = reason;
    }

    public override string ToString()
    {
        return $"{nameof(CreationTime)}: {CreationTime}, {nameof(LastWriteTime)}: {LastWriteTime}, {nameof(LastAccessTime)}: {LastAccessTime}, {nameof(OriginalDateAsString)}: {OriginalDateAsString}, {nameof(OriginalDate)}: {OriginalDate}, {nameof(OriginalFileName)}: {OriginalFileName}, {nameof(CalculatedTakenTime)}: {CalculatedTakenTime}, {nameof(IsIgnored)}: {IsIgnored}, {nameof(IgnoredReason)}: {IgnoredReason}, {nameof(FullPath)}: {FullPath}, {nameof(Hash)}: {Hash}, ";
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((FileInventoryResult) obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(FullPath, Hash);
    }

    private bool Equals(FileInventoryResult other)
    {
        return FullPath == other.FullPath && Hash == other.Hash;
    }
}