using UnityEngine;
using UnityEngine.EventSystems;

namespace LightingScenarioTool
{
    internal sealed class TimelineWheelInput : MonoBehaviour, IScrollHandler
    {
        private TimelinePanel _panel;
        public void Initialize(TimelinePanel panel) => _panel = panel;

        public void OnScroll(PointerEventData eventData)
        {
            if (_panel == null) return;
            if (ShortcutInput.CtrlPressed)
            {
                if (eventData.scrollDelta.y > 0.001f) _panel.App.ZoomIn();
                else if (eventData.scrollDelta.y < -0.001f) _panel.App.ZoomOut();
            }
            else
            {
                _panel.ScrollVertical(eventData.scrollDelta.y);
            }
            eventData.Use();
        }
    }
}
