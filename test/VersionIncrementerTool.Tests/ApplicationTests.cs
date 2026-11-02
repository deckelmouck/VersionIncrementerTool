using System.Diagnostics;
using System.Xml.Linq;

public class ApplicationTests : IDisposable
{
    private readonly string _workDir;
    private readonly string _originalDir;
    private readonly TextWriter _originalOut;
    private readonly StringWriter _output = new();

    public ApplicationTests()
    {
        _originalDir = Environment.CurrentDirectory;
        _originalOut = Console.Out;
        _workDir = Directory.CreateTempSubdirectory("vit-tests-").FullName;
        Environment.CurrentDirectory = _workDir;
        Console.SetOut(_output);

        // identity for commits made by the tool (child processes inherit the environment)
        foreach ((string key, string value) in GitEnvironment)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static readonly (string Key, string Value)[] GitEnvironment =
    {
        ("GIT_AUTHOR_NAME", "test"), ("GIT_AUTHOR_EMAIL", "test@example.com"),
        ("GIT_COMMITTER_NAME", "test"), ("GIT_COMMITTER_EMAIL", "test@example.com"),
        ("GIT_CONFIG_COUNT", "1"), ("GIT_CONFIG_KEY_0", "commit.gpgsign"), ("GIT_CONFIG_VALUE_0", "false"),
    };

    public void Dispose()
    {
        foreach ((string key, _) in GitEnvironment)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
        Console.SetOut(_originalOut);
        Environment.CurrentDirectory = _originalDir;
        Directory.Delete(_workDir, true);
    }

    private static string Csproj(string? version) => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
        {(version is null ? "" : $"    <Version>{version}</Version>\n")}  </PropertyGroup>
        </Project>
        """;

    private string WriteCsproj(string? version, string name = "test.csproj")
    {
        string path = Path.Combine(_workDir, name);
        File.WriteAllText(path, Csproj(version));
        return path;
    }

    private static string? FirstGroupVersion(string path) =>
        XDocument.Load(path).Root!.Element("PropertyGroup")?.Element("Version")?.Value;

    private string Git(params string[] args)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = _workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string arg in new[] { "-c", "user.name=test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false", "-c", "tag.gpgsign=false" }.Concat(args))
        {
            startInfo.ArgumentList.Add(arg);
        }
        using var process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd().Trim();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        return output;
    }

    // git repository with the csproj committed, so the working tree is clean
    private string InitRepoWithCsproj(string version)
    {
        string path = WriteCsproj(version);
        Git("init", "-q");
        Git("add", ".");
        Git("commit", "-q", "-m", "init");
        return path;
    }

    private string Run(params string[] args)
    {
        new Application(args).Run();
        return _output.ToString();
    }

    [Theory]
    [InlineData("major", "1.2.3", "2.0.0")]
    [InlineData("minor", "1.2.3", "1.3.0")]
    [InlineData("patch", "1.2.3", "1.2.4")]
    [InlineData("MAJOR", "0.9.9", "1.0.0")]
    [InlineData("Patch", "1.2.9", "1.2.10")]
    public void IncrementsRequestedPart(string part, string before, string after)
    {
        string path = WriteCsproj(before);

        string output = Run(part);

        Assert.Equal(Csproj(after), File.ReadAllText(path));
        Assert.Contains($"version updated to {after}", output);
        Assert.Contains("update successful", output);
    }

    [Theory]
    [InlineData("patch", "1.2.3-beta1", "1.2.4")]
    [InlineData("minor", "1.2.3-rc.1", "1.3.0")]
    [InlineData("major", "1.2.3+build5", "2.0.0")]
    [InlineData("patch", "1.0.0-beta.2+sha.abc", "1.0.1")]
    public void RemovesPreReleaseAndBuildSuffixWhenIncrementing(string part, string before, string after)
    {
        string path = WriteCsproj(before);

        string output = Run(part);

        Assert.Equal(Csproj(after), File.ReadAllText(path));
        Assert.Contains("update successful", output);
    }

    [Theory]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("1")]
    [InlineData("a.b.c")]
    [InlineData("1.2.x")]
    [InlineData("1.2.3-")]
    [InlineData("")]
    [InlineData("99999999999.0.0")]
    public void RejectsUnsupportedVersionFormatAndKeepsFile(string version)
    {
        string path = WriteCsproj(version);

        string output = Run("patch");

        Assert.Equal(Csproj(version), File.ReadAllText(path));
        Assert.Contains("unsupported version format", output);
        Assert.Contains("update failed", output);
    }

    [Fact]
    public void ReportsErrorWhenNoCsprojExists()
    {
        string output = Run("patch");

        Assert.Contains("there is no *.csproj file", output);
        Assert.Contains("update failed", output);
    }

    [Theory]
    [InlineData("patch", "0.0.1")]
    [InlineData("minor", "0.1.0")]
    [InlineData("major", "1.0.0")]
    public void AddsVersionTagStartingAtZeroWhenMissing(string part, string expected)
    {
        string path = WriteCsproj(null);

        string output = Run(part);

        Assert.Equal(expected, FirstGroupVersion(path));
        Assert.Contains("TargetFramework", File.ReadAllText(path));
        Assert.Contains("added <Version> tag", output);
        Assert.Contains("update successful", output);
    }

    [Fact]
    public void DoesNotAddVersionTagForInvalidPart()
    {
        string path = WriteCsproj(null);

        string output = Run("foo");

        Assert.Equal(Csproj(null), File.ReadAllText(path));
        Assert.Contains("Invalid version part", output);
    }

    [Fact]
    public void FailsAndKeepsFileWhenThereIsNoPropertyGroup()
    {
        string content = "<Project Sdk=\"Microsoft.NET.Sdk\">\n</Project>";
        string path = Path.Combine(_workDir, "test.csproj");
        File.WriteAllText(path, content);

        string output = Run("patch");

        Assert.Equal(content, File.ReadAllText(path));
        Assert.Contains("no <PropertyGroup> found", output);
        Assert.Contains("update failed", output);
    }

    [Fact]
    public void AddsVersionTagToFirstUnconditionalPropertyGroup()
    {
        string path = Path.Combine(_workDir, "test.csproj");
        File.WriteAllText(path, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
                <DefineConstants>DEBUG</DefineConstants>
              </PropertyGroup>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        Run("patch");

        var groups = XDocument.Load(path).Root!.Elements("PropertyGroup").ToList();
        Assert.Null(groups[0].Element("Version"));
        Assert.Equal("0.0.1", groups[1].Element("Version")?.Value);
    }

    [Fact]
    public void ShowsUsageAndCurrentVersionWithoutArguments()
    {
        string path = WriteCsproj("1.2.3");

        string output = Run();

        Assert.Equal(Csproj("1.2.3"), File.ReadAllText(path));
        Assert.Contains("csproj-file:", output);
        Assert.Contains("test.csproj", output);
        Assert.Contains("current version: 1.2.3", output);
        Assert.Contains("how to use: dotnet vit", output);
    }

    [Fact]
    public void ShowsUsageWithoutArgumentsWhenVersionTagIsMissing()
    {
        string path = WriteCsproj(null);

        string output = Run();

        Assert.Equal(Csproj(null), File.ReadAllText(path));
        Assert.Contains("no <Version> tag found", output);
        Assert.Contains("how to use: dotnet vit", output);
    }

    [Fact]
    public void ShowsUsageWithoutArgumentsWhenNoCsprojExists()
    {
        string output = Run();

        Assert.Contains("there is no *.csproj file", output);
        Assert.Contains("how to use: dotnet vit", output);
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("build")]
    public void RejectsInvalidPart(string part)
    {
        string path = WriteCsproj("1.2.3");

        string output = Run(part);

        Assert.Equal(Csproj("1.2.3"), File.ReadAllText(path));
        Assert.Contains("Invalid version part", output);
    }

    [Fact]
    public void RejectsMultipleArguments()
    {
        string path = WriteCsproj("1.2.3");

        string output = Run("major", "minor");

        Assert.Equal(Csproj("1.2.3"), File.ReadAllText(path));
        Assert.Contains("multiple arguments are not implemented yet", output);
    }

    [Fact]
    public void IncrementsVersionInExampleCsproj()
    {
        string source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "test.csproj");
        string target = Path.Combine(_workDir, "test.csproj");
        File.Copy(source, target);

        Run("minor");

        string content = File.ReadAllText(target);
        Assert.Contains("<Version>1.5.0</Version>", content);
        // other version related properties stay untouched
        Assert.Contains("<InformationalVersion>1.4.6-beta</InformationalVersion>", content);
    }

    [Theory]
    [InlineData("ClassLibrary.csproj")]
    [InlineData("ConsoleApplication.csproj")]
    [InlineData("WebApplication.csproj")]
    [InlineData("WPFApplication.csproj")]
    public void AddsVersionTagToExampleCsprojAndKeepsLineEndings(string fixture)
    {
        string source = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture);
        string target = Path.Combine(_workDir, fixture);
        File.Copy(source, target);

        Run("patch");

        string content = File.ReadAllText(target);
        Assert.Equal("0.0.1", FirstGroupVersion(target));
        // the example files use CRLF, the inserted line must not introduce a lone LF
        Assert.DoesNotContain("\n", content.Replace("\r\n", ""));
    }

    [Theory]
    [InlineData("--tag", "patch")]
    [InlineData("patch", "--tag")]
    [InlineData("PATCH", "--TAG")]
    public void CreatesGitTagWithNewVersion(params string[] args)
    {
        string path = InitRepoWithCsproj("1.2.3");

        string output = Run(args);

        Assert.Contains("<Version>1.2.4</Version>", File.ReadAllText(path));
        Assert.Equal("1.2.4", Git("tag", "--list"));
        Assert.Contains("git tag created: 1.2.4", output);
        Assert.Contains("update successful", output);
        // csproj is modified but not committed
        Assert.Contains("uncommitted changes", output);
    }

    [Fact]
    public void CreatesNoGitTagWithoutTagOption()
    {
        InitRepoWithCsproj("1.2.3");

        Run("patch");

        Assert.Equal("", Git("tag", "--list"));
    }

    [Fact]
    public void ReportsErrorWhenTagCannotBeCreatedOutsideOfGitRepository()
    {
        string path = WriteCsproj("1.2.3");
        // make sure the temp dir is not part of a repository
        Environment.SetEnvironmentVariable("GIT_CEILING_DIRECTORIES", Path.GetDirectoryName(_workDir));

        try
        {
            string output = Run("patch", "--tag");

            Assert.Contains("<Version>1.2.4</Version>", File.ReadAllText(path));
            Assert.Contains("git tag was not created", output);
            Assert.Contains("update failed", output);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_CEILING_DIRECTORIES", null);
        }
    }

    [Fact]
    public void ReportsErrorWhenTagAlreadyExists()
    {
        InitRepoWithCsproj("1.2.3");
        Git("tag", "1.2.4");

        string output = Run("patch", "--tag");

        Assert.Contains("'1.2.4' was not created", output);
        Assert.Contains("update failed", output);
    }

    [Fact]
    public void ShowsUsageWhenOnlyTagOptionIsGiven()
    {
        string path = InitRepoWithCsproj("1.2.3");

        string output = Run("--tag");

        Assert.Equal(Csproj("1.2.3"), File.ReadAllText(path));
        Assert.Equal("", Git("tag", "--list"));
        Assert.Contains("how to use: dotnet vit <Major|Minor|Patch> [--commit] [--tag]", output);
    }

    [Fact]
    public void RejectsUnknownOption()
    {
        string path = WriteCsproj("1.2.3");

        string output = Run("patch", "--foo");

        Assert.Equal(Csproj("1.2.3"), File.ReadAllText(path));
        Assert.Contains("Unknown option '--foo'", output);
    }

    [Fact]
    public void CommitsOnlyTheCsproj()
    {
        string path = InitRepoWithCsproj("1.2.3");
        string other = Path.Combine(_workDir, "other.txt");
        File.WriteAllText(other, "a");
        Git("add", "other.txt");
        Git("commit", "-q", "-m", "other");
        File.WriteAllText(other, "changed");

        string output = Run("patch", "--commit");

        Assert.Equal("Bump version to 1.2.4", Git("log", "-1", "--format=%s"));
        Assert.Equal("", Git("status", "--porcelain", "--", path));
        Assert.Equal("M other.txt", Git("status", "--porcelain", "--", "other.txt"));
        Assert.Equal("", Git("tag", "--list"));
        Assert.Contains("changes committed: Bump version to 1.2.4", output);
        Assert.Contains("update successful", output);
    }

    [Theory]
    [InlineData("--commit", "--tag")]
    [InlineData("--tag", "--commit")]
    public void CommitsBeforeTaggingSoTheTagContainsTheNewVersion(string first, string second)
    {
        InitRepoWithCsproj("1.2.3");

        string output = Run("patch", first, second);

        Assert.Equal(Git("rev-parse", "HEAD"), Git("rev-list", "-n", "1", "1.2.4"));
        Assert.Contains("<Version>1.2.4</Version>", Git("show", "1.2.4:test.csproj"));
        Assert.DoesNotContain("uncommitted changes", output);
        Assert.Contains("update successful", output);
    }

    [Fact]
    public void CommitsCsprojThatIsNotTrackedYet()
    {
        Git("init", "-q");
        Git("commit", "-q", "--allow-empty", "-m", "init");
        string path = WriteCsproj("1.2.3");

        Run("patch", "--commit");

        Assert.Equal("Bump version to 1.2.4", Git("log", "-1", "--format=%s"));
        Assert.Equal("", Git("status", "--porcelain", "--", path));
    }

    [Fact]
    public void ReportsErrorWhenCommitIsNotPossibleOutsideOfGitRepository()
    {
        string path = WriteCsproj("1.2.3");
        Environment.SetEnvironmentVariable("GIT_CEILING_DIRECTORIES", Path.GetDirectoryName(_workDir));

        try
        {
            string output = Run("patch", "--commit");

            Assert.Contains("<Version>1.2.4</Version>", File.ReadAllText(path));
            Assert.Contains("changes were not committed", output);
            Assert.Contains("update failed", output);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_CEILING_DIRECTORIES", null);
        }
    }

    [Fact]
    public void DoesNotCommitWithoutCommitOption()
    {
        string path = InitRepoWithCsproj("1.2.3");

        Run("patch");

        Assert.Equal("init", Git("log", "-1", "--format=%s"));
        Assert.NotEqual("", Git("status", "--porcelain", "--", path));
    }
}
