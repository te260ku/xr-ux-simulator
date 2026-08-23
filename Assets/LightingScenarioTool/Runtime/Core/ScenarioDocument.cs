using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LightingScenarioTool
{
    /// <summary>
    /// Owns editable scenario data, undo/redo history, validation and mutations.
    /// Transient playhead movement is intentionally excluded from dirty-state tracking.
    /// </summary>
    public sealed class ScenarioDocument
    {
        private const int MaxHistoryEntries = 100;
        private readonly Stack<string> _undo = new Stack<string>();
        private readonly Stack<string> _redo = new Stack<string>();
        private string _savedPersistentState;

        public ScenarioData Data { get; private set; }
        public string CurrentProjectPath { get; private set; }
        public bool IsDirty { get; private set; }
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        public event Action Changed;

        public ScenarioDocument()
        {
            Data = ScenarioDataUtility.Normalize(ScenarioData.CreateDefault());
            _savedPersistentState = CapturePersistentState();
        }

        public void NewDocument()
        {
            Data = ScenarioDataUtility.Normalize(ScenarioData.CreateDefault());
            CurrentProjectPath = null;
            ClearHistory();
            _savedPersistentState = CapturePersistentState();
            IsDirty = false;
            NotifyChanged();
        }

        public void LoadDocument(ScenarioData data, string projectPath = null)
        {
            Data = ScenarioDataUtility.Normalize(data ?? ScenarioData.CreateDefault());
            CurrentProjectPath = NormalizeProjectPath(projectPath);
            ClearHistory();
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

        /// <summary>
        /// Recalculates dirty state after an intentionally history-free persistent edit.
        /// Prefer Execute/CommitExternalEdit for normal editing operations.
        /// </summary>
        public void MarkDirtyWithoutNotification()
        {
            RecalculateDirty();
        }

        public string CaptureState()
        {
            return JsonUtility.ToJson(Data);
        }

        public void CommitExternalEdit(string beforeState)
        {
            if (string.IsNullOrEmpty(beforeState)) return;

            ScenarioDataUtility.Normalize(Data);
            var afterState = CaptureState();
            if (string.Equals(beforeState, afterState, StringComparison.Ordinal))
            {
                RecalculateDirty();
                return;
            }

            PushBounded(_undo, beforeState);
            _redo.Clear();
            RecalculateDirty();
            NotifyChanged();
        }


        /// <summary>
        /// Executes one atomic document mutation. If the mutation throws, the previous
        /// serialized state is restored before the exception is propagated.
        /// </summary>
        public void Execute(Action<ScenarioData> mutation)
        {
            if (mutation == null) return;

            var beforeState = CaptureState();
            try
            {
                mutation(Data);
                ScenarioDataUtility.Normalize(Data);
            }
            catch
            {
                RestoreState(beforeState);
                throw;
            }

            CommitExternalEdit(beforeState);
        }

        public void Undo()
        {
            if (_undo.Count == 0) return;

            var currentTime = Data.editorSettings.currentTime;
            PushBounded(_redo, CaptureState());
            RestoreState(_undo.Pop());
            SetCurrentTimeTransient(currentTime);
            RecalculateDirty();
            NotifyChanged();
        }

        public void Redo()
        {
            if (_redo.Count == 0) return;

            var currentTime = Data.editorSettings.currentTime;
            PushBounded(_undo, CaptureState());
            RestoreState(_redo.Pop());
            SetCurrentTimeTransient(currentTime);
            RecalculateDirty();
            NotifyChanged();
        }

        public LightingUnitData FindUnit(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return null;
            return Data.lightingUnits.FirstOrDefault(unit =>
                unit != null && string.Equals(unit.unitId, unitId, StringComparison.Ordinal));
        }

        public ColorKeyframeData FindColorKeyframe(string unitId, string keyframeId)
        {
            var unit = FindUnit(unitId);
            if (unit?.track?.colorKeyframes == null || string.IsNullOrEmpty(keyframeId)) return null;
            return unit.track.colorKeyframes.FirstOrDefault(key =>
                key != null && string.Equals(key.keyframeId, keyframeId, StringComparison.Ordinal));
        }

        public ColorKeyframeData FindColorKeyframe(string keyframeId)
        {
            return TryFindColorKeyframeLocation(keyframeId, out _, out var keyframe)
                ? keyframe
                : null;
        }

        public LightingUnitData FindUnitForColorKeyframe(string keyframeId)
        {
            return TryFindColorKeyframeLocation(keyframeId, out var unit, out _)
                ? unit
                : null;
        }

        public string CreateNextUnitId()
        {
            var usedIds = new HashSet<string>(
                Data.lightingUnits
                    .Where(unit => unit != null && !string.IsNullOrEmpty(unit.unitId))
                    .Select(unit => unit.unitId),
                StringComparer.Ordinal);

            var index = 1;
            while (true)
            {
                var candidate = $"Light{index:000}";
                if (!usedIds.Contains(candidate)) return candidate;
                index++;
            }
        }

        public void SetCurrentTimeTransient(float time)
        {
            Data.editorSettings.currentTime = Mathf.Clamp(time, 0f, Data.metadata.duration);
        }

        public void SetPreviewLightSizeNoHistory(float value)
        {
            Data.editorSettings.previewLightSize = Mathf.Clamp(
                value,
                ScenarioDataUtility.MinPreviewLightSize,
                ScenarioDataUtility.MaxPreviewLightSize);
        }

        public bool TrySetUnitPreviewPositionNoHistory(string unitId, float previewX, float previewY)
        {
            var unit = FindUnit(unitId);
            if (unit == null) return false;

            unit.previewX = Mathf.Clamp01(previewX);
            unit.previewY = Mathf.Clamp01(previewY);
            return true;
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
            if (string.IsNullOrEmpty(unitId)) return;
            Execute(data => data.lightingUnits.RemoveAll(unit =>
                unit != null && string.Equals(unit.unitId, unitId, StringComparison.Ordinal)));
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
                    track = includeTrackData ? CloneTrack(source.track) : new TrackData()
                };
                data.lightingUnits.Add(created);
            });
            return created;
        }

        public ColorKeyframeData AddColorKeyframe(string unitId, float time, out string error)
        {
            error = null;
            var unit = FindUnit(unitId);
            if (unit == null)
            {
                error = "Track not found.";
                return null;
            }
            if (unit.track.locked)
            {
                error = "Track is locked.";
                return null;
            }

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
                var target = data.lightingUnits.First(item => item.unitId == unitId).track;
                created = new ColorKeyframeData
                {
                    keyframeId = Guid.NewGuid().ToString("N"),
                    time = candidate,
                    color = SerializableColor.FromUnityColor(initialColor)
                };
                target.colorKeyframes.Add(created);
                target.colorKeyframes.Sort((a, b) => a.time.CompareTo(b.time));
            });
            return created;
        }


        public bool DeleteColorKeyframes(IEnumerable<string> keyframeIds, out string error)
        {
            error = null;
            var ids = ToDistinctIds(keyframeIds);
            if (ids.Count == 0) return true;

            foreach (var id in ids)
            {
                if (!TryFindColorKeyframeLocation(id, out var unit, out _))
                {
                    error = "Color keyframe not found.";
                    return false;
                }
                if (unit.track.locked)
                {
                    error = "One or more selected keyframes are on a locked track.";
                    return false;
                }
            }

            Execute(data =>
            {
                foreach (var unit in data.lightingUnits)
                    unit.track.colorKeyframes.RemoveAll(key => ids.Contains(key.keyframeId));
            });
            return true;
        }

        public bool TrySetColorKeyframeTime(string unitId, string keyframeId, float time, out string error)
        {
            var beforeState = CaptureState();
            if (!TrySetColorKeyframeTimeNoHistory(unitId, keyframeId, time, out error))
                return false;

            CommitExternalEdit(beforeState);
            return true;
        }

        public bool TrySetColorKeyframeTimeNoHistory(string unitId, string keyframeId, float time, out string error)
        {
            error = null;
            if (FindColorKeyframe(unitId, keyframeId) == null)
            {
                error = "Color keyframe not found on the specified track.";
                return false;
            }

            var clamped = Mathf.Clamp(time, 0f, Data.metadata.duration);
            return TrySetColorKeyframeTimesNoHistory(
                new Dictionary<string, float> { [keyframeId] = clamped },
                out error);
        }

        /// <summary>
        /// Applies multiple keyframe times atomically without creating an undo entry.
        /// Intended for an external edit transaction such as a drag operation.
        /// </summary>
        public bool TrySetColorKeyframeTimesNoHistory(
            IDictionary<string, float> proposedTimes,
            out string error)
        {
            error = null;
            if (proposedTimes == null || proposedTimes.Count == 0) return false;

            var resolvedKeyframes = new Dictionary<string, ColorKeyframeData>(StringComparer.Ordinal);
            foreach (var pair in proposedTimes)
            {
                if (!TryFindColorKeyframeLocation(pair.Key, out var unit, out var keyframe))
                {
                    error = "Color keyframe not found.";
                    return false;
                }
                if (unit.track.locked)
                {
                    error = "One or more selected keyframes are on a locked track.";
                    return false;
                }
                if (pair.Value < -ScenarioDataUtility.TimeEpsilon ||
                    pair.Value > Data.metadata.duration + ScenarioDataUtility.TimeEpsilon)
                {
                    error = "Keyframe time is outside the scenario range.";
                    return false;
                }
                resolvedKeyframes[pair.Key] = keyframe;
            }

            if (!ValidateProposedTimes(proposedTimes, out error))
                return false;

            foreach (var pair in proposedTimes)
            {
                resolvedKeyframes[pair.Key].time = Mathf.Clamp(
                    pair.Value,
                    0f,
                    Data.metadata.duration);
            }

            foreach (var unit in Data.lightingUnits)
                unit.track.colorKeyframes.Sort((a, b) => a.time.CompareTo(b.time));
            return true;
        }


        public bool TrySetColorKeyframesColor(
            IEnumerable<string> keyframeIds,
            Color color,
            out string error)
        {
            error = null;
            var ids = ToDistinctIds(keyframeIds);
            if (ids.Count == 0)
            {
                error = "No color keyframes selected.";
                return false;
            }

            foreach (var id in ids)
            {
                if (!TryFindColorKeyframeLocation(id, out var unit, out _))
                {
                    error = "Color keyframe not found.";
                    return false;
                }
                if (unit.track.locked)
                {
                    error = "One or more selected keyframes are on a locked track.";
                    return false;
                }
            }

            var serializedColor = SerializableColor.FromUnityColor(color);
            Execute(data =>
            {
                foreach (var unit in data.lightingUnits)
                {
                    foreach (var keyframe in unit.track.colorKeyframes)
                    {
                        if (ids.Contains(keyframe.keyframeId))
                            keyframe.color = serializedColor;
                    }
                }
            });
            return true;
        }

        public float SnapColorKeyframeTime(float rawTime, string unitId, string excludedKeyframeId)
        {
            // Kept for API compatibility. Snapping is intentionally global so keyframes can
            // align across tracks; unitId historically did not scope the candidates.
            return SnapColorKeyframeTime(
                rawTime,
                string.IsNullOrEmpty(excludedKeyframeId) ? null : new[] { excludedKeyframeId });
        }

        public float SnapColorKeyframeTime(float rawTime, IEnumerable<string> excludedKeyframeIds)
        {
            return SnapColorKeyframeTimeCore(
                rawTime,
                Data.lightingUnits.SelectMany(unit => unit.track.colorKeyframes),
                excludedKeyframeIds);
        }

        public static float GetGridInterval(float pixelsPerSecond)
        {
            if (pixelsPerSecond < 50f) return 1f;
            if (pixelsPerSecond < 100f) return 0.5f;
            if (pixelsPerSecond < 180f) return 0.25f;
            return 0.1f;
        }

        private float SnapColorKeyframeTimeCore(
            float rawTime,
            IEnumerable<ColorKeyframeData> candidates,
            IEnumerable<string> excludedKeyframeIds)
        {
            var clamped = Mathf.Clamp(rawTime, 0f, Data.metadata.duration);
            if (!Data.editorSettings.snapEnabled) return clamped;

            var pixelsPerSecond = Mathf.Max(20f, Data.editorSettings.pixelsPerSecond);
            var threshold = 8f / pixelsPerSecond;
            var grid = GetGridInterval(pixelsPerSecond);
            var nearest = Mathf.Round(clamped / grid) * grid;
            var bestDistance = Mathf.Abs(nearest - clamped);
            var excluded = new HashSet<string>(
                excludedKeyframeIds ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);

            Consider(Data.editorSettings.currentTime, clamped, ref nearest, ref bestDistance);
            foreach (var keyframe in candidates)
            {
                if (keyframe == null || excluded.Contains(keyframe.keyframeId)) continue;
                Consider(keyframe.time, clamped, ref nearest, ref bestDistance);
            }

            return bestDistance <= threshold
                ? Mathf.Clamp(nearest, 0f, Data.metadata.duration)
                : clamped;
        }

        private bool ValidateProposedTimes(
            IDictionary<string, float> proposedTimes,
            out string error)
        {
            error = null;
            foreach (var unit in Data.lightingUnits)
            {
                var times = new List<float>(unit.track.colorKeyframes.Count);
                foreach (var keyframe in unit.track.colorKeyframes)
                {
                    times.Add(proposedTimes.TryGetValue(keyframe.keyframeId, out var proposed)
                        ? proposed
                        : keyframe.time);
                }

                times.Sort();
                for (var i = 1; i < times.Count; i++)
                {
                    if (Mathf.Abs(times[i] - times[i - 1]) <= ScenarioDataUtility.TimeEpsilon)
                    {
                        error = "Different color keyframes cannot occupy the same time on a track.";
                        return false;
                    }
                }
            }
            return true;
        }

        private bool TryFindColorKeyframeLocation(
            string keyframeId,
            out LightingUnitData owner,
            out ColorKeyframeData keyframe)
        {
            owner = null;
            keyframe = null;
            if (string.IsNullOrEmpty(keyframeId)) return false;

            foreach (var unit in Data.lightingUnits)
            {
                if (unit?.track?.colorKeyframes == null) continue;
                var found = unit.track.colorKeyframes.FirstOrDefault(item =>
                    item != null && string.Equals(item.keyframeId, keyframeId, StringComparison.Ordinal));
                if (found == null) continue;

                owner = unit;
                keyframe = found;
                return true;
            }
            return false;
        }

        private static HashSet<string> ToDistinctIds(IEnumerable<string> ids)
        {
            return new HashSet<string>(
                (ids ?? Enumerable.Empty<string>()).Where(id => !string.IsNullOrEmpty(id)),
                StringComparer.Ordinal);
        }

        private static TrackData CloneTrack(TrackData source)
        {
            var clone = new TrackData();
            if (source == null) return clone;

            clone.locked = source.locked;
            clone.muted = source.muted;
            clone.colorKeyframes = (source.colorKeyframes ?? new List<ColorKeyframeData>())
                .Where(keyframe => keyframe != null)
                .Select(keyframe => new ColorKeyframeData
                {
                    keyframeId = Guid.NewGuid().ToString("N"),
                    time = keyframe.time,
                    color = keyframe.color
                })
                .OrderBy(keyframe => keyframe.time)
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
            if (data.lightingUnits.All(unit =>
                    unit == null || !string.Equals(unit.displayName, candidate, StringComparison.Ordinal)))
                return candidate;

            var index = 2;
            while (true)
            {
                candidate = $"{baseName} Copy {index}";
                if (data.lightingUnits.All(unit =>
                        unit == null || !string.Equals(unit.displayName, candidate, StringComparison.Ordinal)))
                    return candidate;
                index++;
            }
        }

        private static bool HasColorKeyframeAtTime(
            TrackData track,
            float time,
            string ignoredKeyframeId)
        {
            return track?.colorKeyframes != null && track.colorKeyframes.Any(keyframe =>
                keyframe != null &&
                keyframe.keyframeId != ignoredKeyframeId &&
                Mathf.Abs(keyframe.time - time) <= ScenarioDataUtility.TimeEpsilon);
        }

        private static void Consider(float candidate, float raw, ref float nearest, ref float bestDistance)
        {
            var distance = Mathf.Abs(candidate - raw);
            if (distance >= bestDistance) return;
            nearest = candidate;
            bestDistance = distance;
        }

        private string CapturePersistentState()
        {
            if (Data == null) return string.Empty;
            if (Data.editorSettings == null) return CaptureState();

            // currentTime is a transport/playback cursor, not an authored project edit.
            var currentTime = Data.editorSettings.currentTime;
            try
            {
                Data.editorSettings.currentTime = 0f;
                return CaptureState();
            }
            finally
            {
                Data.editorSettings.currentTime = currentTime;
            }
        }

        private void RestoreState(string serializedState)
        {
            var restored = JsonUtility.FromJson<ScenarioData>(serializedState);
            Data = ScenarioDataUtility.Normalize(restored ?? ScenarioData.CreateDefault());
        }

        private void RecalculateDirty()
        {
            IsDirty = !string.Equals(
                CapturePersistentState(),
                _savedPersistentState ?? string.Empty,
                StringComparison.Ordinal);
        }

        private static void PushBounded(Stack<string> stack, string state)
        {
            stack.Push(state);
            if (stack.Count <= MaxHistoryEntries) return;

            var newestStates = stack.Take(MaxHistoryEntries).Reverse().ToArray();
            stack.Clear();
            foreach (var item in newestStates)
                stack.Push(item);
        }

        private void ClearHistory()
        {
            _undo.Clear();
            _redo.Clear();
        }

        private static string NormalizeProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path.Trim();
            }
        }

        private void NotifyChanged()
        {
            Changed?.Invoke();
        }
    }
}
