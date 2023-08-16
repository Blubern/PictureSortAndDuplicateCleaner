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
        .SetMinimumLevel(LogLevel.Debug))
    .BuildServiceProvider();

var pictureSorter = serviceProvider.GetRequiredService<PictureSorter>();
var parameter = new PictureSortParameter(
    @"P:\Sonam",
    @"P:\Sonam2",
    7);

var result = await pictureSorter.StartPictureSortAsync(
    parameter,
    new ConsoleProgress(),
    cancellationTokenSource.Token);

Console.WriteLine("Hello, World!");