using System.Diagnostics;
using System.Text.RegularExpressions;

public class Application
{
    #region private fields
    private const string DefaultVersion = "0.0.0";
    private const string VersionPattern = "<Version>(.*?)</Version>";
    // x.y.z with optional pre-release (-beta1) and build metadata (+abc) suffix
    private static readonly Regex SemVerRegex = new Regex(@"^(\d+)\.(\d+)\.(\d+)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$");
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
        Debug.WriteLine($"execution path: {_executionPath}");
        #endif
    }

    public void Run()
    {
        Init();

        if (_args.Length == 0)
        {
            ShowInfo();
            Application.Exit(1);
            return;
        }

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

    private void ShowInfo()
    {
        string csproj = Application.GetCsprojFile(_executionPath);

        if (csproj.Equals(string.Empty))
        {
            Console.WriteLine($"there is no *.csproj file in '{_executionPath}'");
            return;
        }

        Console.WriteLine($"csproj-file: {csproj}");

        Match match = Regex.Match(File.ReadAllText(csproj), VersionPattern);
        if (match.Success)
        {
            Console.WriteLine($"current version: {match.Groups[1].Value}");
        }
        else
        {
            Console.WriteLine("no <Version> tag found in the .csproj file.");
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
            default:
                Console.WriteLine("Invalid version part. Please provide 'Major', 'Minor' or 'Patch'.");
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
        Match match = Regex.Match(csprojContent, VersionPattern);

        // without a <Version> tag the increment starts at 0.0.0
        string version = match.Success ? match.Groups[1].Value : DefaultVersion;
        if (match.Success)
        {
            Console.WriteLine($"old version {version}");
        }
        else
        {
            Console.WriteLine($"no <Version> tag found in the .csproj file, starting at {DefaultVersion}");
        }

        Match semVer = SemVerRegex.Match(version.Trim());
        if (!semVer.Success
            || !int.TryParse(semVer.Groups[1].Value, out int major)
            || !int.TryParse(semVer.Groups[2].Value, out int minor)
            || !int.TryParse(semVer.Groups[3].Value, out int patch))
        {
            Console.WriteLine($"unsupported version format '{version}'. Supported formats: x.y.z, x.y.z-suffix");
            return false;
        }

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
                Console.WriteLine($"unsupported version part {targetPart}");
                return false;
        }

        string newVersion = $"{major}.{minor}.{patch}";
        string newCsprojContent;

        if (match.Success)
        {
            newCsprojContent = csprojContent.Replace($"<Version>{version}</Version>", $"<Version>{newVersion}</Version>");
        }
        else
        {
            string? inserted = Application.InsertVersionTag(csprojContent, newVersion);
            if (inserted == null)
            {
                Console.WriteLine("no <PropertyGroup> found in the .csproj file, cannot add a <Version> tag.");
                return false;
            }
            newCsprojContent = inserted;
            Console.WriteLine("added <Version> tag to the first <PropertyGroup>");
        }

        File.WriteAllText(csproj, newCsprojContent);

        Console.WriteLine($"version updated to {newVersion}");
        return true;
    }

    // adds the tag as first child of the first <PropertyGroup> without attributes (no conditional group)
    private static string? InsertVersionTag(string csprojContent, string version)
    {
        Match group = Regex.Match(csprojContent, @"<PropertyGroup\s*>");
        if (!group.Success)
        {
            return null;
        }

        string lineBreak = csprojContent.Contains("\r\n") ? "\r\n" : "\n";
        int insertAt = group.Index + group.Length;

        Match child = Regex.Match(csprojContent.Substring(insertAt), @"^[ \t]*\r?\n([ \t]*)\S");
        string indent = child.Success ? child.Groups[1].Value : "    ";

        return csprojContent.Insert(insertAt, $"{lineBreak}{indent}<Version>{version}</Version>");
    }

    private static void Exit(int exitCode)
    {
        ConsoleColor consoleColor = Console.ForegroundColor;
        if(exitCode == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("update successful");
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