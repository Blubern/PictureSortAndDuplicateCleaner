namespace PictureSort.Cmd;

public class ConsoleProgress : Progress<string>
{
    protected override void OnReport(string value)
    {
        Console.Out.WriteLine(value);
    }
}