using UnityEngine;
using UnityEngine.EventSystems;

namespace LightingScenarioTool
{
    internal sealed class TimelineRulerInput : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        private LightingScenarioApp _app;
        private RectTransform _rt;
        public void Initialize(LightingScenarioApp app, RectTransform rt) { _app = app; _rt = rt; }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) SetTime(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) SetTime(eventData);
        }

        private void SetTime(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rt, eventData.position, eventData.pressEventCamera, out var local)) return;
            var x = local.x + _rt.pivot.x * _rt.rect.width;
            _app.SetCurrentTime(TimelineCoordinates.XToTime(
                x,
                _app.Document.Data.metadata.duration,
                _app.Document.Data.editorSettings.pixelsPerSecond));
        }
    }
}
