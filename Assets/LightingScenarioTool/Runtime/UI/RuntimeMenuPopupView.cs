using UnityEngine;

namespace LightingScenarioTool
{
    internal sealed class RuntimeMenuPopupView : MonoBehaviour
    {
        [SerializeField] private RectTransform _content;

        internal RectTransform Content => _content;
        internal RectTransform RectTransform => transform as RectTransform;

#if UNITY_EDITOR
        internal void EditorConfigure(RectTransform content)
        {
            _content = content;
        }
#endif
    }
}
