namespace PictureSortAndDuplicateCleaner.Abstractions;

/// <summary>
/// <see cref="MemoryStream"/> variant that commits its bytes back to its owning
/// <see cref="InMemoryFileSystem"/> when disposed. Mirrors the semantics of
/// <c>File.OpenWrite</c> with <c>FileMode.CreateNew</c> — used only by
/// <see cref="InMemoryFileSystem.OpenWrite"/>.
/// </summary>
internal sealed class InMemoryWriteStream : MemoryStream
{
    private readonly InMemoryFileSystem _owner;
    private readonly string _fullPath;
    private bool _committed;

    public InMemoryWriteStream(InMemoryFileSystem owner, string fullPath)
    {
        _owner = owner;
        _fullPath = fullPath;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_committed)
        {
            _committed = true;
            _owner.CommitWrite(_fullPath, ToArray());
        }
        base.Dispose(disposing);
    }
}
