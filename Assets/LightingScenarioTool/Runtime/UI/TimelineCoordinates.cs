using UnityEngine;

namespace LightingScenarioTool
{
    /// <summary>
    /// Centralizes conversion between scenario time and timeline-local X coordinates.
    ///
    /// The edge padding keeps centered visuals such as the 0s ruler label, playhead head,
    /// and keyframe diamond fully inside the masked timeline viewport at both ends.
    /// All timeline drawing and pointer input must use this class so visual and hit-test
    /// coordinates remain identical.
    /// </summary>
    internal static class TimelineCoordinates
    {
        // TimelineRulerLabel is 78 px wide by default, so half of it is 39 px.
        // 40 px gives one extra pixel of safety without requiring any Hierarchy change.
        internal const float EdgePadding = 40f;

        internal static float ContentWidth(float duration, float pixelsPerSecond)
        {
            var safeDuration = Mathf.Max(0f, duration);
            var safePixelsPerSecond = Mathf.Max(0.001f, pixelsPerSecond);
            return EdgePadding * 2f + safeDuration * safePixelsPerSecond;
        }

        internal static float TimeToX(float time, float pixelsPerSecond)
        {
            var safePixelsPerSecond = Mathf.Max(0.001f, pixelsPerSecond);
            return EdgePadding + Mathf.Max(0f, time) * safePixelsPerSecond;
        }

        internal static float XToTime(float x, float pixelsPerSecond)
        {
            var safePixelsPerSecond = Mathf.Max(0.001f, pixelsPerSecond);
            return Mathf.Max(0f, (x - EdgePadding) / safePixelsPerSecond);
        }

        internal static float XToTime(float x, float duration, float pixelsPerSecond)
        {
            return Mathf.Clamp(XToTime(x, pixelsPerSecond), 0f, Mathf.Max(0f, duration));
        }
    }
}
