using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
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
            int fontSize = 14,
            TextAnchor anchor = TextAnchor.MiddleLeft)
        {
            if (go == null)
                throw new System.ArgumentNullException(nameof(go));

            // Unity UI allows only one Graphic-derived component on a GameObject.
            // Image / RawImage / TextMeshProUGUI all derive from Graphic, so a label
            // cannot be added directly to an object that already owns a background
            // Image. In that case create a stretched child dedicated to the TMP text.
            var textHost = GetOrCreateTextHost(go);

            var wasActiveSelf = textHost.activeSelf;
            if (wasActiveSelf)
                textHost.SetActive(false);

            try
            {
                if (textHost.GetComponent<CanvasRenderer>() == null)
                    textHost.AddComponent<CanvasRenderer>();

                var label = textHost.GetComponent<TextMeshProUGUI>();
                if (label == null)
                    label = textHost.AddComponent<TextMeshProUGUI>();

                if (label == null)
                    throw new System.InvalidOperationException(
                        $"Failed to create TextMeshProUGUI on '{textHost.name}'.");

                label.enableAutoSizing = false;
                label.fontSize = Mathf.Round(fontSize);
                label.color = new Color(0.94f, 0.94f, 0.94f, 1f);
                label.alignment = ToTmpAlignment(anchor);
                label.text = text ?? string.Empty;
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Truncate;
                label.extraPadding = true;
                // TMP uses SDF glyphs; keeping integer point sizes and refreshing geometry
                // after configuration gives clean edges under CanvasScaler up-scaling.
                label.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
                return label;
            }
            finally
            {
                if (wasActiveSelf)
                    textHost.SetActive(true);
            }
        }

        private static GameObject GetOrCreateTextHost(GameObject go)
        {
            var existingTmp = go.GetComponent<TextMeshProUGUI>();
            if (existingTmp != null)
                return go;

            // Any other Graphic (Image, RawImage, etc.) occupies the single Graphic
            // slot on this GameObject. Put TMP on a child instead.
            var existingGraphic = go.GetComponent<Graphic>();
            if (existingGraphic == null)
                return go;

            const string childName = "__TMPText";
            var childTransform = go.transform.Find(childName);
            GameObject child;
            if (childTransform != null)
            {
                child = childTransform.gameObject;
            }
            else
            {
                child = CreateUIObject(childName, go.transform);
                var rt = (RectTransform)child.transform;
                Stretch(rt);
            }

            return child;
        }

        public static Button CreateButton(Transform parent, string text, UnityAction onClick, float width = 72f)
        {
            var go = CreateUIObject("Button_" + text, parent);
            AddImage(go, new Color(0.22f, 0.22f, 0.22f, 1f));
            var button = go.AddComponent<Button>();
            if (onClick != null) button.onClick.AddListener(onClick);
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 30f;

            var textGo = CreateUIObject("Text", go.transform);
            var rt = (RectTransform)textGo.transform;
            Stretch(rt);
            AddText(textGo, text, 13, TextAnchor.MiddleCenter).raycastTarget = false;
            return button;
        }

        public static TMP_InputField CreateInput(Transform parent, string value, float width = 120f)
        {
            // Build the complete TMP input hierarchy while inactive. TMP_InputField and its
            // child TMP texts are then initialized together when the root is activated.
            var go = CreateUIObject("Input", parent);
            go.SetActive(false);

            AddImage(go, new Color(0.13f, 0.13f, 0.13f, 1f));
            var field = go.AddComponent<TMP_InputField>();
            field.targetGraphic = go.GetComponent<Image>();

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 30f;

            var viewportGo = CreateUIObject("Text Area", go.transform);
            var viewportRt = (RectTransform)viewportGo.transform;
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = new Vector2(6f, 2f);
            viewportRt.offsetMax = new Vector2(-6f, -2f);
            viewportGo.AddComponent<RectMask2D>();
            field.textViewport = viewportRt;

            var placeholderGo = CreateUIObject("Placeholder", viewportGo.transform);
            var placeholderRt = (RectTransform)placeholderGo.transform;
            Stretch(placeholderRt);
            var placeholder = AddText(placeholderGo, string.Empty, 13, TextAnchor.MiddleLeft);
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            placeholder.raycastTarget = false;

            var textGo = CreateUIObject("Text", viewportGo.transform);
            var textRt = (RectTransform)textGo.transform;
            Stretch(textRt);
            var text = AddText(textGo, value, 13, TextAnchor.MiddleLeft);
            text.raycastTarget = false;

            field.textComponent = text;
            field.placeholder = placeholder;

            // Activate only after all structural references are connected. From this point
            // TMP_InputField / TextMeshProUGUI have completed Awake/OnEnable, so setters that
            // rebuild the caret or label are safe to use.
            go.SetActive(true);
            field.customCaretColor = true;
            field.caretColor = new Color(0.95f, 0.95f, 0.95f, 1f);
            field.caretWidth = 2;
            field.selectionColor = new Color(0.35f, 0.55f, 0.85f, 0.55f);
            field.SetTextWithoutNotify(value ?? string.Empty);
            return field;
        }

        public static Toggle CreateToggle(Transform parent, string label, bool value)
        {
            var root = CreateUIObject("Toggle_" + label, parent);
            var layout = root.AddComponent<LayoutElement>();
            layout.preferredWidth = 80f;
            layout.preferredHeight = 30f;

            var box = CreateUIObject("Box", root.transform);
            var boxRt = (RectTransform)box.transform;
            boxRt.anchorMin = new Vector2(0f, 0.5f);
            boxRt.anchorMax = new Vector2(0f, 0.5f);
            boxRt.pivot = new Vector2(0f, 0.5f);
            boxRt.anchoredPosition = new Vector2(2f, 0f);
            boxRt.sizeDelta = new Vector2(20f, 20f);
            AddImage(box, new Color(0.15f, 0.15f, 0.15f, 1f));

            var check = CreateUIObject("Checkmark", box.transform);
            var checkRt = (RectTransform)check.transform;
            checkRt.anchorMin = new Vector2(0.2f, 0.2f);
            checkRt.anchorMax = new Vector2(0.8f, 0.8f);
            checkRt.offsetMin = Vector2.zero;
            checkRt.offsetMax = Vector2.zero;
            var checkImage = AddImage(check, new Color(0.85f, 0.85f, 0.85f, 1f));

            var labelGo = CreateUIObject("Label", root.transform);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(28f, 0f);
            labelRt.offsetMax = Vector2.zero;
            AddText(labelGo, label, 13, TextAnchor.MiddleLeft).raycastTarget = false;

            var toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = checkImage;
            toggle.isOn = value;
            return toggle;
        }

        public static TMP_Dropdown CreateDropdown(Transform parent, float width = 100f)
        {
            // As with TMP_InputField, configure the whole hierarchy before TMP_Dropdown is
            // activated. This avoids initialization-order dependent dirty/layout callbacks.
            var root = CreateUIObject("Dropdown", parent);
            root.SetActive(false);

            AddImage(root, new Color(0.15f, 0.15f, 0.15f, 1f));
            var layout = root.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 30f;
            var dropdown = root.AddComponent<TMP_Dropdown>();
            dropdown.targetGraphic = root.GetComponent<Image>();

            var labelGo = CreateUIObject("Label", root.transform);
            var labelRt = (RectTransform)labelGo.transform;
            Stretch(labelRt);
            labelRt.offsetMin = new Vector2(6f, 0f);
            labelRt.offsetMax = new Vector2(-20f, 0f);
            var label = AddText(labelGo, string.Empty, 13, TextAnchor.MiddleLeft);
            label.raycastTarget = false;
            dropdown.captionText = label;

            var arrowGo = CreateUIObject("Arrow", root.transform);
            var arrowRt = (RectTransform)arrowGo.transform;
            arrowRt.anchorMin = new Vector2(1f, 0.5f);
            arrowRt.anchorMax = new Vector2(1f, 0.5f);
            arrowRt.pivot = new Vector2(1f, 0.5f);
            arrowRt.anchoredPosition = new Vector2(-5f, 0f);
            arrowRt.sizeDelta = new Vector2(12f, 12f);
            AddText(arrowGo, "▼", 10, TextAnchor.MiddleCenter).raycastTarget = false;

            var template = CreateUIObject("Template", root.transform);
            template.SetActive(false);
            var templateRt = (RectTransform)template.transform;
            templateRt.anchorMin = new Vector2(0f, 0f);
            templateRt.anchorMax = new Vector2(1f, 0f);
            templateRt.pivot = new Vector2(0.5f, 1f);
            templateRt.anchoredPosition = new Vector2(0f, -2f);
            templateRt.sizeDelta = new Vector2(0f, 64f);
            AddImage(template, new Color(0.1f, 0.1f, 0.1f, 1f));
            var scroll = template.AddComponent<ScrollRect>();

            var viewport = CreateUIObject("Viewport", template.transform);
            var viewportRt = (RectTransform)viewport.transform;
            Stretch(viewportRt);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            AddImage(viewport, Color.white);

            var content = CreateUIObject("Content", viewport.transform);
            var contentRt = (RectTransform)content.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0f, 30f);

            var item = CreateUIObject("Item", content.transform);
            var itemRt = (RectTransform)item.transform;
            itemRt.anchorMin = new Vector2(0f, 0.5f);
            itemRt.anchorMax = new Vector2(1f, 0.5f);
            itemRt.sizeDelta = new Vector2(0f, 30f);
            var itemToggle = item.AddComponent<Toggle>();
            var itemBg = AddImage(item, new Color(0.12f, 0.12f, 0.12f, 1f));
            itemToggle.targetGraphic = itemBg;

            var itemLabelGo = CreateUIObject("Item Label", item.transform);
            var itemLabelRt = (RectTransform)itemLabelGo.transform;
            Stretch(itemLabelRt);
            itemLabelRt.offsetMin = new Vector2(6f, 0f);
            var itemLabel = AddText(itemLabelGo, "Option", 13, TextAnchor.MiddleLeft);
            itemLabel.raycastTarget = false;

            dropdown.template = templateRt;
            dropdown.itemText = itemLabel;
            scroll.viewport = viewportRt;
            scroll.content = contentRt;

            root.SetActive(true);
            return dropdown;
        }

        public static TextMeshProUGUI CreateLabel(Transform parent, string value, float width = 90f)
        {
            var go = CreateUIObject("Label", parent);
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 30f;
            return AddText(go, value, 13, TextAnchor.MiddleLeft);
        }

        public static RectTransform CreateRow(Transform parent, float height = 34f)
        {
            var go = CreateUIObject("Row", parent);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(4, 4, 2, 2);
            // Let the HorizontalLayoutGroup honor each child's LayoutElement width.
            // With childControlWidth=false, newly-created RectTransforms keep their default
            // ~100 px width and the preferredWidth values set by CreateLabel/CreateInput/
            // CreateButton are ignored, causing long rows to overflow the 16:9 canvas.
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

        public static void EnsureEventSystem()
        {
            if (Object.FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
            Object.DontDestroyOnLoad(go);
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
