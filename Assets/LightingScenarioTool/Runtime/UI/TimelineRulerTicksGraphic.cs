using UnityEngine;
using UnityEngine.UI;

namespace LightingScenarioTool
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TimelineRulerTicksGraphic : MaskableGraphic
    {
        private float _duration;
        private float _pixelsPerSecond;
        private float _interval;
        private float _tickHeight = 13f;

        internal void Configure(float duration, float pixelsPerSecond, float interval)
        {
            _duration = Mathf.Max(0f, duration);
            _pixelsPerSecond = Mathf.Max(0.001f, pixelsPerSecond);
            _interval = Mathf.Max(0.001f, interval);
            color = AppTheme.TextSecondary;
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
            if (_duration <= 0f || _pixelsPerSecond <= 0f || _interval <= 0f) return;

            var rect = rectTransform.rect;
            var count = Mathf.CeilToInt(_duration / _interval);
            var yMin = rect.yMin;
            var yMax = Mathf.Min(rect.yMax, yMin + _tickHeight);
            for (var i = 0; i <= count; i++)
            {
                var time = Mathf.Min(_duration, i * _interval);
                var x = Mathf.Round(rect.xMin + TimelineCoordinates.TimeToX(time, _pixelsPerSecond)) + 0.5f;
                TimelineTrackGridGraphic.AddQuad(vh, x - 0.5f, yMin, x + 0.5f, yMax, color, color);
                if (time >= _duration - 0.0001f) break;
            }
        }
    }
}
