using UnityEngine;
using UnityEngine.EventSystems;

namespace LightingScenarioTool
{
    internal sealed class TrackColorInput : MonoBehaviour, IPointerClickHandler
    {
        private LightingScenarioApp _app;
        private string _unitId;
        private RectTransform _rt;
        public void Initialize(LightingScenarioApp app, string unitId, RectTransform rt)
        {
            _app = app;
            _unitId = unitId;
            _rt = rt;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (eventData.clickCount < 2)
            {
                _app.SelectUnit(_unitId);
                return;
            }

            var unit = _app.Document.FindUnit(_unitId);
            if (unit == null || unit.track.locked) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rt, eventData.position, eventData.pressEventCamera, out var local)) return;
            var x = local.x + _rt.pivot.x * _rt.rect.width;
            _app.CreateColorKeyframe(
                _unitId,
                TimelineCoordinates.XToTime(
                    x,
                    _app.Document.Data.metadata.duration,
                    _app.Document.Data.editorSettings.pixelsPerSecond));
        }
    }
}
