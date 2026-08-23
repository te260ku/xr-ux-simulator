using UnityEngine;
using UnityEngine.UI;

namespace LightingScenarioTool
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TimelineTrackGridGraphic : MaskableGraphic
    {
        private float _duration;
        private float _pixelsPerSecond;
        private float _interval;

        internal void Configure(float duration, float pixelsPerSecond, float interval)
        {
            _duration = Mathf.Max(0f, duration);
            _pixelsPerSecond = Mathf.Max(0.001f, pixelsPerSecond);
            _interval = Mathf.Max(0.001f, interval);
            color = AppTheme.Grid;
            raycastTarget = false;
            maskable = true;
            SetAllDirty();
        }


        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            // Horizontal row rule is part of the procedural grid so it remains visible even
            // when the track prefab hierarchy is customized.
            var dividerColor = AppTheme.Divider;
            AddQuad(vh, rect.xMin, rect.yMin, rect.xMax, rect.yMin + 1f, dividerColor, dividerColor);

            if (_duration <= 0f || _pixelsPerSecond <= 0f || _interval <= 0f) return;

            var gridColor = AppTheme.Grid;
            var count = Mathf.CeilToInt(_duration / _interval);
            for (var i = 0; i <= count; i++)
            {
                var time = Mathf.Min(_duration, i * _interval);
                // Snap to a half pixel in the reference UI coordinate system so a one-unit
                // rule does not land between pixels and disappear through filtering.
                var x = Mathf.Round(rect.xMin + TimelineCoordinates.TimeToX(time, _pixelsPerSecond)) + 0.5f;
                AddQuad(vh, x - 0.5f, rect.yMin, x + 0.5f, rect.yMax, gridColor, gridColor);
                if (time >= _duration - 0.0001f) break;
            }
        }

        internal static void AddQuad(
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
            vh.AddVert(vertex);
            vertex.position = new Vector3(xMin, yMax, 0f);
            vh.AddVert(vertex);

            vertex.color = rightColor;
            vertex.position = new Vector3(xMax, yMax, 0f);
            vh.AddVert(vertex);
            vertex.position = new Vector3(xMax, yMin, 0f);
            vh.AddVert(vertex);

            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
