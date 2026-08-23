#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LightingScenarioTool.Editor
{
    internal static class LightingScenarioUiPrefabBuilder
    {
        private const string RootFolder = "Assets/LightingScenarioTool";
        private const string PrefabsFolder = RootFolder + "/Prefabs";
        private const string UiFolder = PrefabsFolder + "/UI";
        private const string CatalogPath = UiFolder + "/LightingScenarioUiPrefabCatalog.asset";

        internal static LightingScenarioUiPrefabCatalog BuildOrUpdate()
        {
            EnsureFolder(RootFolder, "Prefabs");
            EnsureFolder(PrefabsFolder, "UI");
            DeleteObsoleteGeneratedAssets();

            var trackLabel = SavePrefab("TimelineTrackLabel.prefab", BuildTrackLabel);
            var trackTime = SavePrefab("TimelineTrackTime.prefab", BuildTrackTime);
            ValidateTrackTimePrefab(trackTime);
            var colorKeyframe = SavePrefab("ColorKeyframe.prefab", BuildColorKeyframe).GetComponent<ColorKeyframeView>();
            var rulerLabel = SavePrefab("TimelineRulerLabel.prefab", BuildRulerLabel).GetComponent<TMP_Text>();
            var rulerInteraction = SavePrefab("TimelineRulerInteraction.prefab", BuildRulerInteraction);
            var timePlayhead = SavePrefab("TimelinePlayhead.prefab", () => BuildPlayhead(false));
            var rulerPlayhead = SavePrefab("TimelineRulerPlayhead.prefab", () => BuildPlayhead(true));
            var previewLight = SavePrefab("PreviewLight.prefab", BuildPreviewLight).GetComponent<PreviewLightView>();
            var menuItem = SavePrefab("RuntimeMenuItem.prefab", BuildMenuItem).GetComponent<RuntimeMenuItemView>();
            var menuPopup = SavePrefab("RuntimeMenuPopup.prefab", BuildMenuPopup).GetComponent<RuntimeMenuPopupView>();
            var presetSwatch = SavePrefab("ColorPresetSwatch.prefab", BuildPresetSwatch).GetComponent<RuntimePresetSwatchView>();
            var colorPicker = SavePrefab("HsvColorPicker.prefab", BuildColorPicker).GetComponent<RuntimeColorPickerView>();
            var dialog = SavePrefab("RuntimeDialog.prefab", BuildDialog).GetComponent<RuntimeDialogView>();

            var catalog = AssetDatabase.LoadAssetAtPath<LightingScenarioUiPrefabCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LightingScenarioUiPrefabCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.EditorAssign(
                trackLabel,
                trackTime,
                colorKeyframe,
                rulerLabel,
                rulerInteraction,
                timePlayhead,
                rulerPlayhead,
                previewLight,
                menuPopup,
                menuItem,
                colorPicker,
                presetSwatch,
                dialog);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return catalog;
        }

        private static void ValidateTrackTimePrefab(GameObject prefab)
        {
            if (prefab == null)
                throw new InvalidOperationException("Failed to create TimelineTrackTime.prefab.");

            var gridTransform = prefab.transform.Find("GridGraphic");
            var grid = gridTransform != null
                ? gridTransform.GetComponent<TimelineTrackGridGraphic>()
                : null;
            if (grid == null)
            {
                throw new InvalidOperationException(
                    "TimelineTrackTime.prefab was generated without TimelineTrackGridGraphic. " +
                    "The timeline grid cannot be rendered.");
            }

            var lane = prefab.transform.Find("ColorKeyframeLane");
            var gradientTransform = lane != null ? lane.Find("ColorGradient") : null;
            var gradient = gradientTransform != null
                ? gradientTransform.GetComponent<TimelineColorGradientGraphic>()
                : null;

            if (gradient == null)
            {
                throw new InvalidOperationException(
                    "TimelineTrackTime.prefab was generated without TimelineColorGradientGraphic. " +
                    "The timeline color interpolation line cannot be rendered.");
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            var full = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(full)) AssetDatabase.CreateFolder(parent, child);
        }

        private static void DeleteObsoleteGeneratedAssets()
        {
            DeleteAssetIfPresent(UiFolder + "/RuntimeMenuSeparator.prefab");
        }

        private static void DeleteAssetIfPresent(string assetPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);
        }

        private static GameObject SavePrefab(string fileName, Func<GameObject> build)
        {
            var temp = build();
            temp.name = System.IO.Path.GetFileNameWithoutExtension(fileName);
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(temp, UiFolder + "/" + fileName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(temp);
            }
        }

        private static GameObject BuildTrackLabel()
        {
            var root = UiFactory.CreateUIObject("TimelineTrackLabel", null);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(340f, 40f);
            UiFactory.AddImage(root, AppTheme.TrackHeaderEven);
            root.AddComponent<TrackLabelClick>();

            var accent = UiFactory.CreateUIObject("SelectionAccent", root.transform);
            var accentRt = (RectTransform)accent.transform;
            accentRt.anchorMin = new Vector2(0f, 0f);
            accentRt.anchorMax = new Vector2(0f, 1f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.sizeDelta = new Vector2(3f, 0f);
            UiFactory.AddImage(accent, AppTheme.Accent).raycastTarget = false;

            var nameInput = UiFactory.CreateInput(root.transform, "Track Name", 148f);
            nameInput.gameObject.name = "TrackNameInput";
            var nameRt = (RectTransform)nameInput.transform;
            nameRt.anchorMin = nameRt.anchorMax = new Vector2(0f, 0.5f);
            nameRt.pivot = new Vector2(0f, 0.5f);
            nameRt.anchoredPosition = new Vector2(12f, 0f);
            nameRt.sizeDelta = new Vector2(148f, 28f);
            var nameLayout = nameInput.GetComponent<LayoutElement>();
            if (nameLayout != null) nameLayout.preferredHeight = 28f;

            CreateMiniToggle(root.transform, "Lock", new Vector2(166f, 0f), 52f);
            CreateMiniToggle(root.transform, "Mute", new Vector2(224f, 0f), 54f);
            CreateMiniButton(root.transform, "▲", new Vector2(284f, 0f), 24f);
            CreateMiniButton(root.transform, "▼", new Vector2(312f, 0f), 24f);
            CreateBottomSeparator(root.transform);
            return root;
        }

        private static void CreateMiniToggle(Transform parent, string text, Vector2 position, float width)
        {
            var root = UiFactory.CreateUIObject("Toggle_" + text, parent);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(width, 26f);
            var bg = UiFactory.AddImage(root, AppTheme.Elevated);
            var check = UiFactory.CreateUIObject("Active", root.transform);
            var checkRt = (RectTransform)check.transform;
            UiFactory.Stretch(checkRt);
            checkRt.offsetMin = new Vector2(3f, 3f);
            checkRt.offsetMax = new Vector2(-3f, -3f);
            var checkImage = UiFactory.AddImage(check, AppTheme.AccentTint);
            var textGo = UiFactory.CreateUIObject("Text", root.transform);
            UiFactory.Stretch((RectTransform)textGo.transform);
            var label = UiFactory.AddText(textGo, text, AppTheme.SecondarySize, TextAnchor.MiddleCenter);
            label.raycastTarget = false;
            var toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = checkImage;
            toggle.SetIsOnWithoutNotify(false);
        }

        private static void CreateMiniButton(Transform parent, string text, Vector2 position, float width)
        {
            var button = UiFactory.CreateButton(parent, text, null, width, AppButtonStyle.Icon);
            var rt = (RectTransform)button.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(width, 26f);
        }

        private static void CreateBottomSeparator(Transform parent)
        {
            var line = UiFactory.CreateUIObject("BottomSeparator", parent);
            var rt = (RectTransform)line.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 1f);
            UiFactory.AddImage(line, AppTheme.Divider).raycastTarget = false;
            rt.SetAsLastSibling();
        }

        private static GameObject BuildTrackTime()
        {
            var root = UiFactory.CreateUIObject("TimelineTrackTime", null);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(600f, 40f);
            UiFactory.AddImage(root, AppTheme.TrackEven).raycastTarget = false;

            var gridGo = UiFactory.CreateUIObject("GridGraphic", root.transform);
            var gridRt = (RectTransform)gridGo.transform;
            UiFactory.Stretch(gridRt);
            gridRt.pivot = Vector2.zero;
            var grid = gridGo.AddComponent<TimelineTrackGridGraphic>();
            grid.raycastTarget = false;

            var lane = UiFactory.CreateUIObject("ColorKeyframeLane", root.transform);
            var laneRt = (RectTransform)lane.transform;
            laneRt.anchorMin = laneRt.anchorMax = new Vector2(0f, 1f);
            laneRt.pivot = new Vector2(0f, 1f);
            laneRt.anchoredPosition = new Vector2(0f, -5f);
            laneRt.sizeDelta = new Vector2(600f, 30f);
            UiFactory.AddImage(lane, new Color(0f, 0f, 0f, 0f)).raycastTarget = true;
            lane.AddComponent<TrackColorInput>();

            var gradientGo = UiFactory.CreateUIObject("ColorGradient", lane.transform);
            var gradientRt = (RectTransform)gradientGo.transform;
            gradientRt.anchorMin = gradientRt.anchorMax = new Vector2(0f, 0.5f);
            gradientRt.pivot = new Vector2(0f, 0.5f);
            gradientRt.anchoredPosition = Vector2.zero;
            gradientRt.sizeDelta = new Vector2(600f, 5f);
            var gradient = gradientGo.AddComponent<TimelineColorGradientGraphic>();
            gradient.raycastTarget = false;
            gradient.color = Color.white;

            CreateBottomSeparator(root.transform);
            return root;
        }

        private static GameObject BuildColorKeyframe()
        {
            var root = UiFactory.CreateUIObject("ColorKeyframe", null);
            var view = root.AddComponent<ColorKeyframeView>();
            view.BuildDefaultVisual();
            return root;
        }

        private static GameObject BuildRulerInteraction()
        {
            var root = UiFactory.CreateUIObject("TimelineRulerInteraction", null);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(600f, 32f);
            UiFactory.AddImage(root, new Color(0f, 0f, 0f, 0f)).raycastTarget = true;
            root.AddComponent<TimelineRulerInput>();

            var ticksGo = UiFactory.CreateUIObject("TicksGraphic", root.transform);
            var ticksRt = (RectTransform)ticksGo.transform;
            UiFactory.Stretch(ticksRt);
            ticksRt.pivot = Vector2.zero;
            var ticks = ticksGo.AddComponent<TimelineRulerTicksGraphic>();
            ticks.raycastTarget = false;
            return root;
        }

        private static GameObject BuildRulerLabel()
        {
            var root = UiFactory.CreateUIObject("TimelineRulerLabel", null);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(78f, 20f);
            var label = UiFactory.AddText(root, "0s", AppTheme.TimelineScaleSize, TextAnchor.MiddleCenter);
            label.color = AppTheme.TextSecondary;
            label.raycastTarget = false;
            return root;
        }

        private static GameObject BuildPlayhead(bool ruler)
        {
            var root = UiFactory.CreateUIObject(ruler ? "TimelineRulerPlayhead" : "TimelinePlayhead", null);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(12f, ruler ? 32f : 40f);
            UiFactory.AddImage(root, new Color(0f, 0f, 0f, 0f)).raycastTarget = ruler;
            if (ruler) root.AddComponent<PlayheadDragInput>();

            var line = UiFactory.CreateUIObject("Line", root.transform);
            var lineRt = (RectTransform)line.transform;
            lineRt.anchorMin = new Vector2(0.5f, 0f);
            lineRt.anchorMax = new Vector2(0.5f, 1f);
            lineRt.pivot = new Vector2(0.5f, 0.5f);
            lineRt.sizeDelta = new Vector2(2f, 0f);
            UiFactory.AddImage(line, AppTheme.Playhead).raycastTarget = false;

            if (ruler)
            {
                var head = UiFactory.CreateUIObject("Head", root.transform);
                var headRt = (RectTransform)head.transform;
                headRt.anchorMin = headRt.anchorMax = new Vector2(0.5f, 1f);
                headRt.pivot = new Vector2(0.5f, 0.5f);
                headRt.anchoredPosition = new Vector2(0f, -5f);
                headRt.sizeDelta = new Vector2(10f, 10f);
                headRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
                UiFactory.AddImage(head, AppTheme.Playhead).raycastTarget = false;
            }
            return root;
        }

        private static GameObject BuildPreviewLight()
        {
            var root = UiFactory.CreateUIObject("PreviewLight", null);
            var view = root.AddComponent<PreviewLightView>();
            view.EnsureVisual();
            return root;
        }

        private static GameObject BuildMenuPopup()
        {
            var root = UiFactory.CreateUIObject("RuntimeMenuPopup", null);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(210f, 40f);
            UiFactory.AddImage(root, AppTheme.Elevated);
            var layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.spacing = 0f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var view = root.AddComponent<RuntimeMenuPopupView>();
            view.EditorConfigure(rt);
            return root;
        }

        private static GameObject BuildMenuItem()
        {
            var root = UiFactory.CreateUIObject("RuntimeMenuItem", null);
            var image = UiFactory.AddImage(root, AppTheme.ButtonColors(AppButtonStyle.Secondary).normalColor);
            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            button.colors = AppTheme.ButtonColors(AppButtonStyle.Secondary);
            var layout = root.AddComponent<LayoutElement>();
            layout.preferredHeight = 28f;
            layout.minHeight = 28f;
            layout.flexibleWidth = 1f;

            var textGo = UiFactory.CreateUIObject("Text", root.transform);
            UiFactory.Stretch((RectTransform)textGo.transform);
            var label = UiFactory.AddText(textGo, "Menu Item", AppTheme.BodySize, TextAnchor.MiddleLeft);
            label.alignment = TextAlignmentOptions.Left;
            label.margin = new Vector4(8f, 0f, 4f, 0f);
            label.raycastTarget = false;

            var separator = UiFactory.CreateUIObject("Separator", root.transform);
            var separatorRt = (RectTransform)separator.transform;
            separatorRt.anchorMin = new Vector2(0f, 0.5f);
            separatorRt.anchorMax = new Vector2(1f, 0.5f);
            separatorRt.offsetMin = new Vector2(3f, -0.5f);
            separatorRt.offsetMax = new Vector2(-3f, 0.5f);
            UiFactory.AddImage(separator, AppTheme.Divider).raycastTarget = false;
            separator.SetActive(false);

            var view = root.AddComponent<RuntimeMenuItemView>();
            view.EditorConfigure(image, button, label, layout, separator);
            return root;
        }

        private static GameObject BuildPresetSwatch()
        {
            var root = UiFactory.CreateUIObject("ColorPresetSwatch", null);
            var layout = root.AddComponent<LayoutElement>();
            layout.preferredWidth = 44f;
            layout.preferredHeight = 24f;
            layout.minWidth = 44f;
            layout.minHeight = 24f;
            var image = UiFactory.AddImage(root, Color.white);
            var button = root.AddComponent<Button>();
            button.targetGraphic = image;
            var rightClick = root.AddComponent<PresetSwatchRightClick>();
            var outline = root.AddComponent<Outline>();
            outline.effectColor = AppTheme.TextSecondary;
            outline.effectDistance = new Vector2(1f, 1f);
            var view = root.AddComponent<RuntimePresetSwatchView>();
            view.EditorConfigure(image, button, rightClick);
            return root;
        }

        private static GameObject BuildColorPicker()
        {
            var root = UiFactory.CreateUIObject("HsvColorPicker", null);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(340f, 474f);
            UiFactory.AddImage(root, AppTheme.Elevated);

            var titleGo = UiFactory.CreateUIObject("Title", root.transform);
            var titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -5f);
            titleRt.sizeDelta = new Vector2(-10f, 24f);
            var title = UiFactory.AddText(titleGo, "Color Picker", AppTheme.SectionTitleSize, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyles.Bold;
            title.raycastTarget = false;

            var pickerRoot = UiFactory.CreateUIObject("Picker", root.transform);
            var pickerRt = (RectTransform)pickerRoot.transform;
            pickerRt.anchorMin = pickerRt.anchorMax = new Vector2(0.5f, 1f);
            pickerRt.pivot = new Vector2(0.5f, 1f);
            pickerRt.anchoredPosition = new Vector2(0f, -32f);
            pickerRt.sizeDelta = new Vector2(220f, 220f);

            var ringGo = UiFactory.CreateUIObject("HueRing", pickerRoot.transform);
            var ringRt = (RectTransform)ringGo.transform;
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            ringRt.sizeDelta = new Vector2(214f, 214f);
            var ring = ringGo.AddComponent<RawImage>();

            var svGo = UiFactory.CreateUIObject("SVSquare", pickerRoot.transform);
            var svRt = (RectTransform)svGo.transform;
            svRt.anchorMin = svRt.anchorMax = new Vector2(0.5f, 0.5f);
            svRt.sizeDelta = new Vector2(96f, 96f);
            var sv = svGo.AddComponent<RawImage>();

            var hueMarker = CreateMarker("HueMarker", pickerRoot.transform, 12f);
            var svMarker = CreateMarker("SVMarker", svGo.transform, 11f);

            var previewRow = CreatePickerRow(root.transform, "PreviewRow", -258f);
            UiFactory.CreateLabel(previewRow, "Current", 54f);
            var previewGo = UiFactory.CreateUIObject("Preview", previewRow);
            var previewLayout = previewGo.AddComponent<LayoutElement>();
            previewLayout.preferredWidth = 46f;
            previewLayout.preferredHeight = 24f;
            var preview = UiFactory.AddImage(previewGo, Color.white);
            preview.raycastTarget = false;

            var rgbRow = CreatePickerRow(root.transform, "RgbRow", -294f);
            UiFactory.CreateLabel(rgbRow, "RGB", 36f);
            UiFactory.CreateLabel(rgbRow, "R", 14f);
            var r = UiFactory.CreateInput(rgbRow, string.Empty, 55f);
            UiFactory.CreateLabel(rgbRow, "G", 14f);
            var g = UiFactory.CreateInput(rgbRow, string.Empty, 55f);
            UiFactory.CreateLabel(rgbRow, "B", 14f);
            var b = UiFactory.CreateInput(rgbRow, string.Empty, 55f);

            var hsvRow = CreatePickerRow(root.transform, "HsvRow", -330f);
            UiFactory.CreateLabel(hsvRow, "HSV", 36f);
            UiFactory.CreateLabel(hsvRow, "H", 14f);
            var h = UiFactory.CreateInput(hsvRow, string.Empty, 55f);
            UiFactory.CreateLabel(hsvRow, "S", 14f);
            var s = UiFactory.CreateInput(hsvRow, string.Empty, 55f);
            UiFactory.CreateLabel(hsvRow, "V", 14f);
            var v = UiFactory.CreateInput(hsvRow, string.Empty, 55f);
            r.contentType = g.contentType = b.contentType = TMP_InputField.ContentType.DecimalNumber;
            h.contentType = s.contentType = v.contentType = TMP_InputField.ContentType.DecimalNumber;

            var presetHeader = CreatePickerRow(root.transform, "PresetHeader", -366f);
            UiFactory.CreateLabel(presetHeader, "Presets", 62f);
            var spacer = UiFactory.CreateUIObject("PresetSpacer", presetHeader);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var savePreset = UiFactory.CreateButton(presetHeader, "Save Preset", null, 104f);

            var presetRows = new Transform[2];
            presetRows[0] = CreatePresetRow(root.transform, "PresetRow1", -400f);
            presetRows[1] = CreatePresetRow(root.transform, "PresetRow2", -434f);

            var control = pickerRoot.AddComponent<HsvColorPickerControl>();
            var hueInput = ringGo.AddComponent<HsvHueRingInput>();
            var svInput = svGo.AddComponent<HsvSvSquareInput>();
            var view = root.AddComponent<RuntimeColorPickerView>();
            view.EditorConfigure(
                pickerRt, ring, sv, hueMarker, svMarker, preview,
                r, g, b, h, s, v, savePreset, presetRows,
                control, hueInput, svInput);
            return root;
        }

        private static RectTransform CreateMarker(string name, Transform parent, float size)
        {
            var go = UiFactory.CreateUIObject(name, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var image = UiFactory.AddImage(go, new Color(1f, 1f, 1f, 0.95f));
            image.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1.5f, 1.5f);
            return rt;
        }

        private static Transform CreatePickerRow(Transform parent, string name, float yFromTop)
        {
            var row = UiFactory.CreateRow(parent, 32f);
            row.gameObject.name = name;
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, yFromTop);
            row.sizeDelta = new Vector2(316f, 32f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(3, 3, 1, 1);
            layout.spacing = 3f;
            return row;
        }

        private static Transform CreatePresetRow(Transform parent, string name, float yFromTop)
        {
            var row = UiFactory.CreateRow(parent, 30f);
            row.gameObject.name = name;
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, yFromTop);
            row.sizeDelta = new Vector2(316f, 30f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(3, 3, 2, 2);
            layout.spacing = 5f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return row;
        }

        private static GameObject BuildDialog()
        {
            var root = UiFactory.CreateUIObject("RuntimeDialog", null);
            var rootRt = (RectTransform)root.transform;
            UiFactory.Stretch(rootRt);
            UiFactory.AddImage(root, new Color(0f, 0f, 0f, 0.6f));

            var panel = UiFactory.CreateUIObject("Panel", root.transform);
            var panelRt = (RectTransform)panel.transform;
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(520f, 176f);
            UiFactory.AddImage(panel, AppTheme.Panel);

            var messageGo = UiFactory.CreateUIObject("Message", panel.transform);
            var messageRt = (RectTransform)messageGo.transform;
            messageRt.anchorMin = new Vector2(0f, 0.42f);
            messageRt.anchorMax = new Vector2(1f, 1f);
            messageRt.offsetMin = new Vector2(18f, 0f);
            messageRt.offsetMax = new Vector2(-18f, -12f);
            var message = UiFactory.AddText(messageGo, string.Empty, 14, TextAnchor.MiddleCenter);
            message.enableWordWrapping = true;
            message.overflowMode = TextOverflowModes.Overflow;

            var row = UiFactory.CreateUIObject("ButtonRow", panel.transform);
            var rowRt = (RectTransform)row.transform;
            rowRt.anchorMin = new Vector2(0f, 0f);
            rowRt.anchorMax = new Vector2(1f, 0f);
            rowRt.pivot = new Vector2(0.5f, 0f);
            rowRt.anchoredPosition = new Vector2(0f, 18f);
            rowRt.sizeDelta = new Vector2(-36f, 34f);
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var buttons = new Button[3];
            var labels = new TMP_Text[3];
            for (var i = 0; i < 3; i++)
            {
                buttons[i] = UiFactory.CreateButton(row.transform, "Action", null, 130f);
                buttons[i].gameObject.name = "Action" + (i + 1);
                var buttonRt = (RectTransform)buttons[i].transform;
                buttonRt.sizeDelta = new Vector2(130f, 32f);
                labels[i] = buttons[i].GetComponentInChildren<TMP_Text>(true);
            }

            var view = root.AddComponent<RuntimeDialogView>();
            view.EditorConfigure(message, buttons, labels);
            return root;
        }
    }
}
#endif
