using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PictureSortAndDuplicateCleaner;
using PictureSortAndDuplicateCleaner.Abstractions;
using PictureSortAndDuplicateCleaner.Cmd;
using Serilog;

if (!PictureSortEnvironmentOptions.TryCreate(Environment.GetEnvironmentVariable, out var options, out var error)
    || options is null)
{
    Console.Error.WriteLine(error);
    return ExitCodes.InvalidConfiguration;
}

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(options.LoggingTarget, rollingInterval: RollingInterval.Day)
    .WriteTo.Console()
    .CreateLogger();

Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.PictureSourceVariable, string.Join(';', options.SourceDirectories));
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.PictureTargetVariable, options.TargetDirectory);
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.MaxConcurrencyVariable, options.MaxConcurrency);
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.DuplicateFolderNameVariable, options.DuplicateFolderName);
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.AlreadyExistingFolderNameVariable, options.AlreadyExistingFolderName);
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.CultureNameVariable, options.CultureName);
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.InventoryOfTheTargetDirectoryVariable, options.InventoryOfTheTargetDirectory);
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.LoggingTargetVariable, options.LoggingTarget);
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.SidecarExtensionsVariable, string.Join(';', options.SidecarExtensions));
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.JournalFileVariable, options.JournalFilePath ?? "<disabled>");
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.FolderTemplateVariable, options.FolderTemplate.RawTemplate);
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.UnknownDatePolicyVariable, options.UnknownDatePolicy);
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.DryRunVariable, options.DryRun);
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.OperationModeVariable, options.OperationMode);
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.DuplicateVerificationVariable, options.DuplicateVerification);
Log.Debug("{Variable}: {Value}", PictureSortEnvironmentOptions.HashModeVariable, options.HashMode);

var cultureInfo = CultureInfo.GetCultureInfo(options.CultureName);
CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
Thread.CurrentThread.CurrentCulture = cultureInfo;
Thread.CurrentThread.CurrentUICulture = cultureInfo;

PictureSortParameter parameter;
try
{
    parameter = new PictureSortParameter(
        options.SourceDirectories,
        options.TargetDirectory,
        options.MaxConcurrency,
        options.DuplicateFolderName,
        options.AlreadyExistingFolderName,
        options.InventoryOfTheTargetDirectory,
        options.SidecarExtensions,
        options.JournalFilePath,
        options.FolderTemplate,
        options.UnknownDatePolicy,
        options.DryRun,
        options.OperationMode,
        options.DuplicateVerification,
        options.HashMode);
    PictureSortParameterValidator.Validate(parameter);
}
catch (ArgumentException ex)
{
    Log.Error(ex, "Invalid configuration: {Message}", ex.Message);
    return ExitCodes.InvalidConfiguration;
}

await using var serviceProvider = new ServiceCollection()
    .AddScoped<PictureSorter>()
    .AddSingleton<IFileSystem>(DefaultFileSystem.Instance)
    .AddSingleton<IContentHasher>(sp =>
    {
        var fs = sp.GetRequiredService<IFileSystem>();
        var fileBytes = new FileBytesContentHasher(fs);
        return parameter.HashMode switch
        {
            HashMode.Pixel => new PixelContentHasher(fs, fileBytes, message => Log.Warning("{Message}", message)),
            _ => fileBytes,
        };
    })
    .AddScoped(sp => new InventoryDirectory(sp.GetRequiredService<IFileSystem>(), sp.GetRequiredService<IContentHasher>()))
    .AddScoped<ConsoleProgress>()
    .BuildServiceProvider();

using var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cancellationTokenSource.Cancel();
};

var pictureSorter = serviceProvider.GetRequiredService<PictureSorter>();
var progress = serviceProvider.GetRequiredService<ConsoleProgress>();

try
{
    var result = await pictureSorter.StartPictureSortAsync(parameter, progress, cancellationTokenSource.Token);

    Log.Information(
        "Finished. Source files: {SourceFilesFound}, target files: {TargetFilesFound}, moved to target: {FilesMovedToTarget}, duplicates moved: {DuplicateFilesMoved}, already in target: {AlreadyExistingFilesMoved}, ignored: {TotalFilesIgnored}, errors: {ErrorCount}, sidecars moved: {SidecarsMoved}, sidecars orphaned: {SidecarsOrphaned}, files without date skipped: {FilesWithoutDateSkipped}, journal loaded/written/stale: {JournalLoaded}/{JournalWritten}/{JournalStale}.",
        result.SourceFilesFound,
        result.TargetFilesFound,
        result.FilesMovedToTarget,
        result.DuplicateFilesMoved,
        result.AlreadyExistingFilesMoved,
        result.TotalFilesIgnored,
        result.ErrorCount,
        result.SidecarsMoved,
        result.SidecarsOrphaned,
        result.FilesWithoutDateSkipped,
        result.JournalEntriesLoaded,
        result.JournalEntriesWritten,
        result.JournalEntriesStale);

    return result.HasErrors ? ExitCodes.CompletedWithFileErrors : ExitCodes.Success;
}
catch (OperationCanceledException)
{
    Log.Warning("Picture sort was cancelled by the user.");
    return ExitCodes.UnhandledError;
}
catch (Exception ex)
{
    Log.Error(ex, "Picture sort failed: {Message}", ex.Message);
    return ExitCodes.UnhandledError;
}
finally
{
    await Log.CloseAndFlushAsync();
}
