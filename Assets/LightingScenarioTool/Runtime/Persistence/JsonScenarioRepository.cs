using System;
using System.IO;
using UnityEngine;

namespace LightingScenarioTool
{
    public sealed class JsonScenarioRepository
    {
        /// <summary>
        /// Normalizes an explicitly supplied project path. Project files are never redirected
        /// to Application.persistentDataPath / AppData.
        /// </summary>
        public string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A project file path has not been selected.", nameof(path));

            var resolved = Path.GetFullPath(path.Trim());
            if (string.IsNullOrEmpty(Path.GetExtension(resolved))) resolved += ".json";
            return resolved;
        }

        public void Save(string path, ScenarioData data)
        {
            var resolved = ResolvePath(path);
            var directory = Path.GetDirectoryName(resolved);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("The project save directory could not be determined.");

            Directory.CreateDirectory(directory);
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
