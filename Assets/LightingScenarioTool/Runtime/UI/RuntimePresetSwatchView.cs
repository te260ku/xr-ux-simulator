using System;
using UnityEngine;
using UnityEngine.UI;

namespace LightingScenarioTool
{
    internal sealed class RuntimePresetSwatchView : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private Button _button;
        [SerializeField] private PresetSwatchRightClick _rightClick;

        internal void Initialize(Color color, Action onClick, Action<Vector2> onRightClick)
        {
            _image.color = color;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onClick?.Invoke());
            _rightClick.Initialize(onRightClick);
        }

#if UNITY_EDITOR
        internal void EditorConfigure(Image image, Button button, PresetSwatchRightClick rightClick)
        {
            _image = image;
            _button = button;
            _rightClick = rightClick;
        }
#endif
    }
}
