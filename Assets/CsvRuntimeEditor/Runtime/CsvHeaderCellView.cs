using UnityEngine;
using UnityEngine.UI;

namespace RuntimeCsvEditor
{
    public sealed class CsvHeaderCellView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Text label;

        public RectTransform RectTransform => rectTransform;
        public void SetValue(string value) => label.text = value;
        public void SetWidth(float width) => rectTransform.sizeDelta = new Vector2(width, rectTransform.sizeDelta.y);

#if UNITY_EDITOR
        public void EditorConfigure(RectTransform configuredRect, Text configuredLabel)
        {
            rectTransform = configuredRect;
            label = configuredLabel;
        }
#endif
    }
}
