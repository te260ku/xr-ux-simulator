using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class RepositoryIdGenerator
{
    public static string Generate(RepositoryIdGenerationConfig config)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));
        var idType = RequireClass(config.IdTypeScript, "ID Type");
        var targetType = RequireClass(config.TargetClassScript, "Target Class");
        ValidateTarget(targetType);
        var ids = CsvIdReader.ReadIds(config.RepositoryFilePath);
        CSharpIdValidator.Validate(ids, targetType.Name);
        var output = GetOutputPath(config.TargetClassScript, targetType);
        WriteGeneratedFile(output, CSharpIdClassWriter.Write(idType, targetType, ids));
        AssetDatabase.Refresh();
        return output;
    }

    private static Type RequireClass(MonoScript script, string label)
    {
        if (script == null) throw new InvalidOperationException($"{label}: select a C# script.");
        var type = script.GetClass();
        if (type == null || !type.IsClass)
            throw new InvalidOperationException($"{label}: the selected script must define a class.");
        if (type.ContainsGenericParameters)
            throw new InvalidOperationException($"{label}: open generic classes are not supported.");
        return type;
    }

    private static void ValidateTarget(Type type)
    {
        if (type.IsNested)
            throw new InvalidOperationException("Target Class: nested classes are not supported.");
        // C# reflection does not expose whether a declaration has 'partial'.
        // The target .cs file must declare its class as partial.
    }

    private static string GetOutputPath(MonoScript script, Type targetType)
    {
        var target = AssetDatabase.GetAssetPath(script);
        if (!target.StartsWith("Assets/", StringComparison.Ordinal) ||
            target.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Select a hand-written .cs script inside Assets as Target Class.");
        var directory = Path.GetDirectoryName(target)?.Replace('\\', '/');
        return $"{directory}/{targetType.Name}.g.cs";
    }

    private static void WriteGeneratedFile(string assetPath, string source)
    {
        var root = Directory.GetParent(Application.dataPath).FullName;
        var fullPath = Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
        {
            var old = File.ReadAllText(fullPath);
            if (!old.StartsWith(CSharpIdClassWriter.Marker, StringComparison.Ordinal))
                throw new IOException($"Will not overwrite a non-generated file: {assetPath}");
            if (old == source) return;
        }
        File.WriteAllText(fullPath, source, new UTF8Encoding(false));
    }
}
