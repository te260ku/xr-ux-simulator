#if UNITY_EDITOR
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LightingScenarioTool
{
    internal static class UiFactory
    {
        public static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static Image AddImage(GameObject go, Color color)
        {
            var image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI AddText(
            GameObject go,
            string text,
            int fontSize = AppTheme.BodySize,
            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            if (go == null) throw new System.ArgumentNullException(nameof(go));
            var textHost = GetOrCreateTextHost(go);
            var wasActiveSelf = textHost.activeSelf;
            if (wasActiveSelf) textHost.SetActive(false);

            try
            {
                if (textHost.GetComponent<CanvasRenderer>() == null)
                    textHost.AddComponent<CanvasRenderer>();

                var label = textHost.GetComponent<TextMeshProUGUI>() ?? textHost.AddComponent<TextMeshProUGUI>();
                if (label == null)
                    throw new System.InvalidOperationException($"Failed to create TextMeshProUGUI on '{textHost.name}'.");

                label.enableAutoSizing = false;
                label.fontSize = Mathf.Round(fontSize);
                label.color = AppTheme.TextPrimary;
                label.alignment = ToTmpAlignment(anchor);
                label.text = text ?? string.Empty;
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Truncate;
                label.extraPadding = true;
                label.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
                return label;
            }
            finally
            {
                if (wasActiveSelf) textHost.SetActive(true);
            }
        }

        private static GameObject GetOrCreateTextHost(GameObject go)
        {
            if (go.GetComponent<TextMeshProUGUI>() != null) return go;
            if (go.GetComponent<Graphic>() == null) return go;

            const string childName = "__TMPText";
            var childTransform = go.transform.Find(childName);
            if (childTransform != null) return childTransform.gameObject;

            var child = CreateUIObject(childName, go.transform);
            Stretch((RectTransform)child.transform);
            return child;
        }

        public static Button CreateButton(
            Transform parent,
            string text,
            UnityAction onClick,
            float width = 72f,
            AppButtonStyle style = AppButtonStyle.Secondary)
        {
            var go = CreateUIObject("Button_" + text, parent);
            var background = AddImage(go, AppTheme.ButtonColors(style).normalColor);
            var button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = AppTheme.ButtonColors(style);
            if (onClick != null) button.onClick.AddListener(onClick);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = style == AppButtonStyle.Icon ? AppTheme.SmallControlHeight : AppTheme.ControlHeight;
            layout.minHeight = layout.preferredHeight;

            var textGo = CreateUIObject("Text", go.transform);
            Stretch((RectTransform)textGo.transform);
            var label = AddText(textGo, text, AppTheme.BodySize, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            if (style == AppButtonStyle.Primary) label.fontStyle = FontStyles.Bold;
            return button;
        }

        public static TMP_InputField CreateInput(Transform parent, string value, float width = 120f)
        {
            var go = CreateUIObject("Input", parent);
            go.SetActive(false);

            var background = AddImage(go, AppTheme.InputBackground);
            var field = go.AddComponent<TMP_InputField>();
            field.targetGraphic = background;
            field.transition = Selectable.Transition.ColorTint;
            field.colors = AppTheme.InputColors();

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = AppTheme.ControlHeight;
            layout.minHeight = AppTheme.ControlHeight;

            var border = go.AddComponent<Outline>();
            border.effectColor = AppTheme.Divider;
            border.effectDistance = new Vector2(1f, -1f);
            border.useGraphicAlpha = false;

            var viewportGo = CreateUIObject("Text Area", go.transform);
            var viewportRt = (RectTransform)viewportGo.transform;
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = new Vector2(8f, 3f);
            viewportRt.offsetMax = new Vector2(-8f, -3f);
            viewportGo.AddComponent<RectMask2D>();
            field.textViewport = viewportRt;

            var placeholderGo = CreateUIObject("Placeholder", viewportGo.transform);
            Stretch((RectTransform)placeholderGo.transform);
            var placeholder = AddText(placeholderGo, string.Empty, AppTheme.BodySize, TextAnchor.MiddleLeft);
            placeholder.color = AppTheme.TextDisabled;
            placeholder.raycastTarget = false;

            var textGo = CreateUIObject("Text", viewportGo.transform);
            Stretch((RectTransform)textGo.transform);
            var text = AddText(textGo, value, AppTheme.BodySize, TextAnchor.MiddleLeft);
            text.raycastTarget = false;

            field.textComponent = text;
            field.placeholder = placeholder;
            go.SetActive(true);
            field.customCaretColor = true;
            field.caretColor = AppTheme.TextPrimary;
            field.caretWidth = 2;
            field.selectionColor = new Color(AppTheme.Accent.r, AppTheme.Accent.g, AppTheme.Accent.b, 0.45f);
            field.SetTextWithoutNotify(value ?? string.Empty);
            return field;
        }

        public static Slider CreateSlider(
            Transform parent,
            float minValue,
            float maxValue,
            float value,
            float width = 120f)
        {
            var root = CreateUIObject("Slider", parent);
            var layout = root.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = AppTheme.ControlHeight;
            layout.minHeight = AppTheme.ControlHeight;

            var backgroundGo = CreateUIObject("Background", root.transform);
            var backgroundRt = (RectTransform)backgroundGo.transform;
            backgroundRt.anchorMin = new Vector2(0f, 0.5f);
            backgroundRt.anchorMax = new Vector2(1f, 0.5f);
            backgroundRt.offsetMin = new Vector2(0f, -2f);
            backgroundRt.offsetMax = new Vector2(0f, 2f);
            var background = AddImage(backgroundGo, AppTheme.Divider);
            background.raycastTarget = false;

            var fillAreaGo = CreateUIObject("Fill Area", root.transform);
            var fillAreaRt = (RectTransform)fillAreaGo.transform;
            Stretch(fillAreaRt);
            fillAreaRt.offsetMin = new Vector2(7f, 0f);
            fillAreaRt.offsetMax = new Vector2(-7f, 0f);

            var fillGo = CreateUIObject("Fill", fillAreaGo.transform);
            var fillRt = (RectTransform)fillGo.transform;
            Stretch(fillRt);
            var fill = AddImage(fillGo, AppTheme.Accent);
            fill.raycastTarget = false;

            var handleAreaGo = CreateUIObject("Handle Slide Area", root.transform);
            var handleAreaRt = (RectTransform)handleAreaGo.transform;
            Stretch(handleAreaRt);
            handleAreaRt.offsetMin = new Vector2(7f, 0f);
            handleAreaRt.offsetMax = new Vector2(-7f, 0f);

            var handleGo = CreateUIObject("Handle", handleAreaGo.transform);
            var handleRt = (RectTransform)handleGo.transform;
            handleRt.sizeDelta = new Vector2(14f, 18f);
            var handle = AddImage(handleGo, AppTheme.TextPrimary);

            var slider = root.AddComponent<Slider>();
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.wholeNumbers = false;
            slider.fillRect = fillRt;
            slider.handleRect = handleRt;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = AppTheme.AccentHover,
                pressedColor = AppTheme.AccentPressed,
                selectedColor = AppTheme.AccentHover,
                disabledColor = AppTheme.TextDisabled,
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            slider.SetValueWithoutNotify(Mathf.Clamp(value, minValue, maxValue));
            return slider;
        }

        public static Toggle CreateToggle(Transform parent, string label, bool value)
        {
            var root = CreateUIObject("Toggle_" + label, parent);
            var layout = root.AddComponent<LayoutElement>();
            layout.preferredWidth = 82f;
            layout.preferredHeight = AppTheme.ControlHeight;
            layout.minHeight = AppTheme.ControlHeight;

            var box = CreateUIObject("Box", root.transform);
            var boxRt = (RectTransform)box.transform;
            boxRt.anchorMin = new Vector2(0f, 0.5f);
            boxRt.anchorMax = new Vector2(0f, 0.5f);
            boxRt.pivot = new Vector2(0f, 0.5f);
            boxRt.anchoredPosition = new Vector2(2f, 0f);
            boxRt.sizeDelta = new Vector2(18f, 18f);
            var boxImage = AddImage(box, AppTheme.InputBackground);

            var check = CreateUIObject("Checkmark", box.transform);
            var checkRt = (RectTransform)check.transform;
            checkRt.anchorMin = new Vector2(0.2f, 0.2f);
            checkRt.anchorMax = new Vector2(0.8f, 0.8f);
            checkRt.offsetMin = Vector2.zero;
            checkRt.offsetMax = Vector2.zero;
            var checkImage = AddImage(check, AppTheme.Accent);

            var labelGo = CreateUIObject("Label", root.transform);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(28f, 0f);
            labelRt.offsetMax = Vector2.zero;
            AddText(labelGo, label, AppTheme.BodySize, TextAnchor.MiddleLeft).raycastTarget = false;

            var toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = checkImage;
            toggle.transition = Selectable.Transition.ColorTint;
            toggle.colors = AppTheme.InputColors();
            toggle.isOn = value;
            return toggle;
        }


        public static TextMeshProUGUI CreateLabel(Transform parent, string value, float width = 90f)
        {
            var go = CreateUIObject("Label", parent);
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = AppTheme.ControlHeight;
            return AddText(go, value, AppTheme.BodySize, TextAnchor.MiddleLeft);
        }

        public static TextMeshProUGUI CreateSecondaryLabel(Transform parent, string value, float width = 90f)
        {
            var label = CreateLabel(parent, value, width);
            label.fontSize = AppTheme.SecondarySize;
            label.color = AppTheme.TextSecondary;
            return label;
        }

        public static TextMeshProUGUI CreateSectionTitle(Transform parent, string value, float width = 120f)
        {
            var label = CreateLabel(parent, value, width);
            label.fontSize = AppTheme.SectionTitleSize;
            label.fontStyle = FontStyles.Bold;
            label.color = AppTheme.TextPrimary;
            return label;
        }

        public static RectTransform CreateRow(Transform parent, float height = 36f)
        {
            var go = CreateUIObject("Row", parent);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = AppTheme.SpacingS;
            layout.padding = new RectOffset(4, 4, 2, 2);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

private static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleCenter: return TextAlignmentOptions.Center;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Left;
            }
        }
    }
}
#endif
