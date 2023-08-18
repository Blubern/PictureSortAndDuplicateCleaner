using Microsoft.Extensions.Logging;

namespace PictureSort.Cmd;

public class ConsoleProgress : Progress<string>
{
    private readonly ILogger<ConsoleProgress> _logger;

    public ConsoleProgress(ILogger<ConsoleProgress> _logger)
    {
        this._logger = _logger;
    }

    protected override void OnReport(string value)
    {
        _logger.LogDebug(value);
    }
}