// See https://aka.ms/new-console-template for more information

using System.Text.RegularExpressions;
using System.Collections.Generic;

class Program{
    static string GetCsFileGuid(string csFilePath) {
        var metaPath = csFilePath + ".meta";

        if (!File.Exists(metaPath))
            return null;

        var text = File.ReadAllText(metaPath);
        var match = Regex.Match(text, @"guid:\s*([0-9a-fA-F]+)");

        if (match.Success) {
            return match.Groups[1].Value;
        }

        return null;
    }

    static int Main(string[] args) {
        if (args.Length != 2) {
            Console.WriteLine("Usage: tool.exe <unity_project_path> <output_folder_path>");
            return 1;
        }

        var projectPath = args[0];
        var outputPath = args[1];

        if (!Directory.Exists(projectPath)) {
            Console.WriteLine($"Project path does not exist: {projectPath}");
            return 2;
        }

        Directory.CreateDirectory(outputPath);

        var csFiles = Directory.EnumerateFiles(projectPath, "*.cs", SearchOption.AllDirectories);
        var sceneFiles = Directory.EnumerateFiles(projectPath, "*.unity", SearchOption.AllDirectories);

        Console.WriteLine($"Found {csFiles.Count()} .cs files");
        Console.WriteLine($"Found {sceneFiles.Count()} .unity scene files");

        var guidToCs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var csPath in csFiles) {
            var guid = GetCsFileGuid(csPath);
            if (guid != null) {
                guidToCs[guid] = csPath;
            }
        }

        foreach (var key in guidToCs.Keys) {
            Console.WriteLine($"Guid {key} related to file: {guidToCs[key]}");
        }

        return 0;
    }
}