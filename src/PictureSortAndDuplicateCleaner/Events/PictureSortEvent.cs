namespace PictureSortAndDuplicateCleaner.Events;

/// <summary>
/// Base type for typed progress events emitted by <see cref="PictureSorter"/>. Consumers
/// can subscribe by passing an <see cref="IProgress{T}"/> of <see cref="PictureSortEvent"/>
/// to <c>StartPictureSortAsync</c>; the legacy <see cref="IProgress{T}"/> of <c>string</c>
/// sink remains supported in parallel for backwards compatibility.
/// </summary>
public abstract record PictureSortEvent;
