using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;

namespace PictureSortAndDuplicateCleaner.Tests;

public sealed class PictureSorterSidecarTests
{
    [Fact]
    public async Task StartPictureSortAsync_WhenSidecarExtensionsEmpty_DoesNotTreatXmpAsSidecar()
    {
        // Default-off Garantie: ohne SIDECAR_EXTENSIONS wird .xmp wie eine normale Datei behandelt.
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var jpg = sourceDirectory.CreateFile("photo.jpg", "image bytes");
        var xmp = sourceDirectory.CreateFile("photo.xmp", "xmp metadata bytes");
        var takenTime = new DateTime(2024, 5, 19, 10, 30, 0);
        fs.SetLastWriteTime(jpg, takenTime);
        fs.SetLastWriteTime(xmp, takenTime);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false);

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(2, result.SourceFilesFound);
        Assert.Equal(0, result.SidecarsMoved);
        Assert.Equal(0, result.SidecarsOrphaned);
        var filesInTarget = fs.EnumerateFiles(targetDirectory.Path, "*.*", SearchOption.AllDirectories).ToArray();
        Assert.Equal(2, filesInTarget.Length);
    }

    [Fact]
    public async Task StartPictureSortAsync_WhenSidecarConfigured_MovesSidecarAlongsidePrimaryIntoDateFolder()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var jpg = sourceDirectory.CreateFile("photo.jpg", "image bytes");
        var xmp = sourceDirectory.CreateFile("photo.xmp", "xmp metadata bytes");
        var takenTime = new DateTime(2024, 5, 19, 10, 30, 0);
        fs.SetLastWriteTime(jpg, takenTime);
        fs.SetLastWriteTime(xmp, takenTime);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: new[] { ".xmp" });

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        var expectedDateFolder = System.IO.Path.Combine(
            targetDirectory.Path,
            takenTime.ToString("yyyy"),
            takenTime.ToString("MMMM"),
            takenTime.ToString("dd"));
        Assert.True(fs.FileExists(System.IO.Path.Combine(expectedDateFolder, "photo.jpg")));
        Assert.True(fs.FileExists(System.IO.Path.Combine(expectedDateFolder, "photo.xmp")));
        Assert.False(fs.FileExists(jpg));
        Assert.False(fs.FileExists(xmp));
        Assert.Equal(1, result.SourceFilesFound);
        Assert.Equal(1, result.FilesMovedToTarget);
        Assert.Equal(1, result.SidecarsMoved);
        Assert.Equal(0, result.SidecarsOrphaned);
    }

    [Fact]
    public async Task StartPictureSortAsync_WhenSidecarConfiguredAndNameCollides_SidecarInheritsSuffix()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var takenTime = new DateTime(2024, 5, 19, 10, 30, 0);

        // Pre-create a colliding file in the target date folder so the source primary
        // is renamed to "photo_1.jpg".
        var preExistingDir = System.IO.Path.Combine(
            targetDirectory.Path,
            takenTime.ToString("yyyy"),
            takenTime.ToString("MMMM"),
            takenTime.ToString("dd"));
        fs.CreateDirectory(preExistingDir);
        fs.WriteAllText(System.IO.Path.Combine(preExistingDir, "photo.jpg"), "other image");

        var jpg = sourceDirectory.CreateFile("photo.jpg", "image bytes");
        var xmp = sourceDirectory.CreateFile("photo.xmp", "xmp metadata bytes");
        fs.SetLastWriteTime(jpg, takenTime);
        fs.SetLastWriteTime(xmp, takenTime);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: new[] { ".xmp" });

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.True(fs.FileExists(System.IO.Path.Combine(preExistingDir, "photo.jpg")));
        Assert.True(fs.FileExists(System.IO.Path.Combine(preExistingDir, "photo_0.jpg")));
        Assert.True(fs.FileExists(System.IO.Path.Combine(preExistingDir, "photo_0.xmp")));
        Assert.Equal(1, result.SidecarsMoved);
    }

    [Fact]
    public async Task StartPictureSortAsync_WhenPrimaryGoesToDuplicateFolder_SidecarFollows()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var firstJpg = sourceDirectory.CreateFile("photo-a.jpg", "same image bytes");
        var firstXmp = sourceDirectory.CreateFile("photo-a.xmp", "first xmp");
        var secondJpg = sourceDirectory.CreateFile("photo-b.jpg", "same image bytes");
        var secondXmp = sourceDirectory.CreateFile("photo-b.xmp", "second xmp");
        var takenTime = new DateTime(2024, 5, 19, 10, 30, 0);
        fs.SetLastWriteTime(firstJpg, takenTime);
        fs.SetLastWriteTime(firstXmp, takenTime);
        fs.SetLastWriteTime(secondJpg, takenTime);
        fs.SetLastWriteTime(secondXmp, takenTime);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            2,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: new[] { ".xmp" });

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        var duplicateRoot = System.IO.Path.Combine(sourceDirectory.Path, "!Duplicate");
        var duplicateJpgs = fs.EnumerateFiles(duplicateRoot, "*.jpg", SearchOption.AllDirectories).ToArray();
        var duplicateXmps = fs.EnumerateFiles(duplicateRoot, "*.xmp", SearchOption.AllDirectories).ToArray();
        Assert.Single(duplicateJpgs);
        Assert.Single(duplicateXmps);

        var targetJpgs = fs.EnumerateFiles(targetDirectory.Path, "*.jpg", SearchOption.AllDirectories).ToArray();
        var targetXmps = fs.EnumerateFiles(targetDirectory.Path, "*.xmp", SearchOption.AllDirectories).ToArray();
        Assert.Single(targetJpgs);
        Assert.Single(targetXmps);

        Assert.Equal(2, result.SidecarsMoved);
    }

    [Fact]
    public async Task StartPictureSortAsync_WhenPrimaryGoesToAlreadyExisting_SidecarFollows()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var sourceJpg = sourceDirectory.CreateFile("incoming.jpg", "already imported image bytes");
        var sourceXmp = sourceDirectory.CreateFile("incoming.xmp", "incoming xmp");
        targetDirectory.CreateFile(System.IO.Path.Combine("archive", "existing.jpg"), "already imported image bytes");

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: true,
            sidecarExtensions: new[] { ".xmp" });

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        var existingRoot = System.IO.Path.Combine(sourceDirectory.Path, "!ExistsInTarget");
        Assert.Single(fs.EnumerateFiles(existingRoot, "incoming.jpg", SearchOption.AllDirectories).ToArray());
        Assert.Single(fs.EnumerateFiles(existingRoot, "incoming.xmp", SearchOption.AllDirectories).ToArray());
        Assert.False(fs.FileExists(sourceJpg));
        Assert.False(fs.FileExists(sourceXmp));
        Assert.Equal(1, result.SidecarsMoved);
    }

    [Fact]
    public async Task StartPictureSortAsync_WithMultipleSidecarTypes_AllFollowPrimary()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var jpg = sourceDirectory.CreateFile("photo.jpg", "image bytes");
        var xmp = sourceDirectory.CreateFile("photo.xmp", "xmp");
        var aae = sourceDirectory.CreateFile("photo.aae", "aae");
        var takenTime = new DateTime(2024, 5, 19, 10, 30, 0);
        fs.SetLastWriteTime(jpg, takenTime);
        fs.SetLastWriteTime(xmp, takenTime);
        fs.SetLastWriteTime(aae, takenTime);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: new[] { ".xmp", ".aae" });

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        var expectedDateFolder = System.IO.Path.Combine(
            targetDirectory.Path,
            takenTime.ToString("yyyy"),
            takenTime.ToString("MMMM"),
            takenTime.ToString("dd"));
        Assert.True(fs.FileExists(System.IO.Path.Combine(expectedDateFolder, "photo.jpg")));
        Assert.True(fs.FileExists(System.IO.Path.Combine(expectedDateFolder, "photo.xmp")));
        Assert.True(fs.FileExists(System.IO.Path.Combine(expectedDateFolder, "photo.aae")));
        Assert.Equal(2, result.SidecarsMoved);
        Assert.Equal(0, result.SidecarsOrphaned);
    }

    [Fact]
    public async Task StartPictureSortAsync_WithLightroomNamePattern_RecognizesSidecar()
    {
        // Pattern "IMG.jpg.xmp" (sidecar carries full primary filename + extra extension)
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var jpg = sourceDirectory.CreateFile("photo.jpg", "image bytes");
        var xmp = sourceDirectory.CreateFile("photo.jpg.xmp", "xmp");
        var takenTime = new DateTime(2024, 5, 19, 10, 30, 0);
        fs.SetLastWriteTime(jpg, takenTime);
        fs.SetLastWriteTime(xmp, takenTime);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: new[] { ".xmp" });

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        var expectedDateFolder = System.IO.Path.Combine(
            targetDirectory.Path,
            takenTime.ToString("yyyy"),
            takenTime.ToString("MMMM"),
            takenTime.ToString("dd"));
        Assert.True(fs.FileExists(System.IO.Path.Combine(expectedDateFolder, "photo.jpg")));
        Assert.True(fs.FileExists(System.IO.Path.Combine(expectedDateFolder, "photo.jpg.xmp")));
        Assert.Equal(1, result.SidecarsMoved);
        Assert.Equal(0, result.SidecarsOrphaned);
    }

    [Fact]
    public async Task StartPictureSortAsync_WithOrphanSidecar_LeavesItInPlaceAndReportsCount()
    {
        var fs = new InMemoryFileSystem();
        var sourceDirectory = new InMemoryDirectory(fs);
        var targetDirectory = new InMemoryDirectory(fs);
        var jpg = sourceDirectory.CreateFile("photo.jpg", "image bytes");
        var orphanXmp = sourceDirectory.CreateFile("orphan.xmp", "no matching primary");
        var takenTime = new DateTime(2024, 5, 19, 10, 30, 0);
        fs.SetLastWriteTime(jpg, takenTime);
        fs.SetLastWriteTime(orphanXmp, takenTime);

        var sorter = new PictureSorter(new InventoryDirectory(fs), fs);
        var parameter = new PictureSortParameter(
            new[] { sourceDirectory.Path },
            targetDirectory.Path,
            1,
            "!Duplicate",
            "!ExistsInTarget",
            inventoryOfTheTargetDirectory: false,
            sidecarExtensions: new[] { ".xmp" });

        var result = await sorter.StartPictureSortAsync(parameter, new TestProgress(), CancellationToken.None);

        Assert.Equal(0, result.SidecarsMoved);
        Assert.Equal(1, result.SidecarsOrphaned);
        Assert.Equal(0, result.ErrorCount);
        Assert.True(fs.FileExists(orphanXmp), "Orphan sidecar must remain in source directory.");
    }
}
