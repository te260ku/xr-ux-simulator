using UnityEngine;
using UnityEngine.EventSystems;

namespace LightingScenarioTool
{
    internal sealed class TrackLabelClick : MonoBehaviour, IPointerClickHandler
    {
        private LightingScenarioApp _app;
        private string _unitId;
        public void Initialize(LightingScenarioApp app, string unitId) { _app = app; _unitId = unitId; }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                _app.SelectUnit(_unitId);
        }
    }
}
