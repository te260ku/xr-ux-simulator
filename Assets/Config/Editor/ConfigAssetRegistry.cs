using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

internal static class ConfigAssetRegistry
{
    public static IReadOnlyList<ConfigAssetBase> FindAll()
    {
        var configTypes =
            TypeCache
                .GetTypesDerivedFrom<ConfigAssetBase>()
                .Where(type =>
                    !type.IsAbstract &&
                    !type.IsGenericType)
                .ToArray();

        var assetGuids = new HashSet<string>();

        foreach (var configType in configTypes)
        {
            var guids = AssetDatabase.FindAssets(
                $"t:{configType.Name}");

            foreach (var guid in guids)
            {
                assetGuids.Add(guid);
            }
        }

        var configs = assetGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(path =>
                AssetDatabase.LoadAssetAtPath<ConfigAssetBase>(path))
            .Where(config => config != null)
            .ToList();

        Validate(configs);

        return configs;
    }

    private static void Validate(
        IReadOnlyCollection<ConfigAssetBase> configs)
    {
        if (configs.Count == 0)
        {
            throw new InvalidOperationException(
                "No ConfigAsset was found.");
        }

        foreach (var config in configs)
        {
            ValidateConfigId(config);
        }

        var duplicateIds = configs
            .GroupBy(
                config => config.ConfigId,
                StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate ConfigId detected: " +
                $"{string.Join(", ", duplicateIds)}");
        }
    }

    private static void ValidateConfigId(
        ConfigAssetBase config)
    {
        if (string.IsNullOrWhiteSpace(config.ConfigId))
        {
            throw new InvalidOperationException(
                $"ConfigId is empty: {config.name}");
        }

        if (config.ConfigId.IndexOfAny(
                Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException(
                $"ConfigId contains invalid characters: " +
                $"{config.ConfigId}");
        }
    }
}