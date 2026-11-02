using System.Diagnostics;
using System.Text.RegularExpressions;

public class Application
{
    #region private fields
    private const string TagOption = "--tag";
    private const string CommitOption = "--commit";
    private const string DefaultVersion = "0.0.0";
    private const string VersionPattern = "<Version>(.*?)</Version>";
    // x.y.z with optional pre-release (-beta1) and build metadata (+abc) suffix
    private static readonly Regex SemVerRegex = new Regex(@"^(\d+)\.(\d+)\.(\d+)(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$");
    private string _executionPath = string.Empty;
    private readonly string[] _args;
    private Part _targetPart;
    private bool _createGitTag;
    private bool _commitChanges;
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
        
        if(Application.UpdateVersionInCsproj(_csproj, _targetPart, out string newVersion))
        {
            if (_commitChanges && !Application.CommitCsproj(_executionPath, _csproj, newVersion))
            {
                Application.Exit(-1);
                return;
            }

            if (_createGitTag && !Application.CreateGitTag(_executionPath, _csproj, newVersion))
            {
                Application.Exit(-1);
                return;
            }

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
        List<string> parts = new List<string>();

        foreach (string arg in args)
        {
            if (arg.Equals(TagOption, StringComparison.OrdinalIgnoreCase))
            {
                _createGitTag = true;
            }
            else if (arg.Equals(CommitOption, StringComparison.OrdinalIgnoreCase))
            {
                _commitChanges = true;
            }
            else if (arg.StartsWith("-"))
            {
                Console.WriteLine($"Unknown option '{arg}'.");
                return Part.None;
            }
            else
            {
                parts.Add(arg);
            }
        }

        if (parts.Count == 0)
        {
            return Part.None;
        }

        if (parts.Count == 1)
        {
            return Application.ParseOne(parts.ToArray());
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

    private static bool UpdateVersionInCsproj(string csproj, Part targetPart, out string newVersion)
    {
        newVersion = string.Empty;
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

        newVersion = $"{major}.{minor}.{patch}";
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

    // commits only the changed csproj, other changes in the working tree stay untouched
    private static bool CommitCsproj(string workingDirectory, string csproj, string version)
    {
        if (!Application.RunGit(workingDirectory, out string error, "rev-parse", "--is-inside-work-tree"))
        {
            Console.WriteLine($"version updated, but the changes were not committed: not a git repository or git is not available ({error})");
            return false;
        }

        // add first so that a csproj which is not tracked yet can be committed as well
        if (!Application.RunGit(workingDirectory, out error, "add", "--", csproj)
            || !Application.RunGit(workingDirectory, out error, "commit", "-m", $"Bump version to {version}", "--", csproj))
        {
            Console.WriteLine($"version updated, but the changes were not committed: {error}");
            return false;
        }

        Console.WriteLine($"changes committed: Bump version to {version}");
        return true;
    }

    // creates a lightweight tag named like the new version on the current commit
    private static bool CreateGitTag(string workingDirectory, string csproj, string version)
    {
        if (!Application.RunGit(workingDirectory, out string error, "rev-parse", "--is-inside-work-tree"))
        {
            Console.WriteLine($"version updated, but the git tag was not created: not a git repository or git is not available ({error})");
            return false;
        }

        if (!Application.RunGit(workingDirectory, out error, "tag", version))
        {
            Console.WriteLine($"version updated, but the git tag '{version}' was not created: {error}");
            return false;
        }

        Console.WriteLine($"git tag created: {version}");

        // the tag points to the current commit, which does not contain the new version yet
        if (Application.RunGit(workingDirectory, out string status, "status", "--porcelain", "--", csproj) && status.Length > 0)
        {
            Console.WriteLine($"note: '{Path.GetFileName(csproj)}' has uncommitted changes, so the tag does not point to a commit with the new version. Commit the change and move the tag with 'git tag -f {version}' if needed.");
        }

        return true;
    }

    // runs git and returns true on exit code 0; output is stdout on success, otherwise stderr
    private static bool RunGit(string workingDirectory, out string output, params string[] arguments)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            using Process process = Process.Start(startInfo)!;
            string stdout = process.StandardOutput.ReadToEnd().Trim();
            string stderr = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit();

            output = process.ExitCode == 0 ? stdout : stderr;
            return process.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            output = ex.Message;
            return false;
        }
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
            Console.WriteLine("how to use: dotnet vit <Major|Minor|Patch> [--commit] [--tag]");
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