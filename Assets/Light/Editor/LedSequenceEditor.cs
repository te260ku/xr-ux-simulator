using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LedSequence))]
public sealed class LedSequenceEditor : Editor
{
    private SerializedProperty _durationProperty;
    private SerializedProperty _loopProperty;
    private SerializedProperty _clipsProperty;

    private readonly Dictionary<string, LedElement> _ledById = new();


    private void OnEnable()
    {
        _durationProperty =
            serializedObject.FindProperty("_duration");

        _loopProperty =
            serializedObject.FindProperty("_loop");

        _clipsProperty =
            serializedObject.FindProperty("_clips");
    }


    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        RefreshLedCache();

        DrawSequenceSettings();

        EditorGUILayout.Space(12);

        DrawClips();

        serializedObject.ApplyModifiedProperties();
    }


    private void DrawSequenceSettings()
    {
        EditorGUILayout.LabelField(
            "Sequence",
            EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(
            _durationProperty);

        EditorGUILayout.PropertyField(
            _loopProperty);
    }


    private void DrawClips()
    {
        EditorGUILayout.LabelField(
            "Effect Clips",
            EditorStyles.boldLabel);

        EditorGUILayout.Space(4);

        for (int i = 0;
             i < _clipsProperty.arraySize;
             i++)
        {
            SerializedProperty clipProperty =
                _clipsProperty.GetArrayElementAtIndex(i);

            DrawClip(
                clipProperty,
                i);
        }

        EditorGUILayout.Space(4);

        if (GUILayout.Button("+ Add Clip"))
        {
            AddClip();
        }
    }


    private void DrawClip(
        SerializedProperty clipProperty,
        int index)
    {
        SerializedProperty effectTypeProperty =
            clipProperty.FindPropertyRelative(
                "_effectType");

        EditorGUILayout.Space(6);

        SerializedProperty distributionProperty =
            clipProperty.FindPropertyRelative(
                "_distributionType");

        EditorGUILayout.PropertyField(
            distributionProperty,
            new GUIContent("Distribution"));


        LedDistributionType distribution =
            (LedDistributionType)
            distributionProperty.enumValueIndex;


        if (distribution !=
            LedDistributionType.Simultaneous)
        {
            EditorGUILayout.PropertyField(
                clipProperty.FindPropertyRelative(
                    "_distributionAxis"),
                new GUIContent("Axis"));

            EditorGUILayout.PropertyField(
                clipProperty.FindPropertyRelative(
                    "_staggerDuration"),
                new GUIContent(
                    "Stagger Duration"));
        }

        string effectName =
            effectTypeProperty.enumDisplayNames[
                effectTypeProperty.enumValueIndex];

        EditorGUILayout.BeginVertical(
            EditorStyles.helpBox);

        clipProperty.isExpanded =
            EditorGUILayout.Foldout(
                clipProperty.isExpanded,
                $"Clip {index} - {effectName}",
                true);

        if (clipProperty.isExpanded)
        {
            EditorGUI.indentLevel++;

            DrawClipContents(
                clipProperty);

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(4);

        if (GUILayout.Button("Remove Clip"))
        {
            _clipsProperty.DeleteArrayElementAtIndex(
                index);
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);
    }


    private void DrawClipContents(
        SerializedProperty clipProperty)
    {
        EditorGUILayout.PropertyField(
            clipProperty.FindPropertyRelative(
                "_startTime"));

        EditorGUILayout.PropertyField(
            clipProperty.FindPropertyRelative(
                "_duration"));

        EditorGUILayout.PropertyField(
            clipProperty.FindPropertyRelative(
                "_effectType"));

        EditorGUILayout.Space(6);

        DrawTargets(
            clipProperty.FindPropertyRelative(
                "_targetLedIds"));

        EditorGUILayout.Space(6);

        EditorGUILayout.PropertyField(
            clipProperty.FindPropertyRelative(
                "_color"));

        EditorGUILayout.PropertyField(
            clipProperty.FindPropertyRelative(
                "_intensity"));
    }


    private void DrawTargets(
        SerializedProperty targetIdsProperty)
    {
        EditorGUILayout.LabelField(
            "Targets",
            EditorStyles.boldLabel);

        EditorGUI.indentLevel++;

        for (int i = 0;
             i < targetIdsProperty.arraySize;
             i++)
        {
            SerializedProperty idProperty =
                targetIdsProperty.GetArrayElementAtIndex(i);

            LedElement currentLed =
                FindLed(idProperty.stringValue);

            bool isMissing =
                !string.IsNullOrEmpty(
                    idProperty.stringValue)
                &&
                currentLed == null;

            EditorGUILayout.BeginHorizontal();

            EditorGUI.BeginChangeCheck();

            LedElement selectedLed =
                (LedElement)EditorGUILayout.ObjectField(
                    $"LED {i}",
                    currentLed,
                    typeof(LedElement),
                    true);

            if (EditorGUI.EndChangeCheck())
            {
                SetTarget(
                    idProperty,
                    selectedLed);
            }

            if (GUILayout.Button(
                    "-",
                    GUILayout.Width(24)))
            {
                targetIdsProperty
                    .DeleteArrayElementAtIndex(i);

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;

                return;
            }

            EditorGUILayout.EndHorizontal();

            if (isMissing)
            {
                EditorGUILayout.HelpBox(
                    $"LEDがSceneに見つかりません。\nID: {idProperty.stringValue}",
                    MessageType.Warning);
            }
        }

        if (GUILayout.Button("+ Add LED"))
        {
            int index =
                targetIdsProperty.arraySize;

            targetIdsProperty.arraySize++;

            targetIdsProperty
                .GetArrayElementAtIndex(index)
                .stringValue = string.Empty;
        }

        EditorGUI.indentLevel--;
    }


    private static void SetTarget(
        SerializedProperty idProperty,
        LedElement led)
    {
        if (led == null)
        {
            idProperty.stringValue =
                string.Empty;

            return;
        }

        // Prefab Assetではなく
        // Scene上のLedElementだけ許可する
        if (!led.gameObject.scene.IsValid())
        {
            Debug.LogWarning(
                "Scene上のLedElementを選択してください.");

            return;
        }

        idProperty.stringValue =
            led.Id;
    }


    private void RefreshLedCache()
    {
        _ledById.Clear();

        LedElement[] leds =
            Object.FindObjectsByType<LedElement>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (LedElement led in leds)
        {
            if (string.IsNullOrEmpty(led.Id))
            {
                continue;
            }

            _ledById[led.Id] = led;
        }
    }


    private LedElement FindLed(
        string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        _ledById.TryGetValue(
            id,
            out LedElement led);

        return led;
    }


    private void AddClip()
    {
        int index =
            _clipsProperty.arraySize;

        _clipsProperty.arraySize++;

        SerializedProperty clipProperty =
            _clipsProperty.GetArrayElementAtIndex(index);

        clipProperty
            .FindPropertyRelative("_startTime")
            .floatValue = 0f;

        clipProperty
            .FindPropertyRelative("_duration")
            .floatValue = 1f;

        clipProperty
            .FindPropertyRelative("_effectType")
            .enumValueIndex = 0;

        clipProperty
            .FindPropertyRelative("_targetLedIds")
            .arraySize = 0;

        clipProperty
            .FindPropertyRelative("_color")
            .colorValue = Color.white;

        clipProperty
            .FindPropertyRelative("_intensity")
            .floatValue = 1f;

        clipProperty
            .FindPropertyRelative(
                "_distributionType")
            .enumValueIndex =
                (int)LedDistributionType.Simultaneous;

        clipProperty
            .FindPropertyRelative(
                "_distributionAxis")
            .enumValueIndex =
                (int)LedDistributionAxis.X;

        clipProperty
            .FindPropertyRelative(
                "_staggerDuration")
            .floatValue = 0f;

        clipProperty.isExpanded = true;
    }
}