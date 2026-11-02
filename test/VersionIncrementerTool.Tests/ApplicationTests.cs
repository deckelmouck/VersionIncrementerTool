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
    }

    public void Dispose()
    {
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
    public void DoesNotAddVersionTagForNotImplementedPart()
    {
        string path = WriteCsproj(null);

        string output = Run("build");

        Assert.Equal(Csproj(null), File.ReadAllText(path));
        Assert.Contains("not yet implemented", output);
        Assert.Contains("update failed", output);
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

    [Fact]
    public void RejectsInvalidPart()
    {
        string path = WriteCsproj("1.2.3");

        string output = Run("foo");

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
    public void BuildPartIsNotImplementedYet()
    {
        string path = WriteCsproj("1.2.3");

        string output = Run("build");

        Assert.Equal(Csproj("1.2.3"), File.ReadAllText(path));
        Assert.Contains("not yet implemented", output);
        Assert.Contains("update failed", output);
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
}
