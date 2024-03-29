﻿// See https://aka.ms/new-console-template for more information
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;


internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Hello there, this is VersionIncrementerTool!");

        if (args.Length != 1)
        {
            Console.WriteLine("Usage: VersionIncrementer.exe <Major|Minor|Patch>");
            return;
        }

        string versionPart = args[0];
        if (!versionPart.Equals("Major", StringComparison.OrdinalIgnoreCase) &&
            !versionPart.Equals("Minor", StringComparison.OrdinalIgnoreCase) &&
            !versionPart.Equals("Patch", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Invalid version part. Please provide 'Major', 'Minor', or 'Patch'.");
            return;
        }        

        string executionPath = Environment.CurrentDirectory;
        Console.WriteLine($"Local BaseDirectory path: {executionPath}");
        
        string csprojFilePath = GetCsprojFilePath(executionPath);

#if DEBUG
        //use only for debugging
        Console.WriteLine($"DEBUG: your are in debug mode!");
        string csprojlocalTestPath = executionPath + "test/";

        csprojFilePath = csprojlocalTestPath;
        Console.WriteLine($"Local test path: {csprojFilePath}");
#endif

        if (csprojFilePath == string.Empty)
        {
            Console.WriteLine("No .csproj file found in the directory.");
            return;
        } 

        //search *.csproj file in the directory
        string[] files = Directory.GetFiles(executionPath, "*.csproj", SearchOption.AllDirectories);
        if (files.Length > 0)
        {
            csprojFilePath = files[0];
            Console.WriteLine($"Found .csproj file: {csprojFilePath}");
        }
        else
        {
            Console.WriteLine("No .csproj file found in the directory.");
            return;
        }


        try
        {
            string csprojContent = File.ReadAllText(csprojFilePath);
            string pattern = "<Version>(.*?)</Version>";
            Match match = Regex.Match(csprojContent, pattern);
            if (match.Success)
            {
                string version = match.Groups[1].Value;
                string[] versionParts = version.Split('.');
                int major = int.Parse(versionParts[0]);
                int minor = int.Parse(versionParts[1]);
                int patch = int.Parse(versionParts[2]);

                switch (versionPart.ToLower())
                {
                    case "major":
                        major++;
                        minor = 0;
                        patch = 0;
                        break;
                    case "minor":
                        minor++;
                        patch = 0;
                        break;
                    case "patch":
                        patch++;
                        break;
                }

                string newVersion = $"{major}.{minor}.{patch}";
                string newCsprojContent = csprojContent.Replace($"<Version>{version}</Version>", $"<Version>{newVersion}</Version>");
                File.WriteAllText(csprojFilePath, newCsprojContent);

                Console.WriteLine($"Version updated to {newVersion}");
            }
            else
            {
                Console.WriteLine("No <Version> tag found in the .csproj file.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    private static string GetCsprojFilePath(string executionPath)
    {
        string[] files = Directory.GetFiles(executionPath, "*.csproj", SearchOption.TopDirectoryOnly);
        if (files.Length > 0)
        {
            return files[0];
        }
        return string.Empty;
    }
}