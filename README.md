# PictureSortAndDuplicateCleaner

PictureSortAndDuplicateCleaner helps clean up large photo import folders, phone backups, camera dumps,
and mixed image archives. It inventories image files, detects duplicates by a
fast XxHash3 content fingerprint (byte-wise by default, optionally on decoded
pixels via `HASH_MODE=pixel`), keeps matching sidecar files together, and
places files into a date-based target structure such as `yyyy/MMMM/dd`.

You can start the command-line tool locally or run it containerized with Docker.
The Docker workflow is useful when you want the same sorting job on Windows,
Linux, a server, a NAS, or CI, as long as the source and target folders are
mounted as volumes.

The taken date is derived in this order:

1. EXIF `DateTimeOriginal` (via `MetadataExtractor`)
2. File `LastWriteTime`
3. File `CreationTime`
4. Otherwise the file is placed into an `Unknown` folder

> **Warning:** PictureSortAndDuplicateCleaner **moves** files by default. `OPERATION_MODE=copy` is
> available when you want the source to remain untouched. Always run a backup
> before pointing the default move mode at irreplaceable data.

## Repository Layout

```
src/
  PictureSortAndDuplicateCleaner/         Core library (inventory, hashing, sorting)
  PictureSortAndDuplicateCleaner.Cmd/     Command-line entry point + Dockerfile
  PictureSortAndDuplicateCleaner.Tests/   xUnit tests
```

## Build and Test

```powershell
cd src
dotnet build PictureSortAndDuplicateCleaner.slnx
dotnet test  PictureSortAndDuplicateCleaner.slnx
```

Requires the .NET SDK 10.0 or newer.

## Prebuilt Releases

Tagged releases publish self-contained command-line executables on the GitHub
Releases page. They do not require a .NET runtime on the target machine and use
the same environment variables documented below.

Release assets are named by version and runtime:

- `picturesortandduplicatecleaner-vX.Y.Z-win-x64.zip` for Windows x64
- `picturesortandduplicatecleaner-vX.Y.Z-linux-x64.tar.gz` for Linux x64
- `picturesortandduplicatecleaner-vX.Y.Z-osx-arm64.tar.gz` for macOS ARM64 / Apple Silicon
- `SHA256SUMS.txt` for verifying downloaded artifacts

The macOS ARM64 binary is not signed or notarized. Depending on local Gatekeeper
settings, the first launch may need explicit approval in macOS security settings.

## Configuration (Environment Variables)

