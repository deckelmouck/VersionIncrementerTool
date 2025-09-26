using System.Diagnostics;
using System.Text.RegularExpressions;

public class Application
{
    #region private fields
    private string _executionPath = string.Empty;
    private readonly string[] _args;
    private Part _targetPart;
    private string _csproj = string.Empty;
    #endregion

    #region public methods

    #endregion

    #region constructor
    public Application(string[] args)
    {
        _args = args ?? Array.Empty<string>();
    }
    #endregion

    #region methods
    private void Init()
    {
        Console.WriteLine("Hello there, this is VersionIncrementerTool!");

        _executionPath = Environment.CurrentDirectory;
        #if DEBUG
        Debug.WriteLine($"excecution path: {_executionPath}");
        #endif
    }

    public void Run()
    {
        Init();

        _targetPart = ParseInputArgs(_args);
        Console.WriteLine($"your target: {_targetPart}");

        if (_targetPart == Part.None)
        {

            Application.Exit(1);
            return;
        }

        _csproj = Application.GetCsprojFile(_executionPath);

        if (_csproj == null || _csproj.Equals(string.Empty))
        {
            Console.WriteLine($"there is no *.csproj file in '{_executionPath}'");
            Application.Exit(-1);
            return;
        }
        else
        {
            Console.WriteLine($"csproj-file: {_csproj}");
        }
        
        if(Application.UpdateVersionInCsproj(_csproj, _targetPart))
        {
            Application.Exit(0);
        }
        else
        {
            Application.Exit(-1);
        }
    }

    private Part ParseInputArgs(string[] args)
    {
        if (args.Length == 0)
        {
            return Part.None;
        }

        if (args.Length == 1)
        {
            return Application.ParseOne(_args);
        }
        else
        {
            Console.WriteLine("multiple arguments are not implemented yet!");
            return Part.None;
        }
    }

    private static Part ParseOne(string[] args)
    {
        switch (args[0].ToLower())
        {
            case "major":
                return Part.Major;
            case "minor":
                return Part.Minor;
            case "patch":
                return Part.Patch;
            case "build":
                return Part.Build;
            default:
                Console.WriteLine("Invalid version part. Please provide 'Major', 'Minor', 'Patch' or 'Build'.");
                return Part.None;
        }        
    }

    private static string GetCsprojFile(string csprojFilePath)
    {
        string[] files = Directory.GetFiles(csprojFilePath, "*.csproj", SearchOption.TopDirectoryOnly);
        if (files.Length > 0)
        {
            Console.WriteLine($"Path: {csprojFilePath} - File(s): {files.Length}");
            return files[0];
        }
        return string.Empty;
    }

    private static bool UpdateVersionInCsproj(string csproj, Part targetPart)
    {
        string csprojContent = File.ReadAllText(csproj);
        string pattern = "<Version>(.*?)</Version>";
        Match match = Regex.Match(csprojContent, pattern);
        if (match.Success)
        {
            string version = match.Groups[1].Value;
            Console.WriteLine($"old version {version}");
            string[] versionParts = version.Split('.');

            int major = int.Parse(versionParts[0]);
            int minor = int.Parse(versionParts[1]);
            int patch = int.Parse(versionParts[2]);

            switch (targetPart)
            {
                case Part.Major:
                    major++;
                    minor = 0;
                    patch = 0;
                    break;
                case Part.Minor:
                    minor++;
                    patch = 0;
                    break;
                case Part.Patch:
                    patch++;
                    break;
                default:
                    Console.WriteLine($"increment version with target part {targetPart} not yet implemented");
                    return false;
            }

            string newVersion = $"{major}.{minor}.{patch}";

            string newCsprojContent = csprojContent.Replace($"<Version>{version}</Version>", $"<Version>{newVersion}</Version>");
            File.WriteAllText(csproj, newCsprojContent);

            Console.WriteLine($"version updated to {newVersion}");
            return true;
        }
        else
        {
            Console.WriteLine("no <Version> tag found in the .csproj file.");
            return false;
        }
    }

    private static void Exit(int exitCode)
    {
        ConsoleColor consoleColor = Console.ForegroundColor;
        if(exitCode == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("update successfull");
        }
        else if(exitCode == 1)
        {
            Console.WriteLine("how to use: dotnet vit <Major|Minor|Patch>");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("update failed");
        }

        Console.ForegroundColor = consoleColor;
        Console.WriteLine("please review all changes - thanks for using version incrementer tool");
    }
    #endregion
}