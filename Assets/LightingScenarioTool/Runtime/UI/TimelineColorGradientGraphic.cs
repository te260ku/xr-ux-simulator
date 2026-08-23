using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LightingScenarioTool
{
    /// <summary>
    /// Draws the color interpolation line between color keyframes as a single UI mesh.
    /// This is a procedural drawing primitive, so it intentionally remains one custom UI mesh
    /// rather than creating one GameObject/Prefab instance per gradient segment.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TimelineColorGradientGraphic : MaskableGraphic
    {
        private const float TargetSegmentWidth = 10f;
        private const int MaxSegmentsPerSpan = 64;

        private readonly List<GradientStop> _stops = new List<GradientStop>();
        private float _pixelsPerSecond = 1f;

        private struct GradientStop
        {
            internal float Time;
            internal Color Color;
        }

        internal void Configure(IReadOnlyList<ColorKeyframeData> keyframes, float pixelsPerSecond)
        {
            _pixelsPerSecond = Mathf.Max(0.001f, pixelsPerSecond);
            _stops.Clear();

            if (keyframes != null)
            {
                for (var i = 0; i < keyframes.Count; i++)
                {
                    var keyframe = keyframes[i];
                    if (keyframe == null) continue;

                    _stops.Add(new GradientStop
                    {
                        Time = Mathf.Max(0f, keyframe.time),
                        Color = keyframe.color.ToUnityColor()
                    });
                }
            }

            // Do not rely on the serialized list already being ordered. Imported or legacy
            // project data can be unsorted, and an inverted pair previously produced no quad.
            _stops.Sort((a, b) => a.Time.CompareTo(b.Time));

            raycastTarget = false;
            maskable = true;
            color = Color.white;
            SetAllDirty();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            SetVerticesDirty();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_stops.Count < 2 || _pixelsPerSecond <= 0f) return;

            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            for (var i = 0; i < _stops.Count - 1; i++)
            {
                var a = _stops[i];
                var b = _stops[i + 1];
                var x0 = rect.xMin + TimelineCoordinates.TimeToX(a.Time, _pixelsPerSecond);
                var x1 = rect.xMin + TimelineCoordinates.TimeToX(b.Time, _pixelsPerSecond);
                var width = x1 - x0;
                if (width <= 0.0001f) continue;

                // Keep the old visual behavior while drawing it in one mesh: long spans are
                // split into small quads whose colors are sampled from the interpolation.
                var segmentCount = Mathf.Clamp(Mathf.CeilToInt(width / TargetSegmentWidth), 1, MaxSegmentsPerSpan);
                for (var segment = 0; segment < segmentCount; segment++)
                {
                    var t0 = segment / (float)segmentCount;
                    var t1 = (segment + 1) / (float)segmentCount;
                    var segmentX0 = Mathf.Lerp(x0, x1, t0);
                    var segmentX1 = Mathf.Lerp(x0, x1, t1);

                    // Skip only the portion completely outside the Graphic rect. Keeping the
                    // edge clamped prevents very long timelines from creating huge geometry.
                    if (segmentX1 < rect.xMin || segmentX0 > rect.xMax) continue;
                    segmentX0 = Mathf.Max(segmentX0, rect.xMin);
                    segmentX1 = Mathf.Min(segmentX1, rect.xMax);
                    if (segmentX1 <= segmentX0) continue;

                    var leftColor = Color.Lerp(a.Color, b.Color, t0);
                    var rightColor = Color.Lerp(a.Color, b.Color, t1);
                    AddQuad(vh, segmentX0, rect.yMin, segmentX1, rect.yMax, leftColor, rightColor);
                }
            }
        }

        private static void AddQuad(
            VertexHelper vh,
            float xMin,
            float yMin,
            float xMax,
            float yMax,
            Color leftColor,
            Color rightColor)
        {
            var start = vh.currentVertCount;
            var vertex = UIVertex.simpleVert;

            vertex.color = leftColor;
            vertex.position = new Vector3(xMin, yMin, 0f);
            vertex.uv0 = new Vector2(0f, 0f);
            vh.AddVert(vertex);

            vertex.position = new Vector3(xMin, yMax, 0f);
            vertex.uv0 = new Vector2(0f, 1f);
            vh.AddVert(vertex);

            vertex.color = rightColor;
            vertex.position = new Vector3(xMax, yMax, 0f);
            vertex.uv0 = new Vector2(1f, 1f);
            vh.AddVert(vertex);

            vertex.position = new Vector3(xMax, yMin, 0f);
            vertex.uv0 = new Vector2(1f, 0f);
            vh.AddVert(vertex);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
