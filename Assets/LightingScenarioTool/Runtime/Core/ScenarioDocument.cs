using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LightingScenarioTool
{
    /// <summary>
    /// Scenario editing model with undo / redo history.
    /// The timeline contains Color Keyframes only; clip/effect concepts are intentionally absent.
    /// </summary>
    public sealed class ScenarioDocument
    {
        private const float Epsilon = 0.0001f;
        private readonly Stack<string> _undo = new Stack<string>();
        private readonly Stack<string> _redo = new Stack<string>();
        private string _savedPersistentState;

        public ScenarioData Data { get; private set; } = ScenarioData.CreateDefault();
        public string CurrentProjectPath { get; private set; }
        public bool IsDirty { get; private set; }
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public event Action Changed;

        public ScenarioDocument()
        {
            _savedPersistentState = CapturePersistentState();
        }

        public void NewDocument()
        {
            Data = ScenarioData.CreateDefault();
            CurrentProjectPath = null;
            _undo.Clear();
            _redo.Clear();
            _savedPersistentState = CapturePersistentState();
            IsDirty = false;
            NotifyChanged();
        }

        public void LoadDocument(ScenarioData data, string projectPath = null)
        {
            Data = data ?? ScenarioData.CreateDefault();
            Normalize(Data);
            CurrentProjectPath = NormalizeProjectPath(projectPath);
            _undo.Clear();
            _redo.Clear();
            _savedPersistentState = CapturePersistentState();
            IsDirty = false;
            NotifyChanged();
        }

        public void MarkSaved(string projectPath)
        {
            CurrentProjectPath = NormalizeProjectPath(projectPath);
            _savedPersistentState = CapturePersistentState();
            IsDirty = false;
            NotifyChanged();
        }

        public void MarkDirtyWithoutNotification()
        {
            RecalculateDirty();
        }

        private static string NormalizeProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try { return System.IO.Path.GetFullPath(path.Trim()); }
            catch { return path.Trim(); }
        }

        private string CapturePersistentState()
        {
            if (Data == null) return string.Empty;
            if (Data.editorSettings == null) return JsonUtility.ToJson(Data);

            // Moving the playhead is intentionally not treated as a project edit.
            var currentTime = Data.editorSettings.currentTime;
            try
            {
                Data.editorSettings.currentTime = 0f;
                return JsonUtility.ToJson(Data);
            }
            finally
            {
                Data.editorSettings.currentTime = currentTime;
            }
        }

        private void RecalculateDirty()
        {
            IsDirty = !string.Equals(
                CapturePersistentState(),
                _savedPersistentState ?? string.Empty,
                StringComparison.Ordinal);
        }

        public string CaptureState() => JsonUtility.ToJson(Data);

        public void CommitExternalEdit(string beforeState)
        {
            if (string.IsNullOrEmpty(beforeState)) return;
            Normalize(Data);
            var after = JsonUtility.ToJson(Data);
            if (string.Equals(beforeState, after, StringComparison.Ordinal)) return;
            _undo.Push(beforeState);
            _redo.Clear();
            RecalculateDirty();
            NotifyChanged();
        }

        public void CancelExternalEdit(string beforeState)
        {
            if (string.IsNullOrEmpty(beforeState)) return;
            Data = JsonUtility.FromJson<ScenarioData>(beforeState) ?? ScenarioData.CreateDefault();
            Normalize(Data);
            RecalculateDirty();
            NotifyChanged();
        }

        public void Execute(Action<ScenarioData> mutation)
        {
            if (mutation == null) return;
            var before = CaptureState();
            mutation(Data);
            Normalize(Data);
            CommitExternalEdit(before);
        }

        public void Undo()
        {
            if (_undo.Count == 0) return;
            _redo.Push(CaptureState());
            Data = JsonUtility.FromJson<ScenarioData>(_undo.Pop()) ?? ScenarioData.CreateDefault();
            Normalize(Data);
            RecalculateDirty();
            NotifyChanged();
        }

        public void Redo()
        {
            if (_redo.Count == 0) return;
            _undo.Push(CaptureState());
            Data = JsonUtility.FromJson<ScenarioData>(_redo.Pop()) ?? ScenarioData.CreateDefault();
            Normalize(Data);
            RecalculateDirty();
            NotifyChanged();
        }

        public LightingUnitData FindUnit(string unitId) =>
            Data.lightingUnits.FirstOrDefault(x => x.unitId == unitId);

        public ColorKeyframeData FindColorKeyframe(string unitId, string keyframeId)
        {
            var unit = FindUnit(unitId);
            return unit?.track?.colorKeyframes?.FirstOrDefault(x => x.keyframeId == keyframeId);
        }

        public ColorKeyframeData FindColorKeyframe(string keyframeId)
        {
            foreach (var unit in Data.lightingUnits)
            {
                var keyframe = unit?.track?.colorKeyframes?.FirstOrDefault(x => x.keyframeId == keyframeId);
                if (keyframe != null) return keyframe;
            }
            return null;
        }

        public LightingUnitData FindUnitForColorKeyframe(string keyframeId)
        {
            return Data.lightingUnits.FirstOrDefault(x =>
                x?.track?.colorKeyframes != null && x.track.colorKeyframes.Any(k => k.keyframeId == keyframeId));
        }

        public string CreateNextUnitId()
        {
            var index = 1;
            while (true)
            {
                var candidate = $"Light{index:000}";
                if (Data.lightingUnits.All(x => x.unitId != candidate)) return candidate;
                index++;
            }
        }

        public LightingUnitData AddUnit(float previewX, float previewY)
        {
            LightingUnitData created = null;
            Execute(data =>
            {
                var id = CreateNextUnitId();
                created = new LightingUnitData
                {
                    unitId = id,
                    displayName = id,
                    previewX = Mathf.Clamp01(previewX),
                    previewY = Mathf.Clamp01(previewY),
                    track = new TrackData()
                };
                data.lightingUnits.Add(created);
            });
            return created;
        }

        public void DeleteUnit(string unitId)
        {
            Execute(data => data.lightingUnits.RemoveAll(x => x.unitId == unitId));
        }

        public LightingUnitData DuplicateUnit(LightingUnitData source, float previewX, float previewY)
        {
            return DuplicateUnit(source, previewX, previewY, true);
        }

        public LightingUnitData DuplicateUnit(
            LightingUnitData source,
            float previewX,
            float previewY,
            bool includeTrackData)
        {
            if (source == null) return null;

            LightingUnitData created = null;
            Execute(data =>
            {
                var id = CreateNextUnitId();
                created = new LightingUnitData
                {
                    unitId = id,
                    displayName = CreateCopyDisplayName(data, source.displayName),
                    previewX = Mathf.Clamp01(previewX),
                    previewY = Mathf.Clamp01(previewY),
                    track = includeTrackData ? CloneTrack(source.track) : new TrackData
                    {
                        locked = false,
                        muted = false,
                        colorKeyframes = new List<ColorKeyframeData>()
                    }
                };
                data.lightingUnits.Add(created);
            });
            return created;
        }

        private static TrackData CloneTrack(TrackData source)
        {
            var clone = new TrackData();
            if (source == null) return clone;

            clone.locked = source.locked;
            clone.muted = source.muted;
            clone.colorKeyframes = (source.colorKeyframes ?? new List<ColorKeyframeData>())
                .Where(x => x != null)
                .Select(x => new ColorKeyframeData
                {
                    keyframeId = Guid.NewGuid().ToString("N"),
                    time = x.time,
                    color = x.color
                })
                .OrderBy(x => x.time)
                .ToList();
            return clone;
        }

        private static string CreateCopyDisplayName(ScenarioData data, string sourceName)
        {
            var baseName = string.IsNullOrWhiteSpace(sourceName) ? "Lighting Unit" : sourceName.Trim();
            var copyMarkerIndex = baseName.LastIndexOf(" Copy", StringComparison.Ordinal);
            if (copyMarkerIndex >= 0)
            {
                var suffix = baseName.Substring(copyMarkerIndex + 5).Trim();
                if (suffix.Length == 0 || int.TryParse(suffix, out _))
                    baseName = baseName.Substring(0, copyMarkerIndex);
            }

            var candidate = baseName + " Copy";
            if (data.lightingUnits.All(x => x == null || !string.Equals(x.displayName, candidate, StringComparison.Ordinal)))
                return candidate;

            var index = 2;
            while (true)
            {
                candidate = $"{baseName} Copy {index}";
                if (data.lightingUnits.All(x => x == null || !string.Equals(x.displayName, candidate, StringComparison.Ordinal)))
                    return candidate;
                index++;
            }
        }

        public ColorKeyframeData AddColorKeyframe(string unitId, float time, out string error)
        {
            error = null;
            var unit = FindUnit(unitId);
            if (unit == null) { error = "Track not found."; return null; }
            if (unit.track.locked) { error = "Track is locked."; return null; }

            var candidate = Mathf.Clamp(time, 0f, Data.metadata.duration);
            if (HasColorKeyframeAtTime(unit.track, candidate, null))
            {
                error = "A color keyframe already exists at that time.";
                return null;
            }

            var initialColor = unit.track.colorKeyframes.Count == 0
                ? LightingScenarioDefaults.FirstKeyframeColor.ToUnityColor()
                : ScenarioEvaluator.EvaluateBaseColor(unit.track, candidate);

            ColorKeyframeData created = null;
            Execute(data =>
            {
                var target = data.lightingUnits.First(x => x.unitId == unitId).track;
                created = new ColorKeyframeData
                {
                    keyframeId = Guid.NewGuid().ToString("N"),
                    time = candidate,
                    color = SerializableColor.FromUnityColor(initialColor)
                };
                target.colorKeyframes.Add(created);
                SortColorKeyframes(target.colorKeyframes);
            });
            return created;
        }

        public bool DeleteColorKeyframe(string unitId, string keyframeId, out string error)
        {
            return DeleteColorKeyframes(new[] { keyframeId }, out error);
        }

        public bool DeleteColorKeyframes(IEnumerable<string> keyframeIds, out string error)
        {
            error = null;
            var ids = new HashSet<string>(keyframeIds ?? Enumerable.Empty<string>());
            if (ids.Count == 0) return true;

            foreach (var id in ids)
            {
                var unit = FindUnitForColorKeyframe(id);
                if (unit == null) { error = "Color keyframe not found."; return false; }
                if (unit.track.locked) { error = "One or more selected keyframes are on a locked track."; return false; }
            }

            Execute(data =>
            {
                foreach (var unit in data.lightingUnits)
                    unit.track.colorKeyframes.RemoveAll(k => ids.Contains(k.keyframeId));
            });
            return true;
        }

        public bool TrySetColorKeyframeTime(string unitId, string keyframeId, float time, out string error)
        {
            var before = CaptureState();
            var clamped = Mathf.Clamp(time, 0f, Data.metadata.duration);
            var proposed = new Dictionary<string, float> { [keyframeId] = clamped };
            if (!TrySetColorKeyframeTimesNoHistory(proposed, out error)) return false;
            CommitExternalEdit(before);
            return true;
        }

        public bool TrySetColorKeyframeTimeNoHistory(string unitId, string keyframeId, float time, out string error)
        {
            var proposed = new Dictionary<string, float>
            {
                [keyframeId] = Mathf.Clamp(time, 0f, Data.metadata.duration)
            };
            return TrySetColorKeyframeTimesNoHistory(proposed, out error);
        }

        /// <summary>
        /// Applies multiple keyframe times atomically without creating an undo entry.
        /// Used while dragging a multi-selection. If validation fails, no keyframe is changed.
        /// </summary>
        public bool TrySetColorKeyframeTimesNoHistory(IDictionary<string, float> proposedTimes, out string error)
        {
            error = null;
            if (proposedTimes == null || proposedTimes.Count == 0) return false;

            var proposedIds = new HashSet<string>(proposedTimes.Keys);
            var resolved = new Dictionary<string, ColorKeyframeData>();

            foreach (var pair in proposedTimes)
            {
                var unit = FindUnitForColorKeyframe(pair.Key);
                if (unit == null) { error = "Color keyframe not found."; return false; }
                if (unit.track.locked) { error = "One or more selected keyframes are on a locked track."; return false; }
                if (pair.Value < -Epsilon || pair.Value > Data.metadata.duration + Epsilon)
                { error = "Keyframe time is outside the scenario range."; return false; }

                var key = unit.track.colorKeyframes.First(k => k.keyframeId == pair.Key);
                resolved[pair.Key] = key;
            }

            // Validate each track with the proposed times substituted for selected keys.
            foreach (var unit in Data.lightingUnits)
            {
                if (unit?.track?.colorKeyframes == null) continue;
                var times = new List<float>(unit.track.colorKeyframes.Count);
                foreach (var key in unit.track.colorKeyframes)
                {
                    times.Add(proposedTimes.TryGetValue(key.keyframeId, out var value) ? value : key.time);
                }
                times.Sort();
                for (var i = 1; i < times.Count; i++)
                {
                    if (Mathf.Abs(times[i] - times[i - 1]) <= Epsilon)
                    { error = "Different color keyframes cannot occupy the same time on a track."; return false; }
                }
            }

            foreach (var pair in proposedTimes)
                resolved[pair.Key].time = Mathf.Clamp(pair.Value, 0f, Data.metadata.duration);

            foreach (var unit in Data.lightingUnits)
                SortColorKeyframes(unit.track.colorKeyframes);
            return true;
        }

        public bool TrySetColorKeyframeColor(string unitId, string keyframeId, Color color, out string error)
        {
            return TrySetColorKeyframesColor(new[] { keyframeId }, color, out error);
        }

        public bool TrySetColorKeyframesColor(IEnumerable<string> keyframeIds, Color color, out string error)
        {
            error = null;
            var ids = new HashSet<string>(keyframeIds ?? Enumerable.Empty<string>());
            if (ids.Count == 0) { error = "No color keyframes selected."; return false; }

            foreach (var id in ids)
            {
                var unit = FindUnitForColorKeyframe(id);
                if (unit == null) { error = "Color keyframe not found."; return false; }
                if (unit.track.locked) { error = "One or more selected keyframes are on a locked track."; return false; }
            }

            Execute(data =>
            {
                var serialized = SerializableColor.FromUnityColor(color);
                foreach (var unit in data.lightingUnits)
                {
                    foreach (var key in unit.track.colorKeyframes)
                        if (ids.Contains(key.keyframeId)) key.color = serialized;
                }
            });
            return true;
        }

        public float SnapColorKeyframeTime(float rawTime, string unitId, string excludedKeyframeId)
        {
            return SnapColorKeyframeTime(rawTime,
                string.IsNullOrEmpty(excludedKeyframeId) ? null : new[] { excludedKeyframeId });
        }

        public float SnapColorKeyframeTime(float rawTime, IEnumerable<string> excludedKeyframeIds)
        {
            var clamped = Mathf.Clamp(rawTime, 0f, Data.metadata.duration);
            if (!Data.editorSettings.snapEnabled) return clamped;

            var pps = Mathf.Max(20f, Data.editorSettings.pixelsPerSecond);
            var threshold = 8f / pps;
            var grid = GetGridInterval(pps);
            var nearest = Mathf.Round(clamped / grid) * grid;
            var bestDistance = Mathf.Abs(nearest - clamped);
            var excluded = excludedKeyframeIds != null
                ? new HashSet<string>(excludedKeyframeIds)
                : new HashSet<string>();

            Consider(Data.editorSettings.currentTime, clamped, ref nearest, ref bestDistance);
            foreach (var unit in Data.lightingUnits)
            {
                foreach (var keyframe in unit.track.colorKeyframes)
                {
                    if (excluded.Contains(keyframe.keyframeId)) continue;
                    Consider(keyframe.time, clamped, ref nearest, ref bestDistance);
                }
            }

            return bestDistance <= threshold
                ? Mathf.Clamp(nearest, 0f, Data.metadata.duration)
                : clamped;
        }

        public static float GetGridInterval(float pixelsPerSecond)
        {
            if (pixelsPerSecond < 50f) return 1f;
            if (pixelsPerSecond < 100f) return 0.5f;
            if (pixelsPerSecond < 180f) return 0.25f;
            return 0.1f;
        }

        private static void Consider(float candidate, float raw, ref float nearest, ref float bestDistance)
        {
            var distance = Mathf.Abs(candidate - raw);
            if (distance < bestDistance)
            {
                nearest = candidate;
                bestDistance = distance;
            }
        }

        private static bool HasColorKeyframeAtTime(TrackData track, float time, string ignoredKeyframeId)
        {
            return track.colorKeyframes.Any(x =>
                x.keyframeId != ignoredKeyframeId && Mathf.Abs(x.time - time) <= Epsilon);
        }

        private static void SortColorKeyframes(List<ColorKeyframeData> keyframes)
        {
            keyframes.Sort((a, b) => a.time.CompareTo(b.time));
        }

        private void NotifyChanged() => Changed?.Invoke();

        private static void Normalize(ScenarioData data)
        {
            if (data.metadata == null) data.metadata = new ScenarioMetadata();
            if (string.IsNullOrEmpty(data.metadata.scenarioId)) data.metadata.scenarioId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(data.metadata.scenarioName)) data.metadata.scenarioName = "Untitled";
            data.metadata.dataFormatVersion = "3.0.0";
            if (data.metadata.duration <= 0f) data.metadata.duration = 10f;
            if (data.lightingUnits == null) data.lightingUnits = new List<LightingUnitData>();
            if (data.editorSettings == null) data.editorSettings = new EditorSettingsData();
            data.editorSettings.pixelsPerSecond = Mathf.Clamp(
                data.editorSettings.pixelsPerSecond <= 0f ? 100f : data.editorSettings.pixelsPerSecond,
                25f,
                400f);
            data.editorSettings.currentTime = Mathf.Clamp(data.editorSettings.currentTime, 0f, data.metadata.duration);
            data.editorSettings.previewLightSize = Mathf.Clamp(
                data.editorSettings.previewLightSize <= 0f ? 54f : data.editorSettings.previewLightSize,
                20f,
                120f);

            var fallbackIndex = 1;
            foreach (var unit in data.lightingUnits)
            {
                if (unit == null) continue;
                if (string.IsNullOrEmpty(unit.unitId)) unit.unitId = $"Light{fallbackIndex:000}";
                if (string.IsNullOrEmpty(unit.displayName)) unit.displayName = unit.unitId;
                fallbackIndex++;
                unit.previewX = Mathf.Clamp01(unit.previewX);
                unit.previewY = Mathf.Clamp01(unit.previewY);
                if (unit.track == null) unit.track = new TrackData();
                if (unit.track.colorKeyframes == null) unit.track.colorKeyframes = new List<ColorKeyframeData>();

                unit.track.colorKeyframes.RemoveAll(x => x == null);
                foreach (var keyframe in unit.track.colorKeyframes)
                {
                    if (string.IsNullOrEmpty(keyframe.keyframeId)) keyframe.keyframeId = Guid.NewGuid().ToString("N");
                    keyframe.time = Mathf.Clamp(keyframe.time, 0f, data.metadata.duration);
                }
                SortColorKeyframes(unit.track.colorKeyframes);
                for (var i = unit.track.colorKeyframes.Count - 1; i > 0; i--)
                {
                    if (Mathf.Abs(unit.track.colorKeyframes[i].time - unit.track.colorKeyframes[i - 1].time) <= Epsilon)
                        unit.track.colorKeyframes.RemoveAt(i);
                }
            }
        }
    }
}
