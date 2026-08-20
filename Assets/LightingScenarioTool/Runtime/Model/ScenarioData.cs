using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightingScenarioTool
{
    [Serializable]
    public sealed class ScenarioData
    {
        public ScenarioMetadata metadata = new ScenarioMetadata();
        public List<LightingUnitData> lightingUnits = new List<LightingUnitData>();
        public EditorSettingsData editorSettings = new EditorSettingsData();

        public static ScenarioData CreateDefault()
        {
            return new ScenarioData
            {
                metadata = new ScenarioMetadata
                {
                    scenarioId = Guid.NewGuid().ToString("N"),
                    scenarioName = "New Scenario",
                    dataFormatVersion = "3.0.0",
                    duration = 10f
                },
                lightingUnits = new List<LightingUnitData>(),
                editorSettings = new EditorSettingsData()
            };
        }
    }

    [Serializable]
    public sealed class ScenarioMetadata
    {
        public string scenarioId;
        public string scenarioName;
        public string dataFormatVersion;
        public float duration;
    }

    [Serializable]
    public sealed class LightingUnitData
    {
        public string unitId;
        public string displayName;
        public float previewX = 0.5f;
        public float previewY = 0.5f;
        public TrackData track = new TrackData();
    }

    [Serializable]
    public sealed class TrackData
    {
        public bool locked;
        public bool muted;
        public List<ColorKeyframeData> colorKeyframes = new List<ColorKeyframeData>();
    }

    [Serializable]
    public sealed class ColorKeyframeData
    {
        public string keyframeId;
        // Absolute time from the start of the scenario.
        public float time;
        public SerializableColor color = new SerializableColor(0f, 0f, 0f);
    }

    [Serializable]
    public struct SerializableColor
    {
        public float r;
        public float g;
        public float b;

        public SerializableColor(float r, float g, float b)
        {
            this.r = r;
            this.g = g;
            this.b = b;
        }

        public Color ToUnityColor()
        {
            return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f);
        }

        public static SerializableColor FromUnityColor(Color color)
        {
            return new SerializableColor(color.r, color.g, color.b);
        }
    }

    [Serializable]
    public sealed class EditorSettingsData
    {
        public float currentTime;
        public bool loop;
        public bool snapEnabled = true;
        public float pixelsPerSecond = 100f;

        // Stored in the project JSON so the preview background is restored on load.
        // The selected file path is intentionally stored as-is (normally an absolute path).
        public string previewBackgroundImagePath;
    }

    internal static class LightingScenarioDefaults
    {
        public static readonly SerializableColor FirstKeyframeColor = new SerializableColor(0f, 0f, 0f);
    }
}
