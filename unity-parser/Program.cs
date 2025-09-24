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

    class SceneBlock{
        public long FileId;
        public int ClassId;
        public string RawText;
    }

    static IEnumerable<SceneBlock> SplitUnityYamlBlocks(string sceneText) {
        var pattern = new Regex(@"(?ms)^--- !u!(\d+) &(-?\d+)\s*(.*?)(?=^--- !u!|\z)", RegexOptions.Multiline);

        foreach (Match m in pattern.Matches(sceneText)) {
            var classId = int.Parse(m.Groups[1].Value);
            var fileId = long.Parse(m.Groups[2].Value);
            var body = m.Groups[3].Value;

            yield return new SceneBlock { ClassId = classId, FileId = fileId, RawText = body };
        }
    }

    static string ExtractSingle(string blockText, string key) {
        var m = Regex.Match(blockText, $@"(?:\n|^)\s*{Regex.Escape(key)}:\s*(.+?)\r?$", RegexOptions.Multiline);
        if (!m.Success) return null;
        var val = m.Groups[1].Value.Trim();
        if (val.StartsWith("\"") && val.EndsWith("\"")) val = val.Substring(1, val.Length - 2); // remove quotes
        return val;
    }

    static string ExtractScriptGuidFromBlock(string blockText) {
        var m = Regex.Match(blockText, @"m_Script:\s*\{[^}]*guid:\s*([0-9a-fA-F]+)");
        if (m.Success) return m.Groups[1].Value;
        return null;
    }

    static long? ExtractGameObjectFileId(string blockText) {
        var m = Regex.Match(blockText, @"m_GameObject:\s*\{\s*fileID:\s*(-?\d+)\s*\}");
        if (m.Success) return long.Parse(m.Groups[1].Value);
        return null;
    }

    static long? ExtractTransformFather(string blockText) {
        var m = Regex.Match(blockText, @"m_Father:\s*\{\s*fileID:\s*(-?\d+)\s*\}");
        if (m.Success) {
            return long.Parse(m.Groups[1].Value);
        }

        return null;
    }

    class GameObjectInfo{
        public long FileId;
        public string Name;
        public List<long> ComponentFileIds = new List<long>();
    }

    class TransformInfo{
        public long TransformId;
        public long FatherId;
        public List<long> ChildrenIds;
        public long GameObjectId;
    }

    class MonoBehaviourInfo{
        public string ScriptGuid;
        public long GameObjectId;
    }

    static void DumpSceneHierarchy(
        string outputPath,
        string outFileName,
        Dictionary<long, GameObjectInfo> gameObjects,
        Dictionary<long, TransformInfo> transforms) {
        var filePath = Path.Combine(outputPath, outFileName);
        using var writer = new StreamWriter(filePath);

        // Map TransformId → GameObject
        var transformIdToGO = new Dictionary<long, GameObjectInfo>();
        foreach (var t in transforms.Values) {
            if (gameObjects.TryGetValue(t.GameObjectId, out var go))
                transformIdToGO[t.TransformId] = go;
        }

        // Find root Transforms (no parent)
        var rootTransforms = new List<TransformInfo>();
        foreach (var t in transforms.Values) {
            if (t.FatherId == 0)
                rootTransforms.Add(t);
        }

        void PrintTransform(TransformInfo t, string indent) {
            if (!transformIdToGO.TryGetValue(t.TransformId, out var go)) return;

            // Print GameObject name only
            writer.WriteLine($"{indent}{go.Name}");

            // Print children recursively
            foreach (var childId in t.ChildrenIds) {
                if (transforms.TryGetValue(childId, out var childTransform)) {
                    PrintTransform(childTransform, indent + "--");
                }
            }
        }

        // Print all root transforms
        foreach (var root in rootTransforms) {
            PrintTransform(root, "");
        }
    }

    static void DumpUnusedScripts(
        string outputFolder,
        string outFileName,
        string projectPath,
        Dictionary<string, string> guidToCs,
        List<MonoBehaviourInfo> monoBehaviours) {
        // Collect all GUIDs that appear in MonoBehaviours
        var usedGuids = new HashSet<string>(
                monoBehaviours
                    .Where(mb => !string.IsNullOrEmpty(mb.ScriptGuid))
                    .Select(mb => mb.ScriptGuid),
                StringComparer.OrdinalIgnoreCase
            );

        // Prepare CSV lines (with header)
        var lines = new List<string> { "Relevant Path, GUID" };

        // Find scripts not referenced in any scene
        foreach (var kvp in guidToCs) {
            if (!usedGuids.Contains(kvp.Key)) {
                // Make path relative to project folder
                var relativePath = kvp.Value.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase)
                    ? kvp.Value.Substring(projectPath.Length)
                    : kvp.Value;

                // Escape if contains comma
                if (relativePath.Contains(","))
                    relativePath = $"\"{relativePath}\"";

                lines.Add($"{relativePath},{kvp.Key}");
            }
        }

        // Save to CSV file
        var unusedFilePath = Path.Combine(outputFolder, outFileName);
        File.WriteAllLines(unusedFilePath, lines);
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

        var guidToCs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var csPath in csFiles) {
            var guid = GetCsFileGuid(csPath);
            if (guid != null) {
                guidToCs[guid] = csPath;
            }
        }

        var monoBehaviours = new List<MonoBehaviourInfo>();

        foreach (var scenePath in sceneFiles) {
            var outFileName = Path.GetFileName(scenePath) + ".dump";
            
            var text = File.ReadAllText(scenePath);
            var blocks = SplitUnityYamlBlocks(text);

            var gameObjects = new Dictionary<long, GameObjectInfo>();
            var transforms = new Dictionary<long, TransformInfo>();

            foreach (var block in blocks) {
                if (block.ClassId == 1) // GameObject
                {
                    var name = ExtractSingle(block.RawText, "m_Name") ?? "<no-name>";
                    var go = new GameObjectInfo { FileId = block.FileId, Name = name };

                    // Collect all component FileIDs from m_Component array
                    var compMatches = Regex.Matches(block.RawText, @"- component:\s*\{\s*fileID:\s*(-?\d+)\s*\}");
                    foreach (Match match in compMatches)
                        go.ComponentFileIds.Add(long.Parse(match.Groups[1].Value));

                    gameObjects[block.FileId] = go;
                }
                else if (block.ClassId == 4) // Transform
                {
                    var transformId = block.FileId;
                    var fatherId = ExtractTransformFather(block.RawText) ?? 0;
                    var goId = ExtractGameObjectFileId(block.RawText) ?? 0;

                    var childrenIds = new List<long>();

                    // Parse m_Children array: each child has fileID
                    var childMatches = Regex.Matches(block.RawText, @"- fileID:\s*(-?\d+)");
                    foreach (Match match in childMatches) {
                        childrenIds.Add(long.Parse(match.Groups[1].Value));
                    }

                    transforms[transformId] = new TransformInfo {
                        TransformId = transformId,
                        FatherId = fatherId,
                        GameObjectId = goId,
                        ChildrenIds = childrenIds
                    };
                }
                else if (block.ClassId == 114) // MonoBehaviour
                {
                    var guid = ExtractScriptGuidFromBlock(block.RawText);
                    var goId = ExtractGameObjectFileId(block.RawText) ?? 0;

                    monoBehaviours.Add(new MonoBehaviourInfo {
                        ScriptGuid = guid,
                        GameObjectId = goId
                    });
                }
            }

            // Build Transform children relationships
            foreach (var t in transforms.Values) {
                if (t.FatherId != 0 && transforms.TryGetValue(t.FatherId, out var father))
                    father.ChildrenIds.Add(t.TransformId);
            }

            DumpSceneHierarchy(outputPath, outFileName, gameObjects, transforms);
        }

        DumpUnusedScripts(outputPath, "UnusedScripts.csv", projectPath, guidToCs, monoBehaviours);

        return 0;
    }
}