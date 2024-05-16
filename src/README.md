# VersionIncrementerTool - A tool to increment the version of a project

## Description
The Version Incrementer Tool is a small utility designed to automate the process of incrementing version numbers in software projects. It provides a simple and efficient way to manage versioning, allowing developers to easily update and track changes in their codebase. With this tool, developers can save time and ensure consistency in versioning across their projects.

## Features

- Increment version numbers in a project
- Increment major, minor, or patch version numbers

## Usage

To use the Version Incrementer Tool, run the following command in your project directory with a csproj file and version tag in the format `<version>x.y.z</version>`:

``` zsh
# get some info
dotnet vit

# increment version
dotnet vit <Major|Minor|Patch>
```

## Disclaimer

This tool is provided as-is, without any warranty or guarantee of any kind. Use at your own risk.
