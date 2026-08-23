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
            if (string.IsNullOrEmpty(Path.GetExtension(resolved)))
                resolved += ".json";
            return resolved;
        }

        public void Save(string path, ScenarioData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var resolved = ResolvePath(path);
            var directory = Path.GetDirectoryName(resolved);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("The project save directory could not be determined.");

            Directory.CreateDirectory(directory);
            var json = JsonUtility.ToJson(data, true);
            WriteAtomically(resolved, json);
        }

        public ScenarioData Load(string path)
        {
            var resolved = ResolvePath(path);
            if (!File.Exists(resolved))
                throw new FileNotFoundException("Scenario file was not found.", resolved);

            var json = File.ReadAllText(resolved);
            var data = JsonUtility.FromJson<ScenarioData>(json);
            if (data == null)
                throw new InvalidDataException("Scenario JSON could not be parsed.");

            EnsureSupportedFormatVersion(data.metadata?.dataFormatVersion);

            // Unknown fields from older project versions are ignored by JsonUtility.
            // Normalization also repairs null collections and duplicate/missing IDs.
            return ScenarioDataUtility.Normalize(data);
        }

        private static void EnsureSupportedFormatVersion(string versionText)
        {
            if (string.IsNullOrWhiteSpace(versionText)) return;

            if (!Version.TryParse(versionText.Trim(), out var projectVersion) ||
                !Version.TryParse(ScenarioDataUtility.CurrentFormatVersion, out var currentVersion))
            {
                throw new InvalidDataException(
                    "Scenario data format version is invalid: " + versionText);
            }

            if (projectVersion.CompareTo(currentVersion) > 0)
            {
                throw new InvalidDataException(
                    $"Scenario data format {projectVersion} is newer than the supported " +
                    $"format {currentVersion}. Open it with a newer version of the tool.");
            }
        }

        private static void WriteAtomically(string destinationPath, string contents)
        {
            var directory = Path.GetDirectoryName(destinationPath);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("The project save directory could not be determined.");

            var tempPath = Path.Combine(
                directory,
                "." + Path.GetFileName(destinationPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                File.WriteAllText(tempPath, contents);
                if (File.Exists(destinationPath))
                    File.Replace(tempPath, destinationPath, null);
                else
                    File.Move(tempPath, destinationPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
