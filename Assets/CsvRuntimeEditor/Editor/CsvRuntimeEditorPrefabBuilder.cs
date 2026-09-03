#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RuntimeCsvEditor.Editor
{
    public static class CsvRuntimeEditorPrefabBuilder
    {
        private const string PrefabDirectory = "Assets/CsvRuntimeEditor/Prefabs";
        private const string MainPrefabPath = PrefabDirectory + "/CsvRuntimeEditor.prefab";
        private const string RowPrefabPath = PrefabDirectory + "/CsvTableRow.prefab";
        private const string HeaderCellPrefabPath = PrefabDirectory + "/CsvHeaderCell.prefab";
        private const string TextCellPrefabPath = PrefabDirectory + "/CsvTextCell.prefab";
        private const string EnumCellPrefabPath = PrefabDirectory + "/CsvEnumCell.prefab";

        private static readonly Color PanelColor = new(0.13f, 0.13f, 0.13f, 0.98f);
        private static readonly Color ToolbarColor = new(0.18f, 0.18f, 0.18f, 1f);
        private static readonly Color HeaderColor = new(0.24f, 0.24f, 0.24f, 1f);
        private static readonly Color FieldColor = new(0.10f, 0.10f, 0.10f, 1f);
        private static readonly Color ButtonColor = new(0.28f, 0.28f, 0.28f, 1f);
        private static readonly Color TextColor = new(0.92f, 0.92f, 0.92f, 1f);

        [MenuItem("Tools/CSV Runtime Editor/Build Prefabs")]
        public static void BuildPrefabs()
        {
            EnsureFolder("Assets/CsvRuntimeEditor");
            EnsureFolder(PrefabDirectory);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            BuildRowPrefab(font);
            BuildHeaderCellPrefab(font);
            BuildTextCellPrefab(font);
            BuildEnumCellPrefab(font);
            BuildMainPrefab(font);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(MainPrefabPath);
            Debug.Log($"CSV Runtime Editor prefabs were generated under {PrefabDirectory}");
        }

        [MenuItem("Tools/CSV Runtime Editor/Add Editor To Current Scene")]
        public static void AddEditorToCurrentScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainPrefabPath);
            if (prefab == null)
            {
                BuildPrefabs();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainPrefabPath);
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Add CSV Runtime Editor");
            Selection.activeGameObject = instance;
        }

        private static void BuildRowPrefab(Font font)
        {
            var root = NewUiObject("CsvTableRow");
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(600f, 38f);
            var bg = AddImage(root, new Color(0.16f, 0.16f, 0.16f, 1f));

            var rowButton = CreateButton("RowSelect", root.transform, string.Empty, font);
            var rowButtonRect = rowButton.GetComponent<RectTransform>();
            Stretch(rowButtonRect);
            rowButtonRect.offsetMax = new Vector2(-546f, 0f);
            var rowText = rowButton.GetComponentInChildren<Text>();

            var cellsRoot = NewUiObject("Cells", root.transform).GetComponent<RectTransform>();
            Stretch(cellsRoot);
            cellsRoot.offsetMin = new Vector2(54f, 0f);

            var view = root.AddComponent<CsvTableRowView>();
            view.EditorConfigure(rootRect, bg, rowButton, rowText, cellsRoot);

            SavePrefab(root, RowPrefabPath);
        }

        private static void BuildHeaderCellPrefab(Font font)
        {
            var root = NewUiObject("CsvHeaderCell");
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(190f, 38f);
            AddImage(root, HeaderColor);

            var label = CreateText("Label", root.transform, "Header", font, 14, TextAnchor.MiddleLeft, FontStyle.Bold);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(8f, 0f);
            label.rectTransform.offsetMax = new Vector2(-8f, 0f);

            var view = root.AddComponent<CsvHeaderCellView>();
            view.EditorConfigure(rect, label);
            SavePrefab(root, HeaderCellPrefabPath);
        }

        private static void BuildTextCellPrefab(Font font)
        {
            var root = NewUiObject("CsvTextCell");
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(190f, 38f);

            var input = CreateInputField(root, font);
            var inputRect = input.GetComponent<RectTransform>();
            Stretch(inputRect);
            inputRect.offsetMin = new Vector2(3f, 3f);
            inputRect.offsetMax = new Vector2(-3f, -3f);

            var view = root.AddComponent<CsvTextCellView>();
            view.EditorConfigure(rect, input);
            SavePrefab(root, TextCellPrefabPath);
        }

        private static void BuildEnumCellPrefab(Font font)
        {
            var root = NewUiObject("CsvEnumCell");
            var rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(190f, 38f);

            var dropdown = CreateDropdown(root, font);
            var dropdownRect = dropdown.GetComponent<RectTransform>();
            Stretch(dropdownRect);
            dropdownRect.offsetMin = new Vector2(3f, 3f);
            dropdownRect.offsetMax = new Vector2(-3f, -3f);

            var view = root.AddComponent<CsvEnumCellView>();
            view.EditorConfigure(rect, dropdown);
            SavePrefab(root, EnumCellPrefabPath);
        }

        private static void BuildMainPrefab(Font font)
        {
            var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RowPrefabPath).GetComponent<CsvTableRowView>();
            var headerCellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HeaderCellPrefabPath).GetComponent<CsvHeaderCellView>();
            var textCellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TextCellPrefabPath).GetComponent<CsvTextCellView>();
            var enumCellPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnumCellPrefabPath).GetComponent<CsvEnumCellView>();

            var root = new GameObject("CsvRuntimeEditor");
            var controller = root.AddComponent<CsvRuntimeEditorUGUI>();
            var view = root.AddComponent<CsvRuntimeEditorView>();

            var canvasGo = NewUiObject("Canvas", root.transform);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            var panel = NewUiObject("Panel", canvasGo.transform);
            Stretch(panel.GetComponent<RectTransform>());
            AddImage(panel, PanelColor);
            var rootLayout = panel.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(12, 12, 12, 12);
            rootLayout.spacing = 8f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            var titleBar = CreateBar("TitleBar", panel.transform, 44f);
            var title = CreateText("Title", titleBar.transform, "CSV Editor", font, 22, TextAnchor.MiddleLeft, FontStyle.Bold);
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var closeButton = CreateButton("CloseButton", titleBar.transform, "Close", font);
            SetLayoutWidth(closeButton.gameObject, 90f);

            var fileBar = CreateBar("FileToolbar", panel.transform, 46f);
            var pathLabel = CreateText("PathLabel", fileBar.transform, "CSV", font, 15, TextAnchor.MiddleLeft, FontStyle.Bold);
            SetLayoutWidth(pathLabel.gameObject, 48f);
            var pathInput = CreateInputField(fileBar, font);
            pathInput.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var loadButton = CreateButton("LoadButton", fileBar.transform, "Load", font);
            SetLayoutWidth(loadButton.gameObject, 90f);
            var saveButton = CreateButton("SaveButton", fileBar.transform, "Save", font);
            SetLayoutWidth(saveButton.gameObject, 90f);

            var editBar = CreateBar("EditToolbar", panel.transform, 46f);
            var addButton = CreateButton("AddRowButton", editBar.transform, "Add Row", font);
            SetLayoutWidth(addButton.gameObject, 110f);
            var deleteButton = CreateButton("DeleteRowButton", editBar.transform, "Delete Row", font);
            SetLayoutWidth(deleteButton.gameObject, 120f);
            var spacer = NewUiObject("Spacer", editBar.transform);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var summary = CreateText("Summary", editBar.transform, string.Empty, font, 15, TextAnchor.MiddleRight);
            SetLayoutWidth(summary.gameObject, 260f);

            var scrollGo = NewUiObject("TableScrollView", panel.transform);
            var scrollLayout = scrollGo.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1f;
            scrollLayout.minHeight = 200f;
            AddImage(scrollGo, new Color(0.08f, 0.08f, 0.08f, 1f));
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;

            var viewport = NewUiObject("Viewport", scrollGo.transform);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();
            AddImage(viewport, new Color(0f, 0f, 0f, 0.01f));

            var content = NewUiObject("Content", viewport.transform).GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(800f, 400f);
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = content;

            var statusBar = CreateBar("StatusBar", panel.transform, 34f);
            var status = CreateText("Status", statusBar.transform, "CSVを読み込んでください。", font, 14, TextAnchor.MiddleLeft);
            status.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            view.EditorConfigure(panel, pathInput, loadButton, saveButton, addButton, deleteButton, closeButton, scroll, content, status, summary);
            controller.EditorConfigure(view, rowPrefab, headerCellPrefab, textCellPrefab, enumCellPrefab);

            SavePrefab(root, MainPrefabPath);
        }

        private static GameObject CreateBar(string name, Transform parent, float height)
        {
            var go = NewUiObject(name, parent);
            AddImage(go, ToolbarColor);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 5, 5);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            return go;
        }

        private static Button CreateButton(string name, Transform parent, string label, Font font)
        {
            var go = NewUiObject(name, parent);
            var image = AddImage(go, ButtonColor);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText("Label", go.transform, label, font, 15, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform);
            return button;
        }

        private static InputField CreateInputField(GameObject root, Font font)
        {
            var image = AddImage(root, FieldColor);
            var text = CreateText("Text", root.transform, string.Empty, font, 15, TextAnchor.MiddleLeft);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(8f, 3f);
            text.rectTransform.offsetMax = new Vector2(-8f, -3f);
            var placeholder = CreateText("Placeholder", root.transform, string.Empty, font, 15, TextAnchor.MiddleLeft);
            Stretch(placeholder.rectTransform);
            placeholder.rectTransform.offsetMin = new Vector2(8f, 3f);
            placeholder.rectTransform.offsetMax = new Vector2(-8f, -3f);
            placeholder.color = new Color(TextColor.r, TextColor.g, TextColor.b, 0.4f);
            var input = root.AddComponent<InputField>();
            input.targetGraphic = image;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.lineType = InputField.LineType.SingleLine;
            return input;
        }

        private static InputField CreateInputField(Transform parent, Font font)
        {
            var go = NewUiObject("PathInput", parent);
            return CreateInputField(go, font);
        }

        private static Dropdown CreateDropdown(GameObject root, Font font)
        {
            var image = AddImage(root, FieldColor);
            var caption = CreateText("Label", root.transform, string.Empty, font, 15, TextAnchor.MiddleLeft);
            Stretch(caption.rectTransform);
            caption.rectTransform.offsetMin = new Vector2(8f, 3f);
            caption.rectTransform.offsetMax = new Vector2(-30f, -3f);

            var arrow = CreateText("Arrow", root.transform, "▼", font, 13, TextAnchor.MiddleCenter, FontStyle.Bold);
            var arrowRect = arrow.rectTransform;
            arrowRect.anchorMin = new Vector2(1f, 0f);
            arrowRect.anchorMax = new Vector2(1f, 1f);
            arrowRect.pivot = new Vector2(1f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-4f, 0f);
            arrowRect.sizeDelta = new Vector2(24f, 0f);

            var template = NewUiObject("Template", root.transform);
            var templateRect = template.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -2f);
            templateRect.sizeDelta = new Vector2(0f, 180f);
            AddImage(template, new Color(0.12f, 0.12f, 0.12f, 1f));
            var popupCanvas = template.AddComponent<Canvas>();
            popupCanvas.overrideSorting = true;
            popupCanvas.sortingOrder = 30010;
            template.AddComponent<GraphicRaycaster>();
            var templateScroll = template.AddComponent<ScrollRect>();
            templateScroll.horizontal = false;
            templateScroll.vertical = true;
            templateScroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = NewUiObject("Viewport", template.transform);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.AddComponent<RectMask2D>();
            AddImage(viewport, new Color(0f, 0f, 0f, 0.01f));

            var content = NewUiObject("Content", viewport.transform);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 38f);

            var item = NewUiObject("Item", content.transform);
            var itemRect = item.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 34f);
            var itemBg = AddImage(item, new Color(0.18f, 0.18f, 0.18f, 1f));
            var toggle = item.AddComponent<Toggle>();
            toggle.targetGraphic = itemBg;

            var checkmark = NewUiObject("Item Checkmark", item.transform);
            var checkmarkImage = AddImage(checkmark, new Color(0.75f, 0.75f, 0.75f, 1f));
            var checkmarkRect = checkmark.GetComponent<RectTransform>();
            checkmarkRect.anchorMin = new Vector2(0f, 0.5f);
            checkmarkRect.anchorMax = new Vector2(0f, 0.5f);
            checkmarkRect.pivot = new Vector2(0f, 0.5f);
            checkmarkRect.anchoredPosition = new Vector2(6f, 0f);
            checkmarkRect.sizeDelta = new Vector2(10f, 10f);
            toggle.graphic = checkmarkImage;

            var itemLabel = CreateText("Item Label", item.transform, "Option", font, 15, TextAnchor.MiddleLeft);
            Stretch(itemLabel.rectTransform);
            itemLabel.rectTransform.offsetMin = new Vector2(24f, 2f);
            itemLabel.rectTransform.offsetMax = new Vector2(-6f, -2f);

            templateScroll.viewport = viewport.GetComponent<RectTransform>();
            templateScroll.content = contentRect;

            var dropdown = root.AddComponent<Dropdown>();
            dropdown.targetGraphic = image;
            dropdown.captionText = caption;
            dropdown.template = templateRect;
            dropdown.itemText = itemLabel;
            template.SetActive(false);
            return dropdown;
        }

        private static Text CreateText(string name, Transform parent, string value, Font font, int fontSize, TextAnchor alignment, FontStyle style = FontStyle.Normal)
        {
            var go = NewUiObject(name, parent);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.fontStyle = style;
            text.color = TextColor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static Image AddImage(GameObject go, Color color)
        {
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static GameObject NewUiObject(string name, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetLayoutWidth(GameObject go, float width)
        {
            var element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
        }

        private static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
