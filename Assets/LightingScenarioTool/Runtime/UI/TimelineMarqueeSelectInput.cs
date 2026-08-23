using UnityEngine;
using UnityEngine.EventSystems;

namespace LightingScenarioTool
{
    internal sealed class TimelineMarqueeSelectInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private TimelinePanel _panel;
        private bool _dragging;
        private Vector2 _startScreen;
        private Camera _eventCamera;
        private bool _additive;

        public void Initialize(TimelinePanel panel) => _panel = panel;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_panel == null || eventData.button != PointerEventData.InputButton.Left) return;
            _dragging = true;
            _startScreen = eventData.position;
            _eventCamera = eventData.pressEventCamera;
            _additive = ShortcutInput.CtrlPressed;
            _panel.BeginMarqueeSelection(_startScreen, _eventCamera);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _panel == null) return;
            _panel.UpdateMarqueeSelection(_startScreen, eventData.position, _eventCamera);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging || _panel == null) return;
            _dragging = false;
            _panel.EndMarqueeSelection(_startScreen, eventData.position, _additive);
        }

        private void OnDisable()
        {
            _dragging = false;
            _panel?.CancelMarqueeSelection();
        }
    }
}
