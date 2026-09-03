using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RuntimeCsvEditor
{
    public sealed class CsvEnumCellView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Dropdown dropdown;
        [SerializeField] private Color normalTextColor = new(0.92f, 0.92f, 0.92f, 1f);
        [SerializeField] private Color invalidTextColor = new(1f, 0.55f, 0.35f, 1f);

        public RectTransform RectTransform => rectTransform;

        public void SetWidth(float width) => rectTransform.sizeDelta = new Vector2(width, rectTransform.sizeDelta.y);

        public void SetOptions(string currentValue, IReadOnlyList<string> enumValues, Action<string> onValueChanged)
        {
            dropdown.onValueChanged.RemoveAllListeners();

            var displayOptions = new List<string>();
            var storedValues = new List<string>();
            int selectedIndex = 0;
            bool isValid = false;

            if (string.IsNullOrEmpty(currentValue))
            {
                displayOptions.Add("(Select)");
                storedValues.Add(string.Empty);
            }
            else
            {
                for (int i = 0; i < enumValues.Count; i++)
                {
                    if (enumValues[i] == currentValue)
                    {
                        isValid = true;
                        break;
                    }
                }

                if (!isValid)
                {
                    displayOptions.Add($"⚠ Invalid: {currentValue}");
                    storedValues.Add(currentValue);
                }
            }

            for (int i = 0; i < enumValues.Count; i++)
            {
                string value = enumValues[i];
                displayOptions.Add(value);
                storedValues.Add(value);

                if (isValid && value == currentValue)
                    selectedIndex = displayOptions.Count - 1;
            }

            dropdown.ClearOptions();
            dropdown.AddOptions(displayOptions);
            dropdown.SetValueWithoutNotify(selectedIndex);
            dropdown.captionText.color = !isValid && !string.IsNullOrEmpty(currentValue) ? invalidTextColor : normalTextColor;
            dropdown.RefreshShownValue();

            dropdown.onValueChanged.AddListener(index =>
            {
                if (index < 0 || index >= storedValues.Count)
                    return;

                dropdown.captionText.color = normalTextColor;
                onValueChanged?.Invoke(storedValues[index]);
            });
        }

#if UNITY_EDITOR
        public void EditorConfigure(RectTransform configuredRect, Dropdown configuredDropdown)
        {
            rectTransform = configuredRect;
            dropdown = configuredDropdown;
        }
#endif
    }
}
