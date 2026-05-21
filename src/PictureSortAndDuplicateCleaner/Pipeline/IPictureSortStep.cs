namespace PictureSortAndDuplicateCleaner.Pipeline;

/// <summary>
/// Marker interface for a single step in the picture-sort pipeline. The current
/// <see cref="PictureSorter"/> implements all stages inline as a starting point; concrete
/// step classes implementing these interfaces are introduced incrementally so that
/// individual stages can be unit-tested in isolation and re-composed via DI.
///
/// Stages, in execution order:
///   1. <see cref="IInventoryStep"/>  — enumerate source + target, compute hashes.
///   2. <see cref="IDuplicateStep"/>  — mark hash-duplicates within source/target, move to !Duplicate.
///   3. <see cref="IExistingInTargetStep"/> — match source hashes against target+journal, move to !ExistsInTarget.
///   4. <see cref="IMoveStep"/>       — move remaining files into the date-folder structure.
///   5. <see cref="ICleanupStep"/>    — delete empty source directories, report orphan sidecars.
/// </summary>
public interface IPictureSortStep
{
    string Name { get; }
}
