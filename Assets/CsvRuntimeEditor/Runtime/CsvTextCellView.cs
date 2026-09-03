using System;
using UnityEngine;
using UnityEngine.UI;

namespace RuntimeCsvEditor
{
    public sealed class CsvTextCellView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private InputField inputField;

        public RectTransform RectTransform => rectTransform;

        public void SetWidth(float width) => rectTransform.sizeDelta = new Vector2(width, rectTransform.sizeDelta.y);

        public void SetValue(string value, Action<string> onValueChanged)
        {
            inputField.onValueChanged.RemoveAllListeners();
            inputField.SetTextWithoutNotify(value ?? string.Empty);
            inputField.onValueChanged.AddListener(v => onValueChanged?.Invoke(v));
        }

#if UNITY_EDITOR
        public void EditorConfigure(RectTransform configuredRect, InputField configuredInput)
        {
            rectTransform = configuredRect;
            inputField = configuredInput;
        }
#endif
    }
}
