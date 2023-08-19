using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PictureSort;
using PictureSort.Cmd;

var cancellationTokenSource = new CancellationTokenSource();
var sourceDirectories = new List<string>(); 

Console.Out.WriteLine("Try to read Environment Variable.");
var pictureSource = Environment.GetEnvironmentVariable("PICTURE_SOURCE");

if (string.IsNullOrWhiteSpace(pictureSource))
{
    Console.Out.WriteLine("You have to specify a Source Directory on the Env Variable PICTURE_SOURCE");
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
    Console.Out.WriteLine("You have to specify a Target Directory on the Env Variable PICTURE_TARGET");
    return;
}

var loggingTarget = Environment.GetEnvironmentVariable("LOGGING_TARGET");
if (string.IsNullOrWhiteSpace(loggingTarget))
{
    Console.Out.WriteLine("You have to specify a Logging Directory on the Env Variable LOGGING_TARGET");
    return;
}

if (!Directory.Exists(loggingTarget))
{
    Console.Out.WriteLine($"The Logging Directory '{loggingTarget}' does not exist.");
    return;
}

var maxConcurrency = Environment.GetEnvironmentVariable("MAX_CONCURRENCY") == null ? Environment.ProcessorCount : Convert.ToInt32(Environment.GetEnvironmentVariable("MAX_CONCURRENCY"));
var duplicateFolderName = Environment.GetEnvironmentVariable("DUPLICATE_FOLDER_NAME") ?? "!Duplicate";
var alreadyExistingFolderName = Environment.GetEnvironmentVariable("ALREADY_EXISTING_FOLDER_NAME") ?? "!ExistsInTarget";

var serviceProvider = new ServiceCollection()
    .AddScoped<PictureSorter>()
    .AddScoped<InventoryDirectory>()
    .AddLogging(loggingBuilder => loggingBuilder
        .AddConsole()
        .AddFile( loggingTarget + "\\pictureSorter-{Date}.txt")
        .SetMinimumLevel(LogLevel.Debug))
    .BuildServiceProvider();

var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

var pictureSorter = serviceProvider.GetRequiredService<PictureSorter>();
var parameter = new PictureSortParameter(
    sourceDirectories,
    pictureTarget,
    maxConcurrency,
    duplicateFolderName,
    alreadyExistingFolderName);

try
{
    var result = await pictureSorter.StartPictureSortAsync(
        parameter,
        new ConsoleProgress(serviceProvider.GetRequiredService<ILogger<ConsoleProgress>>()),
        cancellationTokenSource.Token);
}
catch (Exception e)
{
    logger.LogError(e, "Something is wrong :(");
    Debugger.Break();
    throw;
}

Console.WriteLine("Finished with all ... Have a good day :) ...");