using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace LightingScenarioTool
{
    internal sealed class PresetSwatchRightClick : MonoBehaviour, IPointerClickHandler
    {
        private Action<Vector2> _onRightClick;

        public void Initialize(Action<Vector2> onRightClick)
        {
            _onRightClick = onRightClick;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right) return;
            _onRightClick?.Invoke(eventData.position);
        }
    }
}
