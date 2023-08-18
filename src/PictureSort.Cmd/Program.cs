using System.Diagnostics;
using Karambolo.Extensions.Logging.File;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PictureSort;
using PictureSort.Cmd;

var cancellationTokenSource = new CancellationTokenSource();

var serviceProvider = new ServiceCollection()
    .AddScoped<PictureSorter>()
    .AddScoped<InventoryDirectory>()
    .AddLogging(loggingBuilder => loggingBuilder
        .AddConsole()
        .AddFile(o =>
        {
            o.RootPath = @"E:\Logging";
            o.Files = new[]
            {
                new LogFileOptions
                {
                    Path = "pictureSort-<date>-<counter>.log",
                    DateFormat = "yyyy",
                    MinLevel = new Dictionary<string, LogLevel>
                    {
                        ["Karambolo.Extensions.Logging.File"] = LogLevel.None,
                        ["Default"] = LogLevel.Debug,
                    }
                }
            };
        })
        .SetMinimumLevel(LogLevel.Debug))
    .BuildServiceProvider();

var pictureSorter = serviceProvider.GetRequiredService<PictureSorter>();
var parameter = new PictureSortParameter(
    @"P:\Tobias",
    @"P:\Tobias2",
    8);

try
{
    var result = await pictureSorter.StartPictureSortAsync(
        parameter,
        new ConsoleProgress(serviceProvider.GetRequiredService<ILogger<ConsoleProgress>>()),
        cancellationTokenSource.Token);
}
catch (Exception e)
{
    Console.WriteLine(e);
    Debugger.Break();
    throw;
}

Console.WriteLine("Hello, World!");