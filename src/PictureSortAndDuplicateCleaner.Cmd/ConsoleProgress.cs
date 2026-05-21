using Serilog;

namespace PictureSortAndDuplicateCleaner.Cmd;

public class ConsoleProgress : Progress<string>
{
    protected override void OnReport(string value)
    {
        Log.Debug(value);
    }
}