using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LightingScenarioTool
{
    /// <summary>
    /// Centralizes scenario-format defaults and defensive normalization.
    /// All data entering ScenarioDocument should pass through this utility so
    /// runtime code can rely on non-null collections and globally unique IDs.
    /// </summary>
    internal static class ScenarioDataUtility
    {
        public const string CurrentFormatVersion = "3.0.0";
        public const float DefaultDuration = 10f;
        public const float DefaultPixelsPerSecond = 100f;
        public const float MinPixelsPerSecond = 25f;
        public const float MaxPixelsPerSecond = 400f;
        public const float DefaultPreviewLightSize = 40f;
        public const float MinPreviewLightSize = 20f;
        public const float MaxPreviewLightSize = 40f;
        public const float TimeEpsilon = 0.0001f;

        public static ScenarioData Normalize(ScenarioData data)
        {
            data ??= ScenarioData.CreateDefault();

            data.metadata ??= new ScenarioMetadata();
            if (string.IsNullOrWhiteSpace(data.metadata.scenarioId))
                data.metadata.scenarioId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(data.metadata.scenarioName))
                data.metadata.scenarioName = "Untitled";
            data.metadata.dataFormatVersion = CurrentFormatVersion;
            if (!IsFinite(data.metadata.duration) || data.metadata.duration <= 0f)
                data.metadata.duration = DefaultDuration;

            data.lightingUnits ??= new List<LightingUnitData>();
            data.lightingUnits.RemoveAll(unit => unit == null);

            data.editorSettings ??= new EditorSettingsData();
            var pixelsPerSecond = IsFinite(data.editorSettings.pixelsPerSecond) &&
                                  data.editorSettings.pixelsPerSecond > 0f
                ? data.editorSettings.pixelsPerSecond
                : DefaultPixelsPerSecond;
            data.editorSettings.pixelsPerSecond = Mathf.Clamp(
                pixelsPerSecond,
                MinPixelsPerSecond,
                MaxPixelsPerSecond);

            var currentTime = IsFinite(data.editorSettings.currentTime)
                ? data.editorSettings.currentTime
                : 0f;
            data.editorSettings.currentTime = Mathf.Clamp(
                currentTime,
                0f,
                data.metadata.duration);

            var previewLightSize = IsFinite(data.editorSettings.previewLightSize) &&
                                   data.editorSettings.previewLightSize > 0f
                ? data.editorSettings.previewLightSize
                : DefaultPreviewLightSize;
            data.editorSettings.previewLightSize = Mathf.Clamp(
                previewLightSize,
                MinPreviewLightSize,
                MaxPreviewLightSize);

            NormalizeUnits(data);
            return data;
        }

        private static void NormalizeUnits(ScenarioData data)
        {
            var usedUnitIds = new HashSet<string>(StringComparer.Ordinal);
            var usedKeyframeIds = new HashSet<string>(StringComparer.Ordinal);
            var nextFallbackUnitIndex = 1;

            foreach (var unit in data.lightingUnits)
            {
                var requestedId = string.IsNullOrWhiteSpace(unit.unitId) ? null : unit.unitId.Trim();
                unit.unitId = EnsureUniqueUnitId(requestedId, usedUnitIds, ref nextFallbackUnitIndex);
                if (string.IsNullOrWhiteSpace(unit.displayName))
                    unit.displayName = unit.unitId;

                unit.previewX = Mathf.Clamp01(IsFinite(unit.previewX) ? unit.previewX : 0.5f);
                unit.previewY = Mathf.Clamp01(IsFinite(unit.previewY) ? unit.previewY : 0.5f);
                unit.track ??= new TrackData();
                unit.track.colorKeyframes ??= new List<ColorKeyframeData>();
                unit.track.colorKeyframes.RemoveAll(keyframe => keyframe == null);

                foreach (var keyframe in unit.track.colorKeyframes)
                {
                    var keyframeId = string.IsNullOrWhiteSpace(keyframe.keyframeId)
                        ? null
                        : keyframe.keyframeId.Trim();
                    if (string.IsNullOrEmpty(keyframeId) || !usedKeyframeIds.Add(keyframeId))
                    {
                        do
                        {
                            keyframeId = Guid.NewGuid().ToString("N");
                        }
                        while (!usedKeyframeIds.Add(keyframeId));
                    }
                    keyframe.keyframeId = keyframeId;
                    keyframe.time = Mathf.Clamp(
                        IsFinite(keyframe.time) ? keyframe.time : 0f,
                        0f,
                        data.metadata.duration);
                    keyframe.color = new SerializableColor(
                        ClampColorChannel(keyframe.color.r),
                        ClampColorChannel(keyframe.color.g),
                        ClampColorChannel(keyframe.color.b));
                }

                SortAndRemoveTimeCollisions(unit.track.colorKeyframes);
            }
        }

        private static string EnsureUniqueUnitId(
            string requestedId,
            ISet<string> usedIds,
            ref int nextFallbackUnitIndex)
        {
            if (!string.IsNullOrEmpty(requestedId) && usedIds.Add(requestedId))
                return requestedId;

            while (true)
            {
                var candidate = $"Light{nextFallbackUnitIndex:000}";
                nextFallbackUnitIndex++;
                if (usedIds.Add(candidate))
                    return candidate;
            }
        }

        public static void SortAndRemoveTimeCollisions(List<ColorKeyframeData> keyframes)
        {
            if (keyframes == null) return;

            // LINQ OrderBy is stable, so for malformed data with colliding times the first
            // keyframe in the source file is kept deterministically.
            var ordered = keyframes.OrderBy(keyframe => keyframe.time).ToList();
            keyframes.Clear();
            keyframes.AddRange(ordered);

            for (var i = keyframes.Count - 1; i > 0; i--)
            {
                if (Mathf.Abs(keyframes[i].time - keyframes[i - 1].time) <= TimeEpsilon)
                    keyframes.RemoveAt(i);
            }
        }

        private static float ClampColorChannel(float value)
        {
            return Mathf.Clamp01(IsFinite(value) ? value : 0f);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
