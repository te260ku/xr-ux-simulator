using UnityEngine;

namespace LightingScenarioTool
{
    /// <summary>
    /// Pure color evaluation. The timeline is defined only by Color Keyframes.
    /// </summary>
    public static class ScenarioEvaluator
    {
        public static Color Evaluate(LightingUnitData unit, float time)
        {
            if (unit == null || unit.track == null || unit.track.muted)
                return Color.black;

            return EvaluateBaseColor(unit.track, time);
        }

        public static Color EvaluateBaseColor(TrackData track, float time)
        {
            if (track == null || track.colorKeyframes == null || track.colorKeyframes.Count == 0)
                return Color.black;

            var keyframes = track.colorKeyframes;
            if (time < keyframes[0].time)
                return Color.black;

            if (keyframes.Count == 1 || time >= keyframes[keyframes.Count - 1].time)
                return keyframes[keyframes.Count - 1].color.ToUnityColor();

            for (var i = 0; i < keyframes.Count - 1; i++)
            {
                var previous = keyframes[i];
                var next = keyframes[i + 1];
                if (time > next.time) continue;

                var span = next.time - previous.time;
                if (span <= 0.000001f) return next.color.ToUnityColor();
                var u = Mathf.Clamp01((time - previous.time) / span);
                return Color.Lerp(previous.color.ToUnityColor(), next.color.ToUnityColor(), u);
            }

            return keyframes[keyframes.Count - 1].color.ToUnityColor();
        }
    }
}
