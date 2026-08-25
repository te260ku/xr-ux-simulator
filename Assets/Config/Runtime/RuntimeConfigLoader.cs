// Playerでは、
// var config =
//     RuntimeConfigLoader.Load<LightingConfigData>("lighting");
// と読み込みます。
using System;
using System.IO;
using UnityEngine;

public static class RuntimeConfigLoader
{
    private const string ConfigDirectoryName = "Config";

    public static TData Load<TData>(string configId)
        where TData : class
    {
        var path = GetConfigPath(configId);

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

        var data = JsonUtility.FromJson<TData>(json);

        if (data == null)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize config: {path}");
        }

        return data;
    }

    public static string GetConfigPath(string configId)
    {
        if (string.IsNullOrWhiteSpace(configId))
        {
            throw new ArgumentException(
                "ConfigId must not be empty.",
                nameof(configId));
        }

        var applicationDirectory = GetApplicationDirectory();

        return Path.Combine(
            applicationDirectory,
            ConfigDirectoryName,
            $"{configId}.json");
    }

    private static string GetApplicationDirectory()
    {
#if UNITY_EDITOR
        return Directory.GetParent(Application.dataPath)?.FullName
               ?? throw new InvalidOperationException(
                   "Failed to get project directory.");
#else
        return Directory.GetParent(Application.dataPath)?.FullName
               ?? throw new InvalidOperationException(
                   "Failed to get application directory.");
#endif
    }
}