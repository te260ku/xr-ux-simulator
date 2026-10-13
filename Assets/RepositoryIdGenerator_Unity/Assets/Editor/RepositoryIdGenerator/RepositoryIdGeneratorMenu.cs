using UnityEditor;
using UnityEngine;

public static class RepositoryIdGeneratorMenu
{
    [MenuItem("Tools/Repository/Generate ID Class", true)]
    private static bool ValidateGenerate()
    {
        return Selection.activeObject is RepositoryIdGenerationConfig;
    }

    [MenuItem("Tools/Repository/Generate ID Class")]
    private static void Generate()
    {
        var config = (RepositoryIdGenerationConfig)Selection.activeObject;
        try
        {
            var path = RepositoryIdGenerator.Generate(config);
            Debug.Log($"[RepositoryIdGenerator] Generated: {path}");
            EditorUtility.DisplayDialog("ID Class Generated", path, "OK");
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[RepositoryIdGenerator] {exception}");
            EditorUtility.DisplayDialog("Generation Failed", exception.Message, "OK");
        }
    }
}
