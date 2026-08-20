using System.IO;
using UnityEngine;

namespace LightingScenarioTool
{
    public sealed class JsonScenarioRepository
    {
        public string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) path = "scenario.json";
            return Path.IsPathRooted(path) ? path : Path.Combine(Application.persistentDataPath, path);
        }

        public void Save(string path, ScenarioData data)
        {
            var resolved = ResolvePath(path);
            var directory = Path.GetDirectoryName(resolved);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(resolved, JsonUtility.ToJson(data, true));
        }

        public ScenarioData Load(string path)
        {
            var resolved = ResolvePath(path);
            if (!File.Exists(resolved))
                throw new FileNotFoundException("Scenario file was not found.", resolved);

            var data = JsonUtility.FromJson<ScenarioData>(File.ReadAllText(resolved));
            if (data == null)
                throw new InvalidDataException("Scenario JSON could not be parsed.");

            // Unknown fields from older project versions are ignored by JsonUtility.
            // Current track data intentionally contains Color Keyframes only.
            if (data.metadata != null) data.metadata.dataFormatVersion = "3.0.0";
            return data;
        }
    }
}
