using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

internal static class RuntimeConfigImporter
{
    [MenuItem(
        "Tools/Config/Import Runtime Config Folder...")]
    private static void ImportFromMenu()
    {
        var directory =
            EditorUtility.OpenFolderPanel(
                "Select Runtime Config Folder",
                string.Empty,
                string.Empty);

        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        try
        {
            ImportAll(directory);

            EditorUtility.DisplayDialog(
                "Config Import",
                "Runtime config import completed.",
                "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);

            EditorUtility.DisplayDialog(
                "Config Import Failed",
                exception.Message,
                "OK");
        }
    }

    private static void ImportAll(
        string directory)
    {
        var configs =
            ConfigAssetRegistry.FindAll();

        var jsonMap =
            LoadAndValidateAllJson(
                directory,
                configs);

        Undo.RecordObjects(
            configs
                .Cast<UnityEngine.Object>()
                .ToArray(),
            "Import Runtime Config");

        foreach (var config in configs)
        {
            config.ApplyJson(
                jsonMap[config.ConfigId]);

            EditorUtility.SetDirty(config);
        }

        AssetDatabase.SaveAssets();
    }

    private static Dictionary<string, string>
        LoadAndValidateAllJson(
            string directory,
            IReadOnlyList<ConfigAssetBase> configs)
    {
        var result =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var config in configs)
        {
            var path = Path.Combine(
                directory,
                $"{config.ConfigId}.json");

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Config file was not found: {path}");
            }

            var json = File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException(
                    $"Config file is empty: {path}");
            }

            // 先にDeserializeできることだけ検証する。
            // SOを途中まで更新して失敗するのを防ぐ。
            var testData = JsonUtility.FromJson(
                json,
                config.DataType);

            if (testData == null)
            {
                throw new InvalidOperationException(
                    $"Failed to deserialize config: {path}");
            }

            result.Add(
                config.ConfigId,
                json);
        }

        return result;
    }
}