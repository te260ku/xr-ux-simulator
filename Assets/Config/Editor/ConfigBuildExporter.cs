using System;
using System.IO;
using System.Text;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class ConfigBuildExporter
    : IPostprocessBuildWithReport
{
    private const string ConfigDirectoryName = "Config";

    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        try
        {
            Export(report);
        }
        catch (Exception exception)
        {
            throw new BuildFailedException(
                $"Failed to export config JSON files.\n" +
                $"{exception}");
        }
    }

    private static void Export(BuildReport report)
    {
        var buildOutputPath =
            report.summary.outputPath;

        var buildDirectory =
            Path.GetDirectoryName(buildOutputPath);

        if (string.IsNullOrWhiteSpace(buildDirectory))
        {
            throw new InvalidOperationException(
                $"Failed to resolve build directory: " +
                $"{buildOutputPath}");
        }

        var configDirectory = Path.Combine(
            buildDirectory,
            ConfigDirectoryName);

        Directory.CreateDirectory(configDirectory);

        DeleteOldJsonFiles(configDirectory);

        var configs = ConfigAssetRegistry.FindAll();

        foreach (var config in configs)
        {
            var path = Path.Combine(
                configDirectory,
                $"{config.ConfigId}.json");

            File.WriteAllText(
                path,
                config.ToJson(),
                new UTF8Encoding(false));

            Debug.Log(
                $"Exported config: {path}");
        }
    }

    private static void DeleteOldJsonFiles(
        string configDirectory)
    {
        foreach (var path in Directory.EnumerateFiles(
                     configDirectory,
                     "*.json",
                     SearchOption.TopDirectoryOnly))
        {
            File.Delete(path);
        }
    }
}