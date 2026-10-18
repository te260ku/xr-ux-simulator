using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class BuildDataFolderCopier : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.StandaloneWindows64)
            return;

        var source = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Data"));
        var buildDirectory = Path.GetDirectoryName(report.summary.outputPath);
        var destination = Path.GetFullPath(Path.Combine(buildDirectory, "Data"));

        ValidatePaths(source, destination);
        if (Directory.Exists(destination))
            Directory.Delete(destination, true);

        CopyDirectory(source, destination);
        Debug.Log($"Data folder copied: {source} -> {destination}");
    }

    private static void ValidatePaths(string source, string destination)
    {
        if (!Directory.Exists(source))
            throw new BuildFailedException($"Data folder not found: {source}");

        var sourcePrefix = source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var destinationPrefix = destination.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (sourcePrefix.StartsWith(destinationPrefix, StringComparison.OrdinalIgnoreCase) ||
            destinationPrefix.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
            throw new BuildFailedException("Source and destination Data folders must not overlap.");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));

        foreach (var directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