| Variable                            | Required | Default                  | Description                                                                                  |
| ----------------------------------- | -------- | ------------------------ | -------------------------------------------------------------------------------------------- |
| `PICTURE_SOURCE`                    | yes      | —                        | One or more source directories. Separate multiple paths with `;`.                            |
| `PICTURE_TARGET`                    | yes      | —                        | Target root directory. Files are moved into `yyyy/MMMM/dd` subfolders below this.            |
| `MAX_CONCURRENCY`                   | no       | `Environment.ProcessorCount` | Maximum number of files processed in parallel. Must be a positive integer.                |
| `DUPLICATE_FOLDER_NAME`             | no       | `!DuplicateInSource`     | Folder inside each source directory that collects source-side duplicates of files already moved. |
| `DUPLICATE_IN_TARGET_FOLDER_NAME`   | no       | `!DuplicateInTarget`     | Folder at the source-root level that collects duplicates found in the target tree, so they do not end up under the target path itself. |
| `ALREADY_EXISTING_FOLDER_NAME`      | no       | `!ExistsInTarget`        | Folder inside each source directory that collects files whose hash already exists in target. |
| `INVENTOR_OF_THE_TARGET_DIRECTORY`  | no       | `true`                   | If `true`, inventory the target as well so already-existing files can be detected.           |
| `CULTURE_NAME`                      | no       | `en-US`                  | Culture used for folder names like `MMMM` (month).                                           |
| `LOGGING_TARGET`                    | no       | `pictureSortLogging.txt` | Rolling Serilog file sink.                                                                   |
| `SIDECAR_EXTENSIONS`                | no       | _empty (feature off)_    | Opt-in. Semicolon-separated list of sidecar extensions (e.g. `.xmp;.aae;.json`) that should follow their matching primary image when it is moved. Leading dot optional, case-insensitive. |
| `JOURNAL_FILE`                      | no       | _empty (feature off)_    | Opt-in. Path to an append-only JSONL journal of moved files. May be a file path or an existing directory; in the latter case the file `picturesortandduplicatecleaner-journal.jsonl` is used. When set, hashes from previous runs are treated as "already in target" even without `INVENTOR_OF_THE_TARGET_DIRECTORY=true`. |
| `FOLDER_TEMPLATE`                   | no       | `{yyyy}/{MMMM}/{dd}`     | Token-based template for the per-file target subfolder below `PICTURE_TARGET`. Use `/` or `\` as separators. Unknown tokens or path-traversal segments (`..`) reject startup. See _Folder Template_ below. |
| `UNKNOWN_DATE_POLICY`               | no       | `move`                   | How to handle files without an EXIF date. `move` (default, legacy): drop them into the `Unknown/` folder using filesystem-timestamp fallbacks. `skip`: leave them in source and count under `FilesWithoutDateSkipped`. `fail`: leave them in source and count as errors. |
| `DRY_RUN`                           | no       | `false`                  | If `true`, no files are written, moved, copied, or deleted. The sorter still inventories, hashes, and reports every action it _would_ take. Useful for previewing changes before committing. |
| `OPERATION_MODE`                    | no       | `move`                   | `move` (default, destructive): source files are moved into target. `copy`: source files are copied into target and left in place; source-side duplicate/already-existing reorg is skipped. |
| `DUPLICATE_VERIFICATION`            | no       | `hash`                   | Duplicate matching policy. `hash`/`hashOnly`: compare XxHash3 content fingerprints only. `hashPlusSize`: require matching fingerprint and file size when size is known. |
| `HASH_MODE`                         | no       | `file`                   | Content-fingerprint source. `file`/`fileHash` (default): hash raw file bytes (legacy behavior). `pixel`/`pixelHash`: decode the image with SkiaSharp, normalize to a fixed 256x256 buffer, and hash the pixel data — detects duplicates that share pixel content but differ in EXIF/XMP metadata or lossless re-encoding. Falls back to file-bytes hashing for formats SkiaSharp cannot decode (HEIC, RAW, non-image, corrupt). See _Hashing modes_ below. |

## Exit Codes

| Code | Meaning                                                |
| ---- | ------------------------------------------------------ |
| `0`  | Completed successfully without any per-file errors.    |
| `1`  | Invalid configuration (missing/invalid env variables). |
| `2`  | Unhandled error or cancellation during the run.        |
| `3`  | Completed, but at least one file failed (`ErrorCount > 0`). |

## Run Locally

The example below runs the command-line project from source with `dotnet run`,
which requires the .NET SDK 10.0 or newer. Use Docker if you do not want to
install the SDK on the host.

```powershell
$env:PICTURE_SOURCE = "D:\Photos\Inbox"
$env:PICTURE_TARGET = "D:\Photos\Library"
dotnet run --project src/PictureSortAndDuplicateCleaner.Cmd/PictureSortAndDuplicateCleaner.Cmd.csproj -c Release
```

Multiple sources:

```powershell
$env:PICTURE_SOURCE = "D:\Photos\Inbox;E:\PhoneBackup"
```

## Run with Docker

The Dockerfile expects the repository root as the build context. Docker keeps the
runtime environment consistent across platforms; the host-specific part is the
volume mapping from your photo folders into `/data/source` and `/data/target`.

Tagged releases also publish a Linux multi-architecture image to GitHub
Container Registry:

```powershell
docker pull ghcr.io/blubern/picturesortandduplicatecleaner:latest

docker run --rm `
  -e PICTURE_SOURCE=/data/source `
  -e PICTURE_TARGET=/data/target `
  -v D:\Photos\Inbox:/data/source `
  -v D:\Photos\Library:/data/target `
  ghcr.io/blubern/picturesortandduplicatecleaner:latest
```

To build the image locally instead:

```powershell
docker build -t picturesortandduplicatecleaner -f src/PictureSortAndDuplicateCleaner.Cmd/Dockerfile .

docker run --rm `
  -e PICTURE_SOURCE=/data/source `
  -e PICTURE_TARGET=/data/target `
  -v D:\Photos\Inbox:/data/source `
  -v D:\Photos\Library:/data/target `
  picturesortandduplicatecleaner
