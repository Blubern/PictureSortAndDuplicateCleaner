using Directory = MetadataExtractor.Directory;

namespace PictureSort;

public sealed class FileInventoryResult
{
    private bool _isOnlyHash;
    
    public FileInventoryResult(string fullPath, string originalDirectory, string hash, string originalFileName)
    {
        FullPath = fullPath;
        OriginalDirectory = originalDirectory;
        Hash = hash;
        CreationTime = DateTime.MinValue;
        LastWriteTime = DateTime.MinValue;
        LastAccessTime = DateTime.MinValue;
        OriginalDateAsString = string.Empty;
        OriginalDate = DateTime.MinValue;
        OriginalFileName = originalFileName;
        IgnoredReason = string.Empty;
        IsIgnored = false;
        _isOnlyHash = true;
    }
    
    public FileInventoryResult(string fullPath, string originalDirectory, string hash, DateTime? creationTime, DateTime? lastWriteTime, DateTime? lastAccessTime, string? originalDateAsString, DateTime? originalDate, string originalFileName)
    {
        FullPath = fullPath;
        OriginalDirectory = originalDirectory;
        Hash = hash;
        CreationTime = creationTime;
        LastWriteTime = lastWriteTime;
        LastAccessTime = lastAccessTime;
        OriginalDateAsString = originalDateAsString;
        OriginalDate = originalDate;
        OriginalFileName = originalFileName;
        IgnoredReason = string.Empty;
        IsIgnored = false;
        _isOnlyHash = false;
    }

    public string FullPath { get; }
    
    public string OriginalDirectory { get; }
    
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
        if (CalculatedTakenTime == DateTime.MinValue)
        {
            return "Unknown";
        }
        
        return CalculatedTakenTime.ToString("yyyy")
            + Path.DirectorySeparatorChar
            + CalculatedTakenTime.ToString("MMMM")
            + Path.DirectorySeparatorChar
            + CalculatedTakenTime.ToString("dd");
    }

    public void SetIgnored(string reason)
    {
        IsIgnored = true;
        IgnoredReason = reason;
    }

    public override string ToString()
    {
        if (IsIgnored)
        {
            return $"{nameof(Hash)}: {Hash} - {nameof(IsIgnored)}: {IsIgnored}, {nameof(IgnoredReason)}: {IgnoredReason}, {nameof(OriginalFileName)}: {OriginalFileName}, {nameof(FullPath)}: {FullPath}";
        }
        
        if (_isOnlyHash)
        {
            return $"{nameof(Hash)}: {Hash} - {nameof(OriginalFileName)}: {OriginalFileName}, {nameof(IsIgnored)}: {IsIgnored}, {nameof(FullPath)}: {FullPath}";            
        }
        
        return $"{nameof(Hash)}: {Hash} - {nameof(CreationTime)}: {CreationTime}, {nameof(LastWriteTime)}: {LastWriteTime}, {nameof(LastAccessTime)}: {LastAccessTime}, {nameof(OriginalDateAsString)}: {OriginalDateAsString}, {nameof(OriginalDate)}: {OriginalDate}, {nameof(OriginalFileName)}: {OriginalFileName}, {nameof(CalculatedTakenTime)}: {CalculatedTakenTime}, {nameof(IsIgnored)}: {IsIgnored}, {nameof(FullPath)}: {FullPath}";
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