using System.Text.RegularExpressions;

internal class Program
{
    private static string execPath = string.Empty;

    private static void Main(string[] args)
    {
        Application app = new Application(args);

        app.Run();

        return;
    }
}