using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LightingScenarioTool
{
    internal sealed class RuntimeDialogView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _message;
        [SerializeField] private Button[] _buttons;
        [SerializeField] private TMP_Text[] _buttonLabels;

        internal RectTransform RectTransform => transform as RectTransform;

        internal void Initialize(string message, params DialogAction[] actions)
        {
            _message.text = message ?? string.Empty;
            var count = actions != null ? actions.Length : 0;
            for (var i = 0; i < _buttons.Length; i++)
            {
                var active = i < count && actions[i] != null;
                _buttons[i].gameObject.SetActive(active);
                if (!active) continue;

                var action = actions[i];
                _buttonLabels[i].text = action.Label;
                _buttons[i].onClick.RemoveAllListeners();
                _buttons[i].onClick.AddListener(() => action.Callback?.Invoke());
            }
        }

#if UNITY_EDITOR
        internal void EditorConfigure(TMP_Text message, Button[] buttons, TMP_Text[] buttonLabels)
        {
            _message = message;
            _buttons = buttons;
            _buttonLabels = buttonLabels;
        }
#endif
    }

    internal sealed class DialogAction
    {
        internal string Label { get; }
        internal Action Callback { get; }

        internal DialogAction(string label, Action callback)
        {
            Label = label ?? string.Empty;
            Callback = callback;
        }
    }
}
