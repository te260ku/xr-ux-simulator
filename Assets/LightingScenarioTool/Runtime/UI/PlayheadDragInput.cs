using UnityEngine;
using UnityEngine.EventSystems;

namespace LightingScenarioTool
{
    internal sealed class PlayheadDragInput : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        private LightingScenarioApp _app;
        private RectTransform _content;
        private RectTransform _allowedViewport;
        private bool _dragStartedInAllowedViewport;

        public void Initialize(
            LightingScenarioApp app,
            RectTransform content,
            RectTransform allowedViewport)
        {
            _app = app;
            _content = content;
            _allowedViewport = allowedViewport;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _dragStartedInAllowedViewport = false;
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (!CanStartDrag(eventData)) return;

            _dragStartedInAllowedViewport = true;
            SetTime(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (!_dragStartedInAllowedViewport) return;
            SetTime(eventData);
        }

        private bool CanStartDrag(PointerEventData eventData)
        {
            if (_app == null || _content == null || _allowedViewport == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(
                _allowedViewport,
                eventData.position,
                eventData.pressEventCamera);
        }

        private void SetTime(PointerEventData eventData)
        {
            if (_app == null || _content == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _content, eventData.position, eventData.pressEventCamera, out var local)) return;

            var x = local.x + _content.pivot.x * _content.rect.width;
            _app.SetCurrentTime(TimelineCoordinates.XToTime(
                x,
                _app.Document.Data.metadata.duration,
                _app.Document.Data.editorSettings.pixelsPerSecond));
        }

        private void OnDisable()
        {
            _dragStartedInAllowedViewport = false;
        }
    }
}
