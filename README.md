# VersionIncrementerTool - A tool to increment the version of a project

## Description
The Version Incrementer Tool is a small utility designed to automate the process of incrementing version numbers in software projects. It provides a simple and efficient way to manage versioning, allowing developers to easily update and track changes in their codebase. With this tool, developers can save time and ensure consistency in versioning across their projects.

## Features

- Increment version numbers in a project
- Increment major, minor, or patch version numbers
- Show the current version of the csproj file
- Add a missing `<Version>` tag automatically
- Support pre-release and build suffixes (`1.2.3-beta1`, `1.2.3+build5`)
- Optionally commit the change and create a git tag with the new version

## Installation

To install the Version Incrementer Tool, follow these steps:

``` zsh
# install as dotnet tool
dotnet tool install --global VersionIncrementerTool --version 0.6.0

# install as local tool
dotnet new tool-manifest
dotnet tool install --local VersionIncrementerTool --version 0.6.0
```

## Usage

To use the Version Incrementer Tool, run the following command in your project directory with a csproj file (a `<Version>x.y.z</Version>` tag is created if missing):

``` zsh
# show the csproj file, its current version and usage info
dotnet vit

# increment version
dotnet vit <Major|Minor|Patch>

# increment version and commit the csproj (commit message: "Bump version to x.y.z")
dotnet vit <Major|Minor|Patch> --commit

# increment version and create a git tag with the new version (e.g. 1.2.4)
dotnet vit <Major|Minor|Patch> --tag

# increment version, commit and tag
dotnet vit <Major|Minor|Patch> --commit --tag
```

The git options are independent and optional. The tool never pushes anything.

- `--commit` commits only the changed csproj; other changes in your working tree stay untouched.
- `--tag` runs `git tag <version>` on the current commit. Without `--commit` the version change is not committed yet, so the tag does not point to a commit with the new version (the tool prints a note). With `--commit` the commit is made first and the tag points to it.
- If a git step fails (no git repository, tag already exists, ...), the csproj stays updated, an error is shown and no further git step is run.

### Limitations

- Supported version formats are `x.y.z` with an optional pre-release or build suffix (e.g. `1.2.3`, `1.2.3-beta1`, `1.2.3+build5`). The suffix is removed when the version is incremented (`1.2.3-beta1` + `patch` = `1.2.4`).
- Other formats like `1.2` or `1.2.3.4` are rejected with an error message and the file stays unchanged.
- If the csproj has no `<Version>` tag, it is added as first entry of the first `<PropertyGroup>` (without attributes) and incremented starting at `0.0.0` (`patch` = `0.0.1`, `minor` = `0.1.0`, `major` = `1.0.0`).
- Only the first `*.csproj` file in the current directory is used.
- Only a single argument is accepted.

## Development

To run the tests (xUnit, project in `test/VersionIncrementerTool.Tests`, example csproj files in `test/` are used as fixtures):
``` zsh
dotnet test src/VersionIncrementerTool.sln
```

To build the project, run the following command:
``` zsh
dotnet build -c Release
dotnet pack -c Release
dotnet tool install versionincrementertool --add-source "/[devpath]/VersionIncrementerTool/src/nupkg/"
```

Upload to NuGet.org verified
``` zsh
dotnet nuget push VersionIncrementerTool.(Version).nupkg --api-key <your_api_key> --source https://api.nuget.org/v3/index.json
```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE.md) file for details.

## Disclaimer

This tool is provided as-is, without any warranty or guarantee of any kind. Use at your own risk.
