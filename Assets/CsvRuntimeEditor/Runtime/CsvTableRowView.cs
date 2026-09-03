using UnityEngine;
using UnityEngine.UI;

namespace RuntimeCsvEditor
{
    public sealed class CsvTableRowView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Image background;
        [SerializeField] private Button rowSelectButton;
        [SerializeField] private Text rowNumberText;
        [SerializeField] private RectTransform cellsRoot;
        [SerializeField] private Color headerColor = new(0.24f, 0.24f, 0.24f, 1f);
        [SerializeField] private Color normalColor = new(0.16f, 0.16f, 0.16f, 1f);
        [SerializeField] private Color selectedColor = new(0.24f, 0.32f, 0.43f, 1f);

        public RectTransform RectTransform => rectTransform;
        public RectTransform CellsRoot => cellsRoot;
        public Button RowSelectButton => rowSelectButton;

        public void SetupHeader(string rowLabel, float width, float height, float rowNumberWidth)
        {
            rectTransform.sizeDelta = new Vector2(width, height);
            background.color = headerColor;
            rowNumberText.text = rowLabel;
            rowNumberText.fontStyle = FontStyle.Bold;
            rowSelectButton.interactable = false;
            SetGeometry(rowNumberWidth);
        }

        public void SetupDataRow(string rowLabel, float width, float height, float rowNumberWidth, bool selected)
        {
            rectTransform.sizeDelta = new Vector2(width, height);
            background.color = selected ? selectedColor : normalColor;
            rowNumberText.text = rowLabel;
            rowNumberText.fontStyle = FontStyle.Normal;
            rowSelectButton.interactable = true;
            SetGeometry(rowNumberWidth);
        }

        private void SetGeometry(float rowNumberWidth)
        {
            var buttonRect = rowSelectButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 0f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0f, 0.5f);
            buttonRect.anchoredPosition = Vector2.zero;
            buttonRect.sizeDelta = new Vector2(rowNumberWidth, 0f);

            cellsRoot.anchorMin = Vector2.zero;
            cellsRoot.anchorMax = Vector2.one;
            cellsRoot.offsetMin = new Vector2(rowNumberWidth, 0f);
            cellsRoot.offsetMax = Vector2.zero;
        }

#if UNITY_EDITOR
        public void EditorConfigure(RectTransform configuredRect, Image configuredBackground, Button configuredRowButton, Text configuredRowText, RectTransform configuredCellsRoot)
        {
            rectTransform = configuredRect;
            background = configuredBackground;
            rowSelectButton = configuredRowButton;
            rowNumberText = configuredRowText;
            cellsRoot = configuredCellsRoot;
        }
#endif
    }
}
