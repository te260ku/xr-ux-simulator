using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LightingScenarioTool
{
    internal sealed class RuntimeMenuItemView : MonoBehaviour
    {
        private const float CommandHeight = 28f;
        private const float SeparatorHeight = 9f;

        [SerializeField] private Image _background;
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private LayoutElement _layout;
        [SerializeField] private GameObject _separator;

        internal void Initialize(string label, Action callback)
        {
            _layout.preferredHeight = CommandHeight;
            _layout.minHeight = CommandHeight;
            _background.enabled = true;
            _button.enabled = true;
            _label.gameObject.SetActive(true);
            _separator.SetActive(false);
            _label.text = label ?? string.Empty;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => callback?.Invoke());
        }

        internal void InitializeSeparator()
        {
            _layout.preferredHeight = SeparatorHeight;
            _layout.minHeight = SeparatorHeight;
            _button.onClick.RemoveAllListeners();
            _button.enabled = false;
            _background.enabled = false;
            _label.gameObject.SetActive(false);
            _separator.SetActive(true);
        }

#if UNITY_EDITOR
        internal void EditorConfigure(
            Image background,
            Button button,
            TMP_Text label,
            LayoutElement layout,
            GameObject separator)
        {
            _background = background;
            _button = button;
            _label = label;
            _layout = layout;
            _separator = separator;
        }
#endif
    }
}