```

For hardened unattended deployments, prefer mounting an explicit writable log
location and running the container with the least privileges that can read the
source and write the target folders.

## Behavior Notes

- Files with identical XxHash3 content fingerprints in the source are considered source-side duplicates. The
  first occurrence is moved to the target; the rest are moved to the
  `DUPLICATE_FOLDER_NAME` folder of the source directory they came from.
- Duplicates that are already present in the target tree are treated as target-side duplicates and are moved into
  `DUPLICATE_IN_TARGET_FOLDER_NAME` under the source root, which keeps the target tree clean and makes the
  destination explicit.
- XxHash3 is used as a fast duplicate-detection fingerprint, not as a cryptographic
  integrity or security hash for adversarial input.
- When `INVENTOR_OF_THE_TARGET_DIRECTORY=true`, files whose hash already exists
  in the target are not re-moved into it; instead they land in
  `ALREADY_EXISTING_FOLDER_NAME` so the source stays clean.
- File names that would collide in the target get an `_0`, `_1`, ... suffix
  before the extension. If more than 10,000 candidates are exhausted for a
  single name, the file is reported as an error instead of looping forever.
- In `OPERATION_MODE=copy`, source files and source sidecars are left in place.
  Files already present in the target are skipped, while source-side duplicate
  reorganization into `!DuplicateInSource` is not performed.
- Cancellation via Ctrl+C aborts the run and returns exit code `2`.

## Sidecar Files (opt-in)

When `SIDECAR_EXTENSIONS` is set, PictureSortAndDuplicateCleaner recognises companion files that
belong to a primary image and moves them together with their primary into the
same target folder. A sidecar is matched to a primary in the same directory by
file name. Both common naming conventions are supported:

- `IMG_1234.jpg` + `IMG_1234.xmp` (sidecar shares the base name)
- `IMG_1234.jpg` + `IMG_1234.jpg.xmp` (Lightroom-style — sidecar uses the full
  primary file name plus an extra extension)

Behavior:

- Sidecars follow their primary into the date folder, the source-side duplicate folder,
  the `DUPLICATE_IN_TARGET_FOLDER_NAME` folder, or the `!ExistsInTarget` folder — wherever the
  primary lands.
- If the primary is renamed because of a name collision (e.g. `IMG_1234_0.jpg`),
  the sidecar inherits the same renamed base name (`IMG_1234_0.xmp`).
- A sidecar file without a matching primary is reported as "orphan" in the
  final summary and is **not moved**. It stays where it was.
- The feature is fully opt-in: with an empty `SIDECAR_EXTENSIONS`, files with
  those extensions are treated like ordinary primaries (current default
  behavior).

## Journal (opt-in)

When `JOURNAL_FILE` is set, PictureSortAndDuplicateCleaner writes an append-only JSONL journal of
every primary file that was moved into the target during a run. The journal
serves two purposes:

1. **Crash safety / audit trail** — every successful move is persisted
   immediately, so you can reconstruct what happened after an interrupted run.
2. **Speed up subsequent runs** — on the next run the journal is loaded and its
   hashes are merged into the "already in target" detection. This lets you
   leave `INVENTOR_OF_THE_TARGET_DIRECTORY=false` (which avoids rehashing the
   entire library) and still detect re-imports of files that were previously
   moved.

File format:

```
{"schema":"picturesortandduplicatecleaner-journal/v1"}
{"hash":"...","targetPath":"D:/Library/2024/May/19/IMG_0001.jpg","movedAtUtc":"2024-05-19T08:30:00Z"}
...
```

Behavior:

- The journal is opt-in. With an empty `JOURNAL_FILE`, nothing is loaded or
  written.
- Stale entries (the target file no longer exists) are counted in the summary
  as `journal stale` and are **not** used for the "already in target" check —
  so deleting a file from the target makes it re-importable.
- Concurrent moves are serialised when writing journal lines so the file stays
  valid JSONL.

## Hashing Modes

PictureSortAndDuplicateCleaner offers two duplicate fingerprinting strategies, selected via
`HASH_MODE`. Both use XxHash3 underneath for speed.

### `HASH_MODE=file` (default)

Hashes the raw bytes of each file. Two files are duplicates only when their
bytes are identical. This is the historical PictureSortAndDuplicateCleaner behavior and is the
right choice when you trust that "same content" means "same bytes".

- Fastest mode — no decoding work.
- Cannot recognise an image that was re-exported with different EXIF/XMP, a
  different JPEG quality, or a different lossless container as a duplicate.
- Hash strings are bare lowercase hex (e.g. `5f3a...`).

### `HASH_MODE=pixel`

Decodes each supported image with SkiaSharp, normalizes the decoded bitmap to a
fixed 256x256 Rgba8888 buffer, and hashes that pixel buffer. Two files with
**identical pixel content** but different EXIF/XMP metadata, different PNG
compression, or different lossless container hash to the **same** value.

- Supported decoders: JPEG, PNG, GIF, BMP, WebP, ICO, WBMP (whatever SkiaSharp
  ships with on the host).
- **Per-file automatic fallback**: when a file cannot be decoded (HEIC, RAW,
  non-image, corrupt) PictureSortAndDuplicateCleaner logs a warning and uses the file-bytes hash
  for that file. The run does not abort.
- Pixel-mode hashes are tagged with the prefix `p:` so they cannot collide
  with file-mode hashes in the journal or duplicate index.
- Lossy re-encodings (a re-saved JPEG with different quality) are **not**
  recognised as duplicates: those differ at the pixel level. True
  near-duplicate detection would require perceptual hashing, which is out of
  scope.
- Slower than `file` mode because every file goes through decode + resize.
- Switching `HASH_MODE` between runs effectively invalidates the journal's
  "already in target" detection for the prior mode (the new mode's hashes
  live in a different namespace via the `p:` prefix).

## Folder Template

The per-file target subfolder below `PICTURE_TARGET` is rendered from a
token-based template. The default `{yyyy}/{MMMM}/{dd}` reproduces the original
`yyyy/MMMM/dd` layout, so existing setups need no change.

Supported tokens:

| Token         | Resolves to                                                          |
| ------------- | -------------------------------------------------------------------- |
| `{yyyy}`      | 4-digit year (e.g. `2026`)                                           |
| `{yy}`        | 2-digit year (e.g. `26`)                                             |
| `{MM}`        | 2-digit month (`01`-`12`)                                            |
| `{MMM}`       | Short month name in the active culture (e.g. `May`)                  |
| `{MMMM}`      | Full month name in the active culture (e.g. `May` / `Mai`)           |
| `{dd}`        | 2-digit day (`01`-`31`)                                              |
| `{ddd}`       | Short weekday name in the active culture                             |
| `{dddd}`      | Full weekday name in the active culture                              |
| `{HH}`        | 2-digit hour (`00`-`23`)                                             |
| `{mm}`        | 2-digit minute (`00`-`59`)                                           |
| `{ss}`        | 2-digit second (`00`-`59`)                                           |
| `{Quarter}`   | Calendar quarter (`Q1`-`Q4`)                                         |
| `{Weekday}`   | Same as `{dddd}` (alias)                                             |
| `{WeekOfYear}`| ISO 8601 calendar week (`01`-`53`), invariant 2-digit                |

Rules and safeguards:

- Separators may be `/` or `\`; both render as the OS-native path separator.
- Literal text around tokens is preserved (`photos-{yyyy}/m{MM}` →
  `photos-2026/m05`).
- Empty templates, `.` / `..` segments, unknown tokens, unclosed `{...}` and
  invalid file-name characters are rejected at startup with the failing
  position in the message.
- A file without any usable date (neither EXIF nor file timestamps) still
  lands in `Unknown/` regardless of the template — same fallback as before.
- Date tokens resolve in the active culture (`CULTURE_NAME`), so `{MMMM}`
  produces `May` for `en-US` and `Mai` for `de-DE`.

Examples:

- `{yyyy}/{MM}` → `2026/05`
- `{yyyy}/{Quarter}/{MM}` → `2026/Q2/05`
- `imports-{yyyy}-W{WeekOfYear}` → `imports-2026-W21`
