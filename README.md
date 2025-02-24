# VersionIncrementerTool - A tool to increment the version of a project

## Description
The Version Incrementer Tool is a small utility designed to automate the process of incrementing version numbers in software projects. It provides a simple and efficient way to manage versioning, allowing developers to easily update and track changes in their codebase. With this tool, developers can save time and ensure consistency in versioning across their projects.

## Features

- Increment version numbers in a project
- Increment major, minor, or patch version numbers

## Installation

To install the Version Incrementer Tool, follow these steps:

``` zsh
# install as dotnet tool
dotnet tool install --global VersionIncrementerTool --version 0.3.0

# install as local tool
dotnet new tool-manifest
dotnet tool install --local VersionIncrementerTool --version 0.3.0
```

## Usage

To use the Version Incrementer Tool, run the following command in your project directory with a csproj file and version tag in the format `<version>x.y.z</version>`:

``` zsh
dotnet vit <Major|Minor|Patch>
```

## Development

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
