namespace PictureSortAndDuplicateCleaner.Abstractions;

/// <summary>
/// An in-memory <see cref="IFileSystem"/> useful for tests. Stores file contents as
/// <c>byte[]</c> and tracks creation / last-write / last-access timestamps per file.
/// Paths are normalized (full-pathed, OS directory separator) so callers can mix
/// forward and backward slashes the same way <see cref="Path"/> tolerates them.
/// </summary>
/// <remarks>
/// Intentionally minimal: only the operations actually used by the production code paths
/// are modeled. Directory state is implicit — a directory "exists" if any file's path
/// starts with it or it has been explicitly created via <see cref="CreateDirectory"/>.
/// Concurrency: protected by a single lock, which is sufficient for the parallel inventory
/// scenarios the production code uses.
/// </remarks>
public sealed class InMemoryFileSystem : IFileSystem
{
    private readonly object _gate = new();
    private readonly Dictionary<string, InMemoryFileEntry> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _explicitDirectories = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryFileSystem()
    {
    }

    /// <summary>Seed a file with the given UTF-8 content. Convenience for tests.</summary>
    public void AddFile(string path, string content, DateTime? lastWriteUtc = null)
        => AddFile(path, System.Text.Encoding.UTF8.GetBytes(content), lastWriteUtc);

    /// <summary>Seed a file with raw bytes.</summary>
    public void AddFile(string path, byte[] content, DateTime? lastWriteUtc = null)
    {
        var full = Normalize(path);
        var now = lastWriteUtc ?? DateTime.UtcNow;
        lock (_gate)
        {
            _files[full] = new InMemoryFileEntry(content, now, now, now);
            AddImplicitDirectories(full);
        }
    }

    public bool FileExists(string path)
    {
        lock (_gate) { return _files.ContainsKey(Normalize(path)); }
    }

