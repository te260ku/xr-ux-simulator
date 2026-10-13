using System;
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RepositoryIdGenerationConfig))]
public sealed class RepositoryIdGenerationConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawRepositoryFile();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("idTypeScript"),
            new GUIContent("ID Type (Script)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetClassScript"),
            new GUIContent("Target Class (Script)"));
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(12);
        if (GUILayout.Button("Generate ID Class", GUILayout.Height(30)))
            Generate();
    }

    private void DrawRepositoryFile()
    {
        var property = serializedObject.FindProperty("repositoryFilePath");
        EditorGUILayout.LabelField("Repository CSV");
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.TextField(property.stringValue);
            if (GUILayout.Button("Browse...", GUILayout.Width(85)))
                Browse(property);
        }
    }

    private static void Browse(SerializedProperty property)
    {
        var current = property.stringValue;
        var directory = File.Exists(current)
            ? Path.GetDirectoryName(current)
            : Application.dataPath;
        var selected = EditorUtility.OpenFilePanel("Select Repository CSV", directory, "csv");
        if (!string.IsNullOrEmpty(selected))
            property.stringValue = selected;
    }

    private void Generate()
    {
        try
        {
            var path = RepositoryIdGenerator.Generate((RepositoryIdGenerationConfig)target);
            Debug.Log($"[RepositoryIdGenerator] Generated: {path}");
            EditorUtility.DisplayDialog("ID Class Generated", path, "OK");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[RepositoryIdGenerator] {exception}");
            EditorUtility.DisplayDialog("Generation Failed", exception.Message, "OK");
        }
    }
}
