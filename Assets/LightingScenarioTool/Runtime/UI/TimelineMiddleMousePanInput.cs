using UnityEngine;

namespace LightingScenarioTool
{
    internal sealed class TimelineMiddleMousePanInput : MonoBehaviour
    {
        private TimelinePanel _panel;
        private bool _panning;
        private Vector2 _lastPointerPosition;

        public void Initialize(TimelinePanel panel)
        {
            _panel = panel;
        }

        private void Update()
        {
            if (_panel == null) return;

            var pointerPosition = ShortcutInput.PointerPosition;
            if (!_panning)
            {
                if (!ShortcutInput.MiddleMousePressedThisFrame || !_panel.CanStartHorizontalPan(pointerPosition))
                    return;

                _panning = true;
                _lastPointerPosition = pointerPosition;
                return;
            }

            if (!ShortcutInput.MiddleMousePressed)
            {
                _panning = false;
                return;
            }

            var delta = pointerPosition - _lastPointerPosition;
            _lastPointerPosition = pointerPosition;

            // Grab-and-drag semantics: dragging the pointer right pulls the timeline
            // content right, so the scroll position itself moves left.
            if (Mathf.Abs(delta.x) > 0.001f)
                _panel.ScrollHorizontalByPixels(-delta.x);
        }

        private void OnDisable()
        {
            _panning = false;
        }
    }
}