    public bool DirectoryExists(string path)
    {
        var full = Normalize(path);
        lock (_gate)
        {
            if (_explicitDirectories.Contains(full)) return true;
            var prefix = full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;
            foreach (var f in _files.Keys)
            {
                if (f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }

    public void CreateDirectory(string path)
    {
        var full = Normalize(path);
        lock (_gate) { _explicitDirectories.Add(full); }
    }

    public void Move(string source, string destination)
    {
        var src = Normalize(source);
        var dst = Normalize(destination);
        lock (_gate)
        {
            if (!_files.TryGetValue(src, out var entry))
            {
                throw new FileNotFoundException($"Source file '{source}' not found in in-memory filesystem.", source);
            }
            if (_files.ContainsKey(dst))
            {
                throw new IOException($"Destination file '{destination}' already exists.");
            }
            _files.Remove(src);
            _files[dst] = entry;
            AddImplicitDirectories(dst);
        }
    }

    public bool TryMove(string source, string destination)
    {
        var src = Normalize(source);
        var dst = Normalize(destination);
        lock (_gate)
        {
            if (!_files.TryGetValue(src, out var entry))
            {
                throw new FileNotFoundException($"Source file '{source}' not found in in-memory filesystem.", source);
            }

            if (_files.ContainsKey(dst))
            {
                return false;
            }

            _files.Remove(src);
            _files[dst] = entry;
            AddImplicitDirectories(dst);
            return true;
        }
    }

    public void Copy(string source, string destination, bool overwrite)
    {
        var src = Normalize(source);
        var dst = Normalize(destination);
        lock (_gate)
        {
            if (!_files.TryGetValue(src, out var entry))
            {
                throw new FileNotFoundException($"Source file '{source}' not found in in-memory filesystem.", source);
            }
            if (_files.ContainsKey(dst) && !overwrite)
            {
                throw new IOException($"Destination file '{destination}' already exists.");
            }
            // Deep copy the byte array so subsequent mutations on the destination cannot affect source.
            _files[dst] = new InMemoryFileEntry((byte[])entry.Content.Clone(), DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow);
            AddImplicitDirectories(dst);
        }
    }

    public void Delete(string path)
    {
        lock (_gate) { _files.Remove(Normalize(path)); }
    }

    public void DeleteEmptyDirectory(string path)
    {
        var full = Normalize(path);
        var prefix = full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;
        lock (_gate)
        {
            foreach (var f in _files.Keys)
            {
                if (f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException($"Directory '{path}' is not empty.");
                }
            }
            foreach (var d in _explicitDirectories)
            {
                if (d.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                    !d.Equals(full, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException($"Directory '{path}' is not empty.");
                }
            }
            _explicitDirectories.Remove(full);
        }
    }

    public Stream OpenRead(string path)
    {
        var full = Normalize(path);
        lock (_gate)
        {
            if (!_files.TryGetValue(full, out var entry))
            {
                throw new FileNotFoundException($"File '{path}' not found in in-memory filesystem.", path);
            }
            // Return a copy so concurrent reads don't share the same position cursor.
            return new MemoryStream(entry.Content, writable: false);
        }
    }

    public Stream OpenWrite(string path)
    {
        var full = Normalize(path);
        lock (_gate)
        {
            if (_files.ContainsKey(full))
            {
                throw new IOException($"File '{path}' already exists (OpenWrite uses CreateNew semantics).");
            }
            AddImplicitDirectories(full);
        }
        return new InMemoryWriteStream(this, full);
    }

    public IReadOnlyList<string> ReadAllLines(string path)
    {
        var full = Normalize(path);
        lock (_gate)
        {
            if (!_files.TryGetValue(full, out var entry))
            {
                throw new FileNotFoundException($"File '{path}' not found in in-memory filesystem.", path);
            }
            var text = System.Text.Encoding.UTF8.GetString(entry.Content);
            var split = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            // Match File.ReadAllLines: drop a single trailing empty entry from a terminating newline.
            if (split.Length > 0 && split[^1].Length == 0)
            {
                Array.Resize(ref split, split.Length - 1);
            }
            return split;
        }
    }

    public void WriteAllText(string path, string content)
    {
        var full = Normalize(path);
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            _files[full] = new InMemoryFileEntry(bytes, now, now, now);
            AddImplicitDirectories(full);
        }
    }

    public void AppendAllText(string path, string content)
    {
        var full = Normalize(path);
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var now = DateTime.UtcNow;
        lock (_gate)
        {
            if (_files.TryGetValue(full, out var existing))
            {
                var combined = new byte[existing.Content.Length + bytes.Length];
                Buffer.BlockCopy(existing.Content, 0, combined, 0, existing.Content.Length);
                Buffer.BlockCopy(bytes, 0, combined, existing.Content.Length, bytes.Length);
                _files[full] = new InMemoryFileEntry(combined, existing.CreationTime, now, now);
            }
            else
            {
                _files[full] = new InMemoryFileEntry(bytes, now, now, now);
            }
            AddImplicitDirectories(full);
        }
    }

    /// <summary>Test-only helper: overwrite the last-write timestamp of an existing file.</summary>
    public void SetLastWriteTime(string path, DateTime time)
    {
        var full = Normalize(path);
        lock (_gate)
        {
            if (!_files.TryGetValue(full, out var entry)) return;
            _files[full] = entry with { LastWriteTime = time.ToUniversalTime() };
        }
    }

    /// <summary>Test-only helper: overwrite the creation timestamp of an existing file.</summary>
    public void SetCreationTime(string path, DateTime time)
    {
        var full = Normalize(path);
        lock (_gate)
        {
            if (!_files.TryGetValue(full, out var entry)) return;
            _files[full] = entry with { CreationTime = time.ToUniversalTime() };
        }
    }

    public IEnumerable<string> EnumerateFiles(string directory, string searchPattern, SearchOption searchOption)
    {
        var full = Normalize(directory);
        var prefix = full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;
        List<string> snapshot;
        lock (_gate) { snapshot = _files.Keys.ToList(); }

        foreach (var path in snapshot)
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (searchOption == SearchOption.TopDirectoryOnly)
            {
                var remainder = path.AsSpan(prefix.Length);
                if (remainder.IndexOfAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) >= 0)
                {
                    continue;
                }
            }

            if (searchPattern == "*" || MatchesSimplePattern(Path.GetFileName(path), searchPattern))
            {
                yield return path;
            }
        }
    }

    public IEnumerable<string> EnumerateDirectories(string directory)
    {
        var full = Normalize(directory);
        var prefix = full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> snapshot;
        lock (_gate)
        {
            snapshot = _files.Keys.Concat(_explicitDirectories).ToList();
        }
        foreach (var path in snapshot)
        {
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var remainder = path.Substring(prefix.Length);
            if (remainder.Length == 0) continue;
            var sepIndex = remainder.IndexOf(Path.DirectorySeparatorChar);
            var firstSegment = sepIndex < 0 ? remainder : remainder.Substring(0, sepIndex);
            if (firstSegment.Length == 0) continue;
            if (seen.Add(firstSegment))
            {
                yield return Path.Combine(full, firstSegment);
            }
        }
    }

    public string? GetPathRoot(string path) => Path.GetPathRoot(path);

    public DateTime GetCreationTime(string path)
    {
        lock (_gate)
        {
            return _files.TryGetValue(Normalize(path), out var e) ? e.CreationTime : DateTime.MinValue;
        }
    }

    public DateTime GetLastWriteTime(string path)
    {
        lock (_gate)
        {
            return _files.TryGetValue(Normalize(path), out var e) ? e.LastWriteTime : DateTime.MinValue;
        }
    }

    public DateTime GetLastAccessTime(string path)
    {
        lock (_gate)
        {
            return _files.TryGetValue(Normalize(path), out var e) ? e.LastAccessTime : DateTime.MinValue;
        }
    }

    public long GetFileLength(string path)
    {
        lock (_gate)
        {
            if (!_files.TryGetValue(Normalize(path), out var e))
            {
                throw new FileNotFoundException($"File '{path}' not found in in-memory filesystem.", path);
            }
            return e.Content.LongLength;
        }
    }

    private void AddImplicitDirectories(string fullPath)
    {
        var dir = Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrEmpty(dir))
        {
            _explicitDirectories.Add(dir);
            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }
    }

    private static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        return full.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    private static bool MatchesSimplePattern(string name, string pattern)
    {
        // Tiny shell-style matcher supporting '*' and '?'. Good enough for the inventory use case.
        return MatchHelper(name, 0, pattern, 0);
    }

    private static bool MatchHelper(string s, int si, string p, int pi)
    {
        while (pi < p.Length)
        {
            var pc = p[pi];
            if (pc == '*')
            {
                while (pi < p.Length && p[pi] == '*') pi++;
                if (pi == p.Length) return true;
                for (var k = si; k <= s.Length; k++)
                {
                    if (MatchHelper(s, k, p, pi)) return true;
                }
                return false;
            }
            if (si >= s.Length) return false;
            if (pc != '?' && char.ToLowerInvariant(pc) != char.ToLowerInvariant(s[si])) return false;
            si++;
            pi++;
        }
        return si == s.Length;
    }

    internal void CommitWrite(string fullPath, byte[] content)
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            _files[fullPath] = new InMemoryFileEntry(content, now, now, now);
        }
    }
}
