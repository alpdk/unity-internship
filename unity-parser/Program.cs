// See https://aka.ms/new-console-template for more information

class Program{
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

        return 0;
    }
}