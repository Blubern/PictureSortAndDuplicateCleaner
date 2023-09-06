using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using PictureSort;
using PictureSort.Cmd;
using Serilog;

var cancellationTokenSource = new CancellationTokenSource();
var sourceDirectories = new List<string>(); 

var loggingTarget = Environment.GetEnvironmentVariable("LOGGING_TARGET") ?? "pictureSortLogging.txt";

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(loggingTarget, rollingInterval: RollingInterval.Day)
    .WriteTo.Console()
    .CreateLogger();

Log.Debug("Try to read Environment Variable");
var pictureSource = Environment.GetEnvironmentVariable("PICTURE_SOURCE");

if (string.IsNullOrWhiteSpace(pictureSource))
{
    Log.Error("You have to specify a Source Directory on the Env Variable PICTURE_SOURCE");
    return;
}

if (pictureSource.Contains(';'))
{
    sourceDirectories.AddRange(pictureSource.Split(';'));
}
else
{
    sourceDirectories.Add(pictureSource);
}

var pictureTarget = Environment.GetEnvironmentVariable("PICTURE_TARGET");

if (string.IsNullOrWhiteSpace(pictureTarget))
{
    Log.Error("You have to specify a Target Directory on the Env Variable PICTURE_TARGET");
    return;
}

var envInventoryOfTheTargetDirectory = Environment.GetEnvironmentVariable("INVENTOR_OF_THE_TARGET_DIRECTORY") ?? bool.TrueString;
if (!bool.TryParse(envInventoryOfTheTargetDirectory, out var inventoryOfTheTargetDirectory))
{
    Log.Error("You have to specify a valid value for INVENTOR_OF_THE_TARGET_DIRECTORY - Valid Values are: {TrueString}, {FalseString}. ", bool.TrueString, bool.FalseString);
    return;
}

var maxConcurrency = Environment.GetEnvironmentVariable("MAX_CONCURRENCY") == null ? Environment.ProcessorCount : Convert.ToInt32(Environment.GetEnvironmentVariable("MAX_CONCURRENCY"));
var duplicateFolderName = Environment.GetEnvironmentVariable("DUPLICATE_FOLDER_NAME") ?? "!Duplicate";
var alreadyExistingFolderName = Environment.GetEnvironmentVariable("ALREADY_EXISTING_FOLDER_NAME") ?? "!ExistsInTarget";
var cultureName = Environment.GetEnvironmentVariable("CULTURE_NAME") ?? "en-US";

var serviceProvider = new ServiceCollection()
    .AddScoped<PictureSorter>()
    .AddScoped<InventoryDirectory>()
    .AddScoped<ConsoleProgress>()
    .BuildServiceProvider();

Log.Debug("PICTURE_SOURCE: {PictureSource}", pictureSource);
Log.Debug("LOGGING_TARGET: {PictureSource}", loggingTarget);
Log.Debug("PICTURE_TARGET: {PictureSource}", pictureTarget);
Log.Debug("MAX_CONCURRENCY: {PictureSource}", maxConcurrency);
Log.Debug("DUPLICATE_FOLDER_NAME: {PictureSource}", duplicateFolderName);
Log.Debug("ALREADY_EXISTING_FOLDER_NAME: {PictureSource}", alreadyExistingFolderName);
Log.Debug("CULTURE_NAME: {CultureName}", cultureName);
Log.Debug("INVENTOR_OF_THE_TARGET_DIRECTORY: {InventoryOfTheTargetDirectory}", inventoryOfTheTargetDirectory);

var cultureInfo = new CultureInfo(cultureName);
Thread.CurrentThread.CurrentCulture = cultureInfo;
Thread.CurrentThread.CurrentUICulture = cultureInfo;

var pictureSorter = serviceProvider.GetRequiredService<PictureSorter>();
var parameter = new PictureSortParameter(
    sourceDirectories,
    pictureTarget,
    maxConcurrency,
    duplicateFolderName,
    alreadyExistingFolderName,
    inventoryOfTheTargetDirectory);

try
{
    var result = await pictureSorter.StartPictureSortAsync(
        parameter,
        serviceProvider.GetRequiredService<ConsoleProgress>(),
        cancellationTokenSource.Token);
}
catch (Exception e)
{
    Log.Error(e, "Something is wrong :(");
    Debugger.Break();
    throw;
}

Log.Information("Finished with all ... Have a good day :) ...");