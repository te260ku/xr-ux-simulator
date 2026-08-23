using UnityEngine;
using UnityEngine.EventSystems;

namespace LightingScenarioTool
{
    internal sealed class HsvHueRingInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private HsvColorPickerControl _picker;
        private RectTransform _rect;
        private bool _dragging;

        public void Initialize(HsvColorPickerControl picker, RectTransform rect)
        {
            _picker = picker;
            _rect = rect;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _dragging = _picker.SetHueFromScreen(_rect, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _picker.SetHueFromScreen(_rect, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_dragging) return;
            _picker.SetHueFromScreen(_rect, eventData);
            _picker.Commit();
            _dragging = false;
        }
    }
}
