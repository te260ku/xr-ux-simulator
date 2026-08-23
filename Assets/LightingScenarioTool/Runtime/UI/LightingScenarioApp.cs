using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace LightingScenarioTool
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    [RequireComponent(typeof(AppTheme))]
    public sealed class LightingScenarioApp : MonoBehaviour
    {
        private sealed class KeyframeClipboardItem
        {
            public string unitId;
            public float time;
            public SerializableColor color;
        }

        private enum ClipboardKind
        {
            None,
            ColorKeyframes,
            LightingUnit
        }

        private readonly JsonScenarioRepository _repository = new JsonScenarioRepository();
        private readonly IScenarioExporter _exporter = new DummyBinaryScenarioExporter();
        private readonly List<KeyframeClipboardItem> _clipboard = new List<KeyframeClipboardItem>();
        private ClipboardKind _clipboardKind;
        private string _lightingUnitClipboardJson;
        private int _lightingUnitPasteCount;

        [SerializeField] private AppTheme _theme;
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _overlay;
        [SerializeField] private LightingScenarioUiPrefabCatalog _uiPrefabs;
        private RuntimePopup _popup;
        [SerializeField] private PreviewPanel _preview;
        [SerializeField] private TimelinePanel _timeline;

        [SerializeField] private TMP_InputField _scenarioNameInput;
        [SerializeField] private TMP_InputField _durationInput;
        [SerializeField] private TMP_Text _projectStateText;
        [SerializeField] private TMP_Text _timeText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button _fullscreenButton;
        [SerializeField] private Toggle _loopToggle;
        [SerializeField] private Toggle _snapToggle;

        [SerializeField] private GameObject _selectionInspectorContent;
        [SerializeField] private TMP_InputField _keyframeTimeInput;
        [SerializeField] private Button _colorSwatchButton;
        [SerializeField] private Image _colorSwatchImage;

        private bool _isPlaying;
        private bool _buildingUi;
        private string _primarySelectedColorKeyframeId;
        private int _windowedWidth;
        private int _windowedHeight;

        public ScenarioDocument Document { get; } = new ScenarioDocument();
        public string SelectedUnitId { get; private set; }
        public HashSet<string> SelectedColorKeyframeIds { get; } = new HashSet<string>();
        public float CurrentTime => Document.Data.editorSettings.currentTime;
        public float PreviewLightSize => Mathf.Clamp(
            Document.Data.editorSettings.previewLightSize <= 0f
                ? ScenarioDataUtility.DefaultPreviewLightSize
                : Document.Data.editorSettings.previewLightSize,
            ScenarioDataUtility.MinPreviewLightSize,
            ScenarioDataUtility.MaxPreviewLightSize);
        public bool ShowPreviewUnitNames => !Document.Data.editorSettings.hidePreviewUnitNames;
        internal LightingScenarioUiPrefabCatalog UiPrefabs => _uiPrefabs;

        private void Awake()
        {
            _theme ??= GetComponent<AppTheme>();
            AppTheme.SetActive(_theme);

            if (EventSystem.current == null)
                Debug.LogError("Lighting Scenario Tool requires an EventSystem in the scene. Use Tools > Lighting Scenario > Build Complete UI In Current Scene.", this);

            if (Screen.fullScreenMode == FullScreenMode.Windowed)
            {
                _windowedWidth = Screen.width;
                _windowedHeight = Screen.height;
            }

            Document.Changed += OnDocumentChanged;
            if (!BindExistingUi())
            {
                Debug.LogError(
                    "Lighting Scenario Tool UI hierarchy is missing. " +
                    "Run Tools > Lighting Scenario > Build Complete UI In Current Scene once in the Editor to generate the complete UI hierarchy.",
                    this);
                enabled = false;
                return;
            }

            if (_uiPrefabs == null || !_uiPrefabs.IsComplete)
            {
                Debug.LogError(
                    "Lighting Scenario runtime UI prefabs are missing or incomplete. " +
                    "Run Tools > Lighting Scenario > Build Complete UI In Current Scene in Edit Mode.", this);
                enabled = false;
                return;
            }

            BindUiEvents();
            _popup = new RuntimePopup(_overlay, _uiPrefabs);
            _timeline.Initialize(this);
            _preview.Initialize(this);
            OnDocumentChanged();
        }

        private void OnDestroy()
        {
            Document.Changed -= OnDocumentChanged;
        }

        private void Update()
        {
            _popup?.Tick();
            HandleKeyboardShortcuts();
            if (!_isPlaying) return;

            var duration = Document.Data.metadata.duration;
            var t = CurrentTime + Time.unscaledDeltaTime;
            if (t >= duration)
            {
                if (Document.Data.editorSettings.loop && duration > 0f) t %= duration;
                else { t = duration; _isPlaying = false; }
            }
            SetCurrentTime(t);
        }

        private bool BindExistingUi()
        {
            _theme ??= GetComponent<AppTheme>();
            AppTheme.SetActive(_theme);
            _canvas ??= GetComponent<Canvas>();

            // Prefer serialized scene references. This makes the authored UI independent of
            // the exact hierarchy path after a designer moves or renames objects.
            _overlay ??= FindDescendant<RectTransform>(transform, "Overlay");
            _timeline ??= GetComponentInChildren<TimelinePanel>(true);
            _preview ??= GetComponentInChildren<PreviewPanel>(true);

            var background = FindDescendant(transform, "Background");
            var menuBar = FindDescendant(transform, "MenuBar");
            var scenarioHeader = FindDescendant(transform, "ScenarioHeader");
            var toolbar = FindDescendant(transform, "TimelineToolbar");
            var inspector = FindDescendant(transform, "ColorKeyframeInspector");

            if (_scenarioNameInput == null && scenarioHeader != null)
                _scenarioNameInput = FindComponent<TMP_InputField>(scenarioHeader, "Row/ScenarioGroup/Input")
                    ?? FindDescendant<TMP_InputField>(scenarioHeader, "Input");

            if (_durationInput == null && scenarioHeader != null)
            {
                var durationGroup = FindDescendant(scenarioHeader, "DurationGroup");
                _durationInput = durationGroup != null ? FindDescendant<TMP_InputField>(durationGroup, "Input") : null;
            }

            if (_projectStateText == null)
            {
                var t = FindDescendant(transform, "ProjectStateText");
                _projectStateText = t != null ? t.GetComponent<TMP_Text>() : null;
            }
            if (_statusText == null)
            {
                var t = FindDescendant(transform, "StatusText");
                _statusText = t != null ? t.GetComponent<TMP_Text>() : null;
            }
            if (_fullscreenButton == null)
            {
                var t = FindDescendant(transform, "Button_Fullscreen");
                _fullscreenButton = t != null ? t.GetComponent<Button>() : null;
            }
            if (_timeText == null)
            {
                var t = FindDescendant(transform, "CurrentTimeText");
                _timeText = t != null ? t.GetComponent<TMP_Text>() : null;
            }
            if (_loopToggle == null)
            {
                var t = FindDescendant(transform, "Toggle_Loop");
                _loopToggle = t != null ? t.GetComponent<Toggle>() : null;
            }
            if (_snapToggle == null)
            {
                var t = FindDescendant(transform, "Toggle_Snap");
                _snapToggle = t != null ? t.GetComponent<Toggle>() : null;
            }

            if (_selectionInspectorContent == null && inspector != null)
                _selectionInspectorContent = inspector.gameObject;

            if (_keyframeTimeInput == null && inspector != null)
            {
                var t = FindDescendant(inspector, "Input");
                _keyframeTimeInput = t != null ? t.GetComponent<TMP_InputField>() : null;
            }
            if (_colorSwatchButton == null && inspector != null)
            {
                var t = FindDescendant(inspector, "ColorSwatch");
                _colorSwatchButton = t != null ? t.GetComponent<Button>() : null;
            }
            _colorSwatchImage ??= _colorSwatchButton != null ? _colorSwatchButton.GetComponent<Image>() : null;

            return _canvas != null && _overlay != null && _timeline != null && _preview != null &&
                   _scenarioNameInput != null && _durationInput != null && _projectStateText != null &&
                   _timeText != null && _loopToggle != null && _snapToggle != null &&
                   _selectionInspectorContent != null && _keyframeTimeInput != null &&
                   _colorSwatchButton != null && _colorSwatchImage != null;
        }

        private void BindUiEvents()
        {
            _scenarioNameInput.onEndEdit.RemoveListener(SetScenarioName);
            _scenarioNameInput.onEndEdit.AddListener(SetScenarioName);
            _durationInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            _durationInput.onEndEdit.RemoveListener(SetScenarioDuration);
            _durationInput.onEndEdit.AddListener(SetScenarioDuration);

            _loopToggle.onValueChanged.RemoveListener(OnLoopChanged);
            _loopToggle.onValueChanged.AddListener(OnLoopChanged);
            _snapToggle.onValueChanged.RemoveListener(OnSnapChanged);
            _snapToggle.onValueChanged.AddListener(OnSnapChanged);

            _keyframeTimeInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            _keyframeTimeInput.onEndEdit.RemoveListener(SetSelectedColorKeyframeTimeFromText);
            _keyframeTimeInput.onEndEdit.AddListener(SetSelectedColorKeyframeTimeFromText);
            _colorSwatchButton.onClick.RemoveListener(OpenHsvColorPicker);
            _colorSwatchButton.onClick.AddListener(OpenHsvColorPicker);

            BindButton("Background/MenuBar/Row/Button_File", button => button.onClick.AddListener(() => OpenFileMenu((RectTransform)button.transform)));
            BindButton("Background/MenuBar/Row/Button_View", button => button.onClick.AddListener(() => OpenViewMenu((RectTransform)button.transform)));
            BindButton("Background/MenuBar/Row/Button_Help", button => button.onClick.AddListener(() => OpenHelpMenu((RectTransform)button.transform)));
            BindButton("Background/MenuBar/Row/Button_Fullscreen", button =>
            {
                _fullscreenButton = button;
                button.onClick.AddListener(ToggleFullscreenWindowMode);
            });
            BindButton("Background/MenuBar/Row/Button_Exit", button => button.onClick.AddListener(RequestExit));

            BindButton("Background/TimelineToolbar/Row/Button_|<", button => button.onClick.AddListener(JumpToStart));
            BindButton("Background/TimelineToolbar/Row/Button_Play", button => button.onClick.AddListener(Play));
            BindButton("Background/TimelineToolbar/Row/Button_Pause", button => button.onClick.AddListener(Pause));
            BindButton("Background/TimelineToolbar/Row/Button_Stop", button => button.onClick.AddListener(Stop));
            BindButton("Background/TimelineToolbar/Row/Button_>|", button => button.onClick.AddListener(JumpToEnd));
        }

        private void BindButton(string path, Action<Button> binder)
        {
            var t = transform.Find(path);
            if (t == null)
            {
                var slash = path.LastIndexOf('/');
                var leafName = slash >= 0 ? path.Substring(slash + 1) : path;
                t = FindDescendant(transform, leafName);
            }
            if (t == null) return;
            var button = t.GetComponent<Button>();
            if (button == null) return;
            binder(button);
        }

        private void OnLoopChanged(bool value)
        {
            if (!_buildingUi) Document.Execute(d => d.editorSettings.loop = value);
        }

        private void OnSnapChanged(bool value)
        {
            if (!_buildingUi) Document.Execute(d => d.editorSettings.snapEnabled = value);
        }

        private static T FindComponent<T>(Transform root, string path) where T : Component
        {
            if (root == null) return null;
            var t = string.IsNullOrEmpty(path) ? root : root.Find(path);
            return t != null ? t.GetComponent<T>() : null;
        }


        private static Transform FindDescendant(Transform root, string exactName)
        {
            if (root == null) return null;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child != root && child.name == exactName) return child;
            }
            return null;
        }

        private static T FindDescendant<T>(Transform root, string exactName) where T : Component
        {
            var t = FindDescendant(root, exactName);
            return t != null ? t.GetComponent<T>() : null;
        }

        private static T FindNthComponent<T>(Transform root, int oneBasedIndex) where T : Component
        {
            if (root == null) return null;
            var components = root.GetComponentsInChildren<T>(true);
            return oneBasedIndex > 0 && oneBasedIndex <= components.Length ? components[oneBasedIndex - 1] : null;
        }


#if UNITY_EDITOR
        public void EditorSetUiPrefabCatalog(LightingScenarioUiPrefabCatalog catalog)
        {
            _uiPrefabs = catalog;
            UnityEditor.EditorUtility.SetDirty(this);
        }

        public void EditorBuildCompleteUiHierarchy()
        {
            EnsureThemeComponent();

            // The editor exposes a single deterministic build command. Remove every authored
            // child under the application root and regenerate the entire default UI, including
            // panel scripts, interaction helpers, and serialized references. Runtime widgets live in generated Prefab Assets.
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child != null) UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            ClearUiReferencesForEditorRebuild();
            BuildUi();
        }

        private void ClearUiReferencesForEditorRebuild()
        {
            _overlay = null;
            _preview = null;
            _timeline = null;
            _scenarioNameInput = null;
            _durationInput = null;
            _projectStateText = null;
            _timeText = null;
            _statusText = null;
            _fullscreenButton = null;
            _loopToggle = null;
            _snapToggle = null;
            _selectionInspectorContent = null;
            _keyframeTimeInput = null;
            _colorSwatchButton = null;
            _colorSwatchImage = null;
        }


        private bool EnsureThemeComponent()
        {
            _theme ??= GetComponent<AppTheme>();
            if (_theme != null)
            {
                AppTheme.SetActive(_theme);
                return false;
            }

            _theme = gameObject.AddComponent<AppTheme>();
            AppTheme.SetActive(_theme);
            return true;
        }


#endif

#if UNITY_EDITOR
        private void BuildUi()
        {
            _theme ??= GetComponent<AppTheme>();
            AppTheme.SetActive(_theme);
            _buildingUi = true;
            _canvas = GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.pixelPerfect = true;

            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            var background = UiFactory.CreateUIObject("Background", transform);
            UiFactory.Stretch((RectTransform)background.transform);
            UiFactory.AddImage(background, AppTheme.Background);

            const float menuHeight = 32f;
            const float scenarioHeight = 72f;
            const float toolbarHeight = 46f;
            const float keyframeEditorHeight = 58f;
            const float sectionGap = 0f;
            var y = 0f;

            var menuBar = CreateTopBar("MenuBar", background.transform, y, menuHeight, AppTheme.Panel);
            BuildMenuBar(menuBar.transform);
            y += menuHeight + sectionGap;

            var scenarioHeader = CreateTopBar("ScenarioHeader", background.transform, y, scenarioHeight, AppTheme.Panel);
            BuildProjectScenarioBar(scenarioHeader.transform);
            y += scenarioHeight + sectionGap;

            var toolbar = CreateTopBar("TimelineToolbar", background.transform, y, toolbarHeight, AppTheme.Panel);
            BuildTimelineToolbar(toolbar.transform);
            y += toolbarHeight + sectionGap;

            var keyframeEditor = CreateTopBar("KeyframeEditor", background.transform, y, keyframeEditorHeight, AppTheme.Panel);
            BuildSelectionInspector(keyframeEditor.transform);
            y += keyframeEditorHeight + sectionGap;

            var workspace = UiFactory.CreateUIObject("Workspace", background.transform);
            var workspaceRt = (RectTransform)workspace.transform;
            workspaceRt.anchorMin = Vector2.zero;
            workspaceRt.anchorMax = Vector2.one;
            // Major application areas should meet the screen edge directly. Spacing belongs
            // inside each area's content, not around the area itself.
            workspaceRt.offsetMin = Vector2.zero;
            workspaceRt.offsetMax = new Vector2(0f, -y);

            var workspaceLayout = workspace.AddComponent<HorizontalLayoutGroup>();
            workspaceLayout.spacing = 0f;
            workspaceLayout.childControlWidth = true;
            workspaceLayout.childControlHeight = true;
            workspaceLayout.childForceExpandWidth = true;
            workspaceLayout.childForceExpandHeight = true;

            var timelineGo = UiFactory.CreateUIObject("TimelineArea", workspace.transform);
            UiFactory.AddImage(timelineGo, AppTheme.Panel);
            var timelineLayout = timelineGo.AddComponent<LayoutElement>();
            timelineLayout.minWidth = 560f;
            timelineLayout.flexibleWidth = 2.2f;
            _timeline = timelineGo.AddComponent<TimelinePanel>();
            _timeline.EditorBuildDefaultHierarchy(this);

            var divider = UiFactory.CreateUIObject("PaneDivider", workspace.transform);
            var dividerLayout = divider.AddComponent<LayoutElement>();
            dividerLayout.minWidth = 1f;
            dividerLayout.preferredWidth = 1f;
            dividerLayout.flexibleWidth = 0f;
            UiFactory.AddImage(divider, AppTheme.Divider).raycastTarget = false;

            var previewGo = UiFactory.CreateUIObject("PreviewArea", workspace.transform);
            UiFactory.AddImage(previewGo, AppTheme.Panel);
            var previewLayout = previewGo.AddComponent<LayoutElement>();
            previewLayout.minWidth = 440f;
            previewLayout.flexibleWidth = 1f;
            _preview = previewGo.AddComponent<PreviewPanel>();
            _preview.EditorBuildDefaultHierarchy(this);

            var overlayGo = UiFactory.CreateUIObject("Overlay", transform);
            _overlay = (RectTransform)overlayGo.transform;
            UiFactory.Stretch(_overlay);
            _buildingUi = false;
        }

        private static GameObject CreateTopBar(string name, Transform parent, float yFromTop, float height, Color color)
        {
            var bar = UiFactory.CreateUIObject(name, parent);
            var rt = (RectTransform)bar.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -yFromTop);
            rt.sizeDelta = new Vector2(0f, height);
            UiFactory.AddImage(bar, color).raycastTarget = false;
            CreateBottomDivider(bar.transform);
            return bar;
        }

        private static void CreateBottomDivider(Transform parent)
        {
            var line = UiFactory.CreateUIObject("BottomDivider", parent);
            var rt = (RectTransform)line.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 1f);
            UiFactory.AddImage(line, AppTheme.Divider).raycastTarget = false;
        }

        private void BuildMenuBar(Transform parent)
        {
            var row = UiFactory.CreateRow(parent, 32f);
            UiFactory.Stretch(row);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 0, 0);
            layout.spacing = 2f;

            UiFactory.CreateButton(row, "File", null, 52f, AppButtonStyle.Menu);
            UiFactory.CreateButton(row, "View", null, 52f, AppButtonStyle.Menu);
            UiFactory.CreateButton(row, "Help", null, 52f, AppButtonStyle.Menu);

            var spacer = UiFactory.CreateUIObject("MenuSpacer", row);
            var spacerLayout = spacer.AddComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1f;
            spacerLayout.minWidth = AppTheme.SpacingL;

            _fullscreenButton = UiFactory.CreateButton(row, string.Empty, null, 92f, AppButtonStyle.Secondary);
            _fullscreenButton.gameObject.name = "Button_Fullscreen";
            UiFactory.CreateButton(row, "Exit", null, 58f, AppButtonStyle.Secondary);
        }

        private static Transform CreateHeaderGroup(Transform parent, string label, float width, bool flexible = false)
        {
            var group = UiFactory.CreateUIObject(label + "Group", parent);
            var groupLayoutElement = group.AddComponent<LayoutElement>();
            groupLayoutElement.preferredWidth = width;
            groupLayoutElement.minWidth = Mathf.Min(width, 100f);
            groupLayoutElement.flexibleWidth = flexible ? 1f : 0f;

            var layout = group.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 4, 4);
            layout.spacing = 2f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = UiFactory.CreateSecondaryLabel(group.transform, label, width);
            var titleLayout = title.GetComponent<LayoutElement>();
            titleLayout.preferredHeight = 18f;
            titleLayout.minHeight = 18f;
            return group.transform;
        }

        private static RectTransform CreateInlineRow(Transform parent, float height = AppTheme.ControlHeight)
        {
            var row = UiFactory.CreateRow(parent, height);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = AppTheme.SpacingS;
            var element = row.GetComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            return row;
        }

        private void BuildProjectScenarioBar(Transform parent)
        {
            var row = UiFactory.CreateRow(parent, 72f);
            UiFactory.Stretch(row);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 2, 2);
            layout.spacing = AppTheme.SpacingL;
            layout.childAlignment = TextAnchor.MiddleLeft;

            var scenarioGroup = CreateHeaderGroup(row, "Scenario", 310f);
            _scenarioNameInput = UiFactory.CreateInput(scenarioGroup, "New Scenario", 310f);

            var durationGroup = CreateHeaderGroup(row, "Duration", 150f);
            var durationRow = CreateInlineRow(durationGroup);
            _durationInput = UiFactory.CreateInput(durationRow, "10.000", 105f);
            _durationInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            UiFactory.CreateSecondaryLabel(durationRow, "s", 20f);

            var projectGroup = CreateHeaderGroup(row, "Project", 300f, true);
            _projectStateText = UiFactory.CreateLabel(projectGroup, "Untitled", 300f);
            _projectStateText.gameObject.name = "ProjectStateText";
            var projectLayout = _projectStateText.GetComponent<LayoutElement>();
            projectLayout.minWidth = 120f;
            projectLayout.flexibleWidth = 1f;
            projectLayout.preferredHeight = 26f;
            _projectStateText.overflowMode = TextOverflowModes.Ellipsis;

            _statusText = UiFactory.CreateSecondaryLabel(projectGroup, string.Empty, 300f);
            _statusText.gameObject.name = "StatusText";
            var statusLayout = _statusText.GetComponent<LayoutElement>();
            statusLayout.preferredHeight = 16f;
            statusLayout.minHeight = 16f;
            statusLayout.flexibleWidth = 1f;
            _statusText.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void BuildTimelineToolbar(Transform parent)
        {
            var row = UiFactory.CreateRow(parent, 46f);
            UiFactory.Stretch(row);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 7, 7);
            layout.spacing = AppTheme.SpacingXs;
            layout.childAlignment = TextAnchor.MiddleLeft;

            UiFactory.CreateButton(row, "|<", null, 34f, AppButtonStyle.Icon);
            UiFactory.CreateButton(row, "Play", null, 58f, AppButtonStyle.Primary);
            UiFactory.CreateButton(row, "Pause", null, 62f, AppButtonStyle.Secondary);
            UiFactory.CreateButton(row, "Stop", null, 58f, AppButtonStyle.Secondary);
            UiFactory.CreateButton(row, ">|", null, 34f, AppButtonStyle.Icon);

            var transportGap = UiFactory.CreateUIObject("TransportGap", row);
            transportGap.AddComponent<LayoutElement>().preferredWidth = AppTheme.SpacingM;

            _timeText = UiFactory.CreateSecondaryLabel(row, "0.000 / 10.000 s", 150f);
            _timeText.gameObject.name = "CurrentTimeText";
            _timeText.alignment = TextAlignmentOptions.Center;

            var optionGap = UiFactory.CreateUIObject("OptionGap", row);
            optionGap.AddComponent<LayoutElement>().preferredWidth = AppTheme.SpacingL;

            _loopToggle = UiFactory.CreateToggle(row, "Loop", false);
            _snapToggle = UiFactory.CreateToggle(row, "Snap", true);

            var spacer = UiFactory.CreateUIObject("ToolbarSpacer", row);
            var spacerLayout = spacer.AddComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1f;
            spacerLayout.minWidth = AppTheme.SpacingL;
        }


        private void BuildSelectionInspector(Transform parent)
        {
            _selectionInspectorContent = UiFactory.CreateUIObject("ColorKeyframeInspector", parent);
            var rt = (RectTransform)_selectionInspectorContent.transform;
            UiFactory.Stretch(rt);
            var layout = _selectionInspectorContent.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.spacing = AppTheme.SpacingM;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;

            UiFactory.CreateSectionTitle(_selectionInspectorContent.transform, "Keyframe", 86f);
            UiFactory.CreateSecondaryLabel(_selectionInspectorContent.transform, "Time", 32f);
            _keyframeTimeInput = UiFactory.CreateInput(_selectionInspectorContent.transform, string.Empty, 96f);
            _keyframeTimeInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            UiFactory.CreateSecondaryLabel(_selectionInspectorContent.transform, "s", 16f);

            var groupGap = UiFactory.CreateUIObject("ColorGap", _selectionInspectorContent.transform);
            groupGap.AddComponent<LayoutElement>().preferredWidth = AppTheme.SpacingS;

            UiFactory.CreateSecondaryLabel(_selectionInspectorContent.transform, "Color", 40f);
            var swatchGo = UiFactory.CreateUIObject("ColorSwatch", _selectionInspectorContent.transform);
            var swatchLayout = swatchGo.AddComponent<LayoutElement>();
            swatchLayout.preferredWidth = 36f;
            swatchLayout.preferredHeight = 32f;
            _colorSwatchImage = UiFactory.AddImage(swatchGo, Color.black);
            _colorSwatchButton = swatchGo.AddComponent<Button>();
            _colorSwatchButton.targetGraphic = _colorSwatchImage;
            _colorSwatchButton.transition = Selectable.Transition.None;
            var outline = swatchGo.AddComponent<Outline>();
            outline.effectColor = AppTheme.Divider;
            outline.effectDistance = new Vector2(1f, -1f);
            var spacer = UiFactory.CreateUIObject("InspectorSpacer", _selectionInspectorContent.transform);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
            _keyframeTimeInput.interactable = false;
            _colorSwatchButton.interactable = false;
        }

#endif

        private void OpenFileMenu(RectTransform anchor)
        {
            _popup.ShowMenu(GetPopupAnchorScreenPosition(anchor), new[]
            {
                RuntimeMenuItem.Command("New", RequestNew),
                RuntimeMenuItem.Command("Open...", OpenProject),
                RuntimeMenuItem.Command("Save", Save),
                RuntimeMenuItem.Command("Save As...", SaveAs),
                RuntimeMenuItem.Separator(),
                RuntimeMenuItem.Command("Export...", Export),
                RuntimeMenuItem.Separator(),
                RuntimeMenuItem.Command("Exit", RequestExit)
            });
        }

        private void OpenViewMenu(RectTransform anchor)
        {
            _popup.ShowMenu(GetPopupAnchorScreenPosition(anchor), new[]
            {
                RuntimeMenuItem.Command("Zoom In", ZoomIn),
                RuntimeMenuItem.Command("Zoom Out", ZoomOut)
            });
        }

        private void OpenHelpMenu(RectTransform anchor)
        {
            _popup.ShowMenu(GetPopupAnchorScreenPosition(anchor), new[]
            {
                RuntimeMenuItem.Command("About", () => SetStatus("Lighting Scenario Tool", false))
            });
        }

        private Vector2 GetPopupAnchorScreenPosition(RectTransform anchor)
        {
            if (anchor == null) return ShortcutInput.PointerPosition;
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            var camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            return RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        }

        private void OnDocumentChanged()
        {
            if (_buildingUi) return;
            CleanupSelection();
            RefreshGlobalControls();
            _timeline?.Rebuild();
            _preview?.Rebuild();
            RefreshInspector();
        }

        private void RefreshGlobalControls()
        {
            _buildingUi = true;
            _scenarioNameInput?.SetTextWithoutNotify(Document.Data.metadata.scenarioName);
            _durationInput?.SetTextWithoutNotify(Document.Data.metadata.duration.ToString("0.000"));
            RefreshProjectStateDisplay();
            if (_loopToggle != null) _loopToggle.SetIsOnWithoutNotify(Document.Data.editorSettings.loop);
            if (_snapToggle != null) _snapToggle.SetIsOnWithoutNotify(Document.Data.editorSettings.snapEnabled);
            RefreshTimeLabel();
            _buildingUi = false;
        }

        private void RefreshTimeLabel()
        {
            if (_timeText != null)
                _timeText.text = $"{CurrentTime:0.000} / {Document.Data.metadata.duration:0.000} s";
        }

        public void SetCurrentTime(float time)
        {
            Document.SetCurrentTimeTransient(time);
            RefreshTimeLabel();
            _timeline?.RefreshPlayhead();
            _preview?.RefreshColors();
        }

        public void RefreshPreview() => _preview?.RefreshColors();

        public void SetPreviewLightSizeFromUi(float value)
        {
            if (_buildingUi) return;
            var clamped = Mathf.Clamp(value, ScenarioDataUtility.MinPreviewLightSize, ScenarioDataUtility.MaxPreviewLightSize);
            Document.SetPreviewLightSizeNoHistory(clamped);
            Document.MarkDirtyWithoutNotification();
            RefreshProjectStateDisplay();
            _preview?.RefreshLightSizes();
        }

        public void SetPreviewUnitNamesVisible(bool visible)
        {
            if (_buildingUi) return;
            Document.Execute(d => d.editorSettings.hidePreviewUnitNames = !visible);
        }

        public void RefreshTimelineGeometry()
        {
            _timeline?.RefreshGeometry();
            _preview?.RefreshColors();
            RefreshInspector();
        }

        public void SelectUnit(string unitId)
        {
            SelectedUnitId = unitId;
            SelectedColorKeyframeIds.Clear();
            _primarySelectedColorKeyframeId = null;
            _preview?.RefreshSelection();
            _timeline?.RefreshSelection();
            RefreshInspector();
        }

        public void ClearSelection()
        {
            SelectedUnitId = null;
            SelectedColorKeyframeIds.Clear();
            _primarySelectedColorKeyframeId = null;
            _preview?.RefreshSelection();
            _timeline?.RefreshSelection();
            RefreshInspector();
        }

        public bool IsColorKeyframeSelected(string keyframeId) =>
            !string.IsNullOrEmpty(keyframeId) && SelectedColorKeyframeIds.Contains(keyframeId);

        public void SetColorKeyframeSelection(IEnumerable<string> keyframeIds, bool additive)
        {
            var validIds = (keyframeIds ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrEmpty(id) && Document.FindColorKeyframe(id) != null)
                .Distinct()
                .ToList();

            if (!additive) SelectedColorKeyframeIds.Clear();
            foreach (var id in validIds) SelectedColorKeyframeIds.Add(id);

            if (validIds.Count > 0)
                _primarySelectedColorKeyframeId = validIds[validIds.Count - 1];
            else if (!additive || SelectedColorKeyframeIds.Count == 0)
                _primarySelectedColorKeyframeId = SelectedColorKeyframeIds.FirstOrDefault();

            UpdateSelectedUnitFromKeyframeSelection();

            _timeline?.RefreshSelection();
            _preview?.RefreshSelection();
            RefreshInspector();
        }

        public void SelectColorKeyframe(
            string unitId,
            string keyframeId,
            bool additive = false,
            bool refreshTimeline = true)
        {
            if (Document.FindColorKeyframe(unitId, keyframeId) == null) return;

            if (!additive)
            {
                SelectedColorKeyframeIds.Clear();
                SelectedColorKeyframeIds.Add(keyframeId);
                _primarySelectedColorKeyframeId = keyframeId;
            }
            else if (SelectedColorKeyframeIds.Contains(keyframeId))
            {
                SelectedColorKeyframeIds.Remove(keyframeId);
                if (_primarySelectedColorKeyframeId == keyframeId)
                    _primarySelectedColorKeyframeId = SelectedColorKeyframeIds.FirstOrDefault();
            }
            else
            {
                SelectedColorKeyframeIds.Add(keyframeId);
                _primarySelectedColorKeyframeId = keyframeId;
            }

            UpdateSelectedUnitFromKeyframeSelection();
            if (refreshTimeline) _timeline?.RefreshSelection();
            _preview?.RefreshSelection();
            RefreshInspector();
        }

        private void UpdateSelectedUnitFromKeyframeSelection()
        {
            var selectedUnitIds = SelectedColorKeyframeIds
                .Select(id => Document.FindUnitForColorKeyframe(id)?.unitId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .ToList();

            SelectedUnitId = selectedUnitIds.Count == 1 ? selectedUnitIds[0] : null;
        }

        public void CreateColorKeyframe(string unitId, float rawTime)
        {
            var time = Document.SnapColorKeyframeTime(rawTime, (IEnumerable<string>)null);
            var keyframe = Document.AddColorKeyframe(unitId, time, out var error);
            if (keyframe == null)
            {
                SetStatus(error ?? "Color keyframe could not be added.", true);
                return;
            }
            SelectColorKeyframe(unitId, keyframe.keyframeId);
        }

        public bool SetColorKeyframeTimesNoHistory(IDictionary<string, float> times, out string error)
        {
            var result = Document.TrySetColorKeyframeTimesNoHistory(times, out error);
            if (!result && !string.IsNullOrEmpty(error)) SetStatus(error, true);
            return result;
        }

        public void CommitExternalEdit(string before) => Document.CommitExternalEdit(before);

        public void OpenColorPickerForSelection(RectTransform anchor)
        {
            if (anchor == null || SelectedColorKeyframeIds.Count == 0) return;
            var id = !string.IsNullOrEmpty(_primarySelectedColorKeyframeId)
                ? _primarySelectedColorKeyframeId
                : SelectedColorKeyframeIds.First();
            var keyframe = Document.FindColorKeyframe(id);
            if (keyframe == null) return;

            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            var camera = _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            var screenPosition = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            _popup.ShowHsvColorPicker(screenPosition, keyframe.color.ToUnityColor(), ApplyHsvColor);
        }

        public void ReportStatus(string message, bool isError) => SetStatus(message, isError);
        public void ShowContext(Vector2 screenPosition, string label, Action action) =>
            _popup.ShowContext(screenPosition, label, action);

        public void RequestDeleteUnit(string unitId)
        {
            var unit = Document.FindUnit(unitId);
            if (unit == null) return;
            Action delete = () =>
            {
                Document.DeleteUnit(unitId);
                if (SelectedUnitId == unitId) ClearSelection();
            };

            if (unit.track.colorKeyframes.Count > 0)
                _popup.ShowConfirm("This lighting unit contains color keyframes. Delete the unit and its track?", delete);
            else
                delete();
        }

        public void DeleteSelectedColorKeyframes()
        {
            if (SelectedColorKeyframeIds.Count == 0) return;
            var ids = SelectedColorKeyframeIds.ToArray();
            if (!Document.DeleteColorKeyframes(ids, out var error))
            {
                SetStatus(error, true);
                return;
            }
            SelectedColorKeyframeIds.Clear();
            _primarySelectedColorKeyframeId = null;
            RefreshInspector();
        }

        public void SetUnitName(string unitId, string value)
        {
            if (string.IsNullOrEmpty(unitId)) return;
            var text = string.IsNullOrWhiteSpace(value) ? unitId : value.Trim();
            Document.Execute(d =>
            {
                var unit = d.lightingUnits.FirstOrDefault(x => x.unitId == unitId);
                if (unit != null) unit.displayName = text;
            });
        }

        public void SetTrackLocked(string unitId, bool value) => Document.Execute(d =>
        {
            var u = d.lightingUnits.FirstOrDefault(x => x.unitId == unitId);
            if (u != null) u.track.locked = value;
        });

        public void SetTrackMuted(string unitId, bool value) => Document.Execute(d =>
        {
            var u = d.lightingUnits.FirstOrDefault(x => x.unitId == unitId);
            if (u != null) u.track.muted = value;
        });

        public void MoveTrack(string unitId, int direction) => Document.Execute(d =>
        {
            var i = d.lightingUnits.FindIndex(x => x.unitId == unitId);
            var target = i + direction;
            if (i < 0 || target < 0 || target >= d.lightingUnits.Count) return;
            var u = d.lightingUnits[i];
            d.lightingUnits.RemoveAt(i);
            d.lightingUnits.Insert(target, u);
        });

        public void ZoomIn() => ChangeZoom(1.25f);
        public void ZoomOut() => ChangeZoom(0.8f);
        private void ChangeZoom(float factor) => Document.Execute(d =>
            d.editorSettings.pixelsPerSecond = Mathf.Clamp(
                d.editorSettings.pixelsPerSecond * factor,
                ScenarioDataUtility.MinPixelsPerSecond,
                ScenarioDataUtility.MaxPixelsPerSecond));

        private void JumpToStart() => SetCurrentTime(0f);
        private void JumpToEnd() { _isPlaying = false; SetCurrentTime(Document.Data.metadata.duration); }
        private void Play()
        {
            if (Document.Data.metadata.duration <= 0f) return;
            if (CurrentTime >= Document.Data.metadata.duration - 0.0001f) SetCurrentTime(0f);
            _isPlaying = true;
        }
        private void Pause() => _isPlaying = false;
        private void Stop() { _isPlaying = false; SetCurrentTime(0f); }
        private void TogglePlayPause() { if (_isPlaying) Pause(); else Play(); }

        private void HandleKeyboardShortcuts()
        {
            if (IsTextInputFocused()) return;
            if (_popup != null && _popup.IsOpen)
            {
                if (ShortcutInput.EscapePressedThisFrame) _popup.Close();
                return;
            }

            var ctrl = ShortcutInput.CtrlPressed;
            var shift = ShortcutInput.ShiftPressed;

            // Project commands. Keep these ahead of editing shortcuts so the
            // standard project shortcuts always resolve unambiguously.
            if (ctrl && !shift && ShortcutInput.NPressedThisFrame) { RequestNew(); return; }
            if (ctrl && !shift && ShortcutInput.OPressedThisFrame) { OpenProject(); return; }
            if (ctrl && !shift && ShortcutInput.SPressedThisFrame) { Save(); return; }

            if (ctrl && ShortcutInput.ZPressedThisFrame)
            {
                if (shift) Document.Redo(); else Document.Undo();
                return;
            }
            if (ctrl && ShortcutInput.CPressedThisFrame) { CopySelected(); return; }
            if (ctrl && ShortcutInput.VPressedThisFrame)
            {
                // For Lighting Units, Ctrl+V creates a clean unit (no Lock/Mute/Keyframes),
                // while Ctrl+Shift+V pastes the complete copied unit.
                Paste(shift);
                return;
            }
            if (ctrl && ShortcutInput.DPressedThisFrame) { Duplicate(); return; }
            if (ShortcutInput.DeletePressedThisFrame) { DeleteSelection(); return; }
            if (ShortcutInput.HomePressedThisFrame) { JumpToStart(); return; }
            if (ShortcutInput.EndPressedThisFrame) { JumpToEnd(); return; }
            if (ShortcutInput.SpacePressedThisFrame)
            {
                if (shift) Stop(); else TogglePlayPause();
                return;
            }
            if (ShortcutInput.EscapePressedThisFrame) Stop();
        }

        private static bool IsTextInputFocused()
        {
            var selected = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;
            var input = selected != null ? selected.GetComponentInParent<TMP_InputField>() : null;
            return input != null && input.isFocused;
        }

        private void RequestNew()
        {
            RequestUnsavedChangesAction("create a new project", NewNow);
        }

        private void NewNow()
        {
            _isPlaying = false;
            SelectedUnitId = null;
            SelectedColorKeyframeIds.Clear();
            _primarySelectedColorKeyframeId = null;
            ClearClipboard();
            Document.NewDocument();
            SetStatus("New project created.", false);
        }

        private void OpenProject()
        {
            RequestUnsavedChangesAction("open another project", PickAndLoadProject);
        }

        private void PickAndLoadProject()
        {
            if (!ProjectFilePicker.TryPickOpenProjectFile(GetPickerInitialPath(), out var path))
            {
                ReportFilePickerErrorIfAny();
                return;
            }
            try
            {
                LoadNow(_repository.ResolvePath(path));
            }
            catch (Exception ex)
            {
                SetStatus("Open failed: " + ex.Message, true);
            }
        }

        public void BrowsePreviewBackgroundImage()
        {
            var current = Document.Data.editorSettings.previewBackgroundImagePath;
            if (!ProjectFilePicker.TryPickOpenImageFile(current, out var path))
            {
                ReportFilePickerErrorIfAny();
                return;
            }
            Document.Execute(d => d.editorSettings.previewBackgroundImagePath = path);
            SetStatus("Preview background: " + Path.GetFileName(path), false);
        }

        private void ReportFilePickerErrorIfAny()
        {
            if (!string.IsNullOrWhiteSpace(ProjectFilePicker.LastErrorMessage))
                SetStatus(ProjectFilePicker.LastErrorMessage, true);
        }

        private void SaveAs()
        {
            TrySaveAs();
        }

        private bool TrySaveAs()
        {
            var initial = !string.IsNullOrWhiteSpace(Document.CurrentProjectPath)
                ? Document.CurrentProjectPath
                : GetPickerInitialPath();
            if (!ProjectFilePicker.TryPickSaveProjectFile(initial, out var path))
            {
                ReportFilePickerErrorIfAny();
                return false;
            }
            return TrySaveToPath(path);
        }

        private void Save()
        {
            TrySaveCurrentProject();
        }

        private bool TrySaveCurrentProject()
        {
            if (string.IsNullOrWhiteSpace(Document.CurrentProjectPath))
                return TrySaveAs();

            return TrySaveToPath(Document.CurrentProjectPath);
        }

        private bool TrySaveToPath(string path)
        {
            try
            {
                var resolved = _repository.ResolvePath(path);
                _repository.Save(resolved, Document.Data);
                Document.MarkSaved(resolved);
                SetStatus("Project saved.", false);
                return true;
            }
            catch (Exception ex)
            {
                SetStatus("Save failed: " + ex.Message, true);
                return false;
            }
        }

        private void LoadNow(string path)
        {
            try
            {
                _isPlaying = false;
                var resolved = _repository.ResolvePath(path);
                var data = _repository.Load(resolved);
                SelectedUnitId = null;
                SelectedColorKeyframeIds.Clear();
                _primarySelectedColorKeyframeId = null;
                ClearClipboard();
                Document.LoadDocument(data, resolved);
                SetStatus("Project opened.", false);
            }
            catch (Exception ex)
            {
                SetStatus("Load failed: " + ex.Message, true);
            }
        }

        private void Export()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Document.CurrentProjectPath))
                {
                    SetStatus("Save the project before exporting.", true);
                    return;
                }

                var json = _repository.ResolvePath(Document.CurrentProjectPath);
                var dir = Path.GetDirectoryName(json);
                if (string.IsNullOrEmpty(dir))
                    throw new InvalidOperationException("Export directory could not be determined.");
                var path = Path.Combine(dir, Path.GetFileNameWithoutExtension(json) + ".bin");
                _exporter.Export(path, Document.Data);
                SetStatus("Dummy export: " + Path.GetFileName(path), false);
            }
            catch (Exception ex)
            {
                SetStatus("Export failed: " + ex.Message, true);
            }
        }

        private void ToggleFullscreenWindowMode()
        {
            var enterFullscreen = Screen.fullScreenMode == FullScreenMode.Windowed;
            if (enterFullscreen)
            {
                _windowedWidth = Mathf.Max(640, Screen.width);
                _windowedHeight = Mathf.Max(360, Screen.height);
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            }
            else
            {
                var width = _windowedWidth > 0 ? _windowedWidth : 1280;
                var height = _windowedHeight > 0 ? _windowedHeight : 720;
                Screen.SetResolution(width, height, FullScreenMode.Windowed);
            }
        }

        private void RequestExit()
        {
            RequestUnsavedChangesAction("exit the application", QuitNow);
        }

        private void RequestUnsavedChangesAction(string actionName, Action continueAction)
        {
            if (!Document.IsDirty)
            {
                continueAction?.Invoke();
                return;
            }

            _popup.ShowSaveDiscardCancel(
                $"Unsaved changes exist. Save before you {actionName}?",
                () =>
                {
                    if (TrySaveCurrentProject()) continueAction?.Invoke();
                },
                () => continueAction?.Invoke());
        }

        private string GetPickerInitialPath()
        {
            return !string.IsNullOrWhiteSpace(Document.CurrentProjectPath)
                ? Document.CurrentProjectPath
                : null;
        }

        private string GetProjectDisplayName()
        {
            string name;
            if (string.IsNullOrWhiteSpace(Document.CurrentProjectPath))
            {
                name = "Untitled";
            }
            else
            {
                try { name = Path.GetFileName(Document.CurrentProjectPath); }
                catch { name = Document.CurrentProjectPath; }
                if (string.IsNullOrWhiteSpace(name)) name = "Untitled";
            }

            return Document.IsDirty ? name + " *" : name;
        }

        private void RefreshProjectStateDisplay()
        {
            var displayName = GetProjectDisplayName();
            if (_projectStateText != null) _projectStateText.text = displayName;
        }

        private static void QuitNow()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SetScenarioName(string value)
        {
            if (!_buildingUi)
                Document.Execute(d => d.metadata.scenarioName =
                    string.IsNullOrWhiteSpace(value) ? "Untitled" : value.Trim());
        }

        private void SetScenarioDuration(string value)
        {
            if (_buildingUi) return;
            if (!float.TryParse(value, out var duration) || duration <= 0f)
            {
                SetStatus("Scenario length must be greater than 0.", true);
                RefreshGlobalControls();
                return;
            }

            var latest = Document.Data.lightingUnits
                .SelectMany(x => x.track.colorKeyframes)
                .Select(x => x.time)
                .DefaultIfEmpty(0f)
                .Max();
            if (duration < latest - 0.0001f)
            {
                SetStatus($"Scenario length cannot be shorter than the latest keyframe ({latest:0.###}s).", true);
                RefreshGlobalControls();
                return;
            }
            Document.Execute(d => d.metadata.duration = duration);
        }


        private void RefreshInspector()
        {
            if (_selectionInspectorContent == null || _keyframeTimeInput == null || _colorSwatchButton == null) return;
            _buildingUi = true;

            var selectedKeys = SelectedColorKeyframeIds
                .Select(id => Document.FindColorKeyframe(id))
                .Where(k => k != null)
                .ToList();
            var selectedUnits = SelectedColorKeyframeIds
                .Select(id => Document.FindUnitForColorKeyframe(id))
                .Where(u => u != null)
                .ToList();

            var hasSelection = selectedKeys.Count > 0;
            _selectionInspectorContent.SetActive(hasSelection);
            var allEditable = hasSelection && selectedUnits.Count == selectedKeys.Count && selectedUnits.All(u => !u.track.locked);
            var single = selectedKeys.Count == 1 ? selectedKeys[0] : null;

            _keyframeTimeInput.interactable = single != null && allEditable;
            _keyframeTimeInput.SetTextWithoutNotify(single != null ? single.time.ToString("0.000") : string.Empty);
            _colorSwatchButton.interactable = allEditable;

            if (hasSelection && AllSameColor(selectedKeys, out var commonColor))
                _colorSwatchImage.color = commonColor;
            else
                _colorSwatchImage.color = hasSelection ? new Color(0.35f, 0.35f, 0.37f, 1f) : new Color(0.18f, 0.18f, 0.19f, 1f);

            _buildingUi = false;
        }

        private static bool AllSameColor(IReadOnlyList<ColorKeyframeData> keys, out Color color)
        {
            color = Color.black;
            if (keys == null || keys.Count == 0) return false;
            color = keys[0].color.ToUnityColor();
            for (var i = 1; i < keys.Count; i++)
            {
                var c = keys[i].color.ToUnityColor();
                if (Mathf.Abs(c.r - color.r) > 0.0001f ||
                    Mathf.Abs(c.g - color.g) > 0.0001f ||
                    Mathf.Abs(c.b - color.b) > 0.0001f)
                    return false;
            }
            return true;
        }

        private void SetSelectedColorKeyframeTimeFromText(string text)
        {
            if (_buildingUi || SelectedColorKeyframeIds.Count != 1) return;
            var id = SelectedColorKeyframeIds.First();
            var unit = Document.FindUnitForColorKeyframe(id);
            if (unit == null) return;
            if (!float.TryParse(text, out var time))
            {
                SetStatus("Keyframe time is invalid.", true);
                RefreshInspector();
                return;
            }
            if (!Document.TrySetColorKeyframeTime(unit.unitId, id, time, out var error))
                SetStatus(error, true);
        }

        private void OpenHsvColorPicker()
        {
            if (_colorSwatchButton != null)
                OpenColorPickerForSelection((RectTransform)_colorSwatchButton.transform);
        }

        private void ApplyHsvColor(Color color)
        {
            if (SelectedColorKeyframeIds.Count == 0) return;
            if (!Document.TrySetColorKeyframesColor(SelectedColorKeyframeIds, color, out var error))
                SetStatus(error, true);
        }

        private void DeleteSelection()
        {
            if (SelectedColorKeyframeIds.Count > 0)
                DeleteSelectedColorKeyframes();
            else if (!string.IsNullOrEmpty(SelectedUnitId))
                RequestDeleteUnit(SelectedUnitId);
        }

        private void CopySelected()
        {
            // Color Keyframes take precedence when both a track/unit and keyframes are selected.
            if (SelectedColorKeyframeIds.Count > 0)
            {
                CopySelectedColorKeyframes();
                return;
            }

            if (!string.IsNullOrEmpty(SelectedUnitId))
            {
                var unit = Document.FindUnit(SelectedUnitId);
                if (unit != null)
                {
                    _lightingUnitClipboardJson = JsonUtility.ToJson(unit);
                    _lightingUnitPasteCount = 0;
                    _clipboard.Clear();
                    _clipboardKind = ClipboardKind.LightingUnit;
                    SetStatus($"Copied Lighting Unit: {unit.displayName}", false);
                    return;
                }
            }

            SetStatus("No Color Keyframe or Lighting Unit selected.", true);
        }

        private void CopySelectedColorKeyframes()
        {
            _clipboard.Clear();
            foreach (var id in SelectedColorKeyframeIds
                         .OrderBy(id => Document.FindColorKeyframe(id)?.time ?? 0f))
            {
                var key = Document.FindColorKeyframe(id);
                var unit = Document.FindUnitForColorKeyframe(id);
                if (key == null || unit == null) continue;
                _clipboard.Add(new KeyframeClipboardItem
                {
                    unitId = unit.unitId,
                    time = key.time,
                    color = key.color
                });
            }

            if (_clipboard.Count > 0)
            {
                _lightingUnitClipboardJson = null;
                _lightingUnitPasteCount = 0;
                _clipboardKind = ClipboardKind.ColorKeyframes;
                SetStatus($"Copied {_clipboard.Count} color keyframe(s).", false);
            }
            else
            {
                SetStatus("No color keyframes selected.", true);
            }
        }

        private void Paste(bool includeLightingUnitTrackData = false)
        {
            if (_clipboardKind == ClipboardKind.LightingUnit)
            {
                PasteLightingUnit(
                    includeLightingUnitTrackData ? "Paste With Data" : "Paste",
                    includeLightingUnitTrackData);
                return;
            }

            if (_clipboardKind != ClipboardKind.ColorKeyframes || _clipboard.Count == 0)
            {
                SetStatus("Clipboard is empty.", true);
                return;
            }

            var destinationUnitId = SelectedUnitId;
            if (string.IsNullOrEmpty(destinationUnitId) || Document.FindUnit(destinationUnitId) == null)
            {
                SetStatus("Select a destination track before pasting color keyframes.", true);
                return;
            }

            var earliest = _clipboard.Min(x => x.time);
            PasteClipboardWithDelta(CurrentTime - earliest, "Paste", destinationUnitId);
        }

        private void Duplicate()
        {
            if (SelectedColorKeyframeIds.Count > 0)
            {
                CopySelectedColorKeyframes();
                if (_clipboard.Count == 0) return;
                var step = ScenarioDocument.GetGridInterval(Document.Data.editorSettings.pixelsPerSecond);
                var max = _clipboard.Max(x => x.time);
                var min = _clipboard.Min(x => x.time);
                var delta = max + step <= Document.Data.metadata.duration ? step : -step;
                if (min + delta < 0f)
                {
                    SetStatus("Duplicate failed because there is no room for the copied keyframes.", true);
                    return;
                }
                // Duplicate keeps the selected keyframes on their original tracks.
                PasteClipboardWithDelta(delta, "Duplicate", null);
                return;
            }

            if (!string.IsNullOrEmpty(SelectedUnitId))
            {
                var source = Document.FindUnit(SelectedUnitId);
                if (source != null)
                {
                    DuplicateLightingUnit(source, "Duplicate", 1, true);
                    return;
                }
            }

            SetStatus("No Color Keyframe or Lighting Unit selected.", true);
        }

        private void PasteLightingUnit(string operationName, bool includeTrackData)
        {
            if (string.IsNullOrEmpty(_lightingUnitClipboardJson))
            {
                SetStatus("Lighting Unit clipboard is empty.", true);
                return;
            }

            LightingUnitData source;
            try
            {
                source = JsonUtility.FromJson<LightingUnitData>(_lightingUnitClipboardJson);
            }
            catch (Exception ex)
            {
                SetStatus("Lighting Unit paste failed: " + ex.Message, true);
                return;
            }

            if (source == null)
            {
                SetStatus("Lighting Unit paste failed because the copied data is invalid.", true);
                return;
            }

            _lightingUnitPasteCount++;
            DuplicateLightingUnit(source, operationName, _lightingUnitPasteCount, includeTrackData);
        }

        private void DuplicateLightingUnit(
            LightingUnitData source,
            string operationName,
            int offsetStep,
            bool includeTrackData)
        {
            if (source == null) return;

            const float offsetPerCopy = 0.035f;
            var offset = offsetPerCopy * Mathf.Max(1, offsetStep);
            var x = source.previewX + offset <= 1f
                ? source.previewX + offset
                : source.previewX - offset;
            var y = source.previewY - offset >= 0f
                ? source.previewY - offset
                : source.previewY + offset;
            var created = Document.DuplicateUnit(
                source,
                Mathf.Clamp01(x),
                Mathf.Clamp01(y),
                includeTrackData);
            if (created == null)
            {
                SetStatus(operationName + " Lighting Unit failed.", true);
                return;
            }

            SelectUnit(created.unitId);
            if (includeTrackData)
            {
                SetStatus(
                    $"{operationName} Lighting Unit: {created.displayName} (Lock/Mute/Color Keyframes included)",
                    false);
            }
            else
            {
                SetStatus(
                    $"{operationName} Lighting Unit: {created.displayName} (Lock/Mute/Color Keyframes cleared)",
                    false);
            }
        }

        private void PasteClipboardWithDelta(float delta, string operationName, string destinationUnitId)
        {
            const float epsilon = ScenarioDataUtility.TimeEpsilon;
            var plans = new List<KeyValuePair<LightingUnitData, ColorKeyframeData>>();
            var plannedTimesByUnit = new Dictionary<string, List<float>>();

            foreach (var source in _clipboard)
            {
                // Paste targets the currently selected track. Duplicate passes null and therefore
                // preserves each copied keyframe's original track.
                var targetUnitId = string.IsNullOrEmpty(destinationUnitId)
                    ? source.unitId
                    : destinationUnitId;
                var unit = Document.FindUnit(targetUnitId);
                if (unit == null)
                {
                    SetStatus(operationName + " failed because the destination track does not exist.", true);
                    return;
                }
                if (unit.track.locked)
                {
                    SetStatus(operationName + " failed because the destination track is locked.", true);
                    return;
                }

                var time = source.time + delta;
                if (time < -epsilon || time > Document.Data.metadata.duration + epsilon)
                {
                    SetStatus(operationName + " failed because a keyframe would be outside the scenario range.", true);
                    return;
                }

                if (unit.track.colorKeyframes.Any(k => Mathf.Abs(k.time - time) <= epsilon))
                {
                    SetStatus(operationName + " failed because a keyframe already exists at the destination time.", true);
                    return;
                }

                if (!plannedTimesByUnit.TryGetValue(unit.unitId, out var plannedTimes))
                {
                    plannedTimes = new List<float>();
                    plannedTimesByUnit[unit.unitId] = plannedTimes;
                }
                if (plannedTimes.Any(t => Mathf.Abs(t - time) <= epsilon))
                {
                    SetStatus(operationName + " failed because copied keyframes collide with each other.", true);
                    return;
                }
                plannedTimes.Add(time);

                plans.Add(new KeyValuePair<LightingUnitData, ColorKeyframeData>(
                    unit,
                    new ColorKeyframeData
                    {
                        keyframeId = Guid.NewGuid().ToString("N"),
                        time = Mathf.Clamp(time, 0f, Document.Data.metadata.duration),
                        color = source.color
                    }));
            }

            Document.Execute(_ =>
            {
                foreach (var plan in plans)
                    plan.Key.track.colorKeyframes.Add(plan.Value);
                foreach (var unit in plans.Select(x => x.Key).Distinct())
                    unit.track.colorKeyframes.Sort((a, b) => a.time.CompareTo(b.time));
            });
            SelectedColorKeyframeIds.Clear();
            foreach (var plan in plans) SelectedColorKeyframeIds.Add(plan.Value.keyframeId);
            _primarySelectedColorKeyframeId = plans.Count > 0 ? plans[plans.Count - 1].Value.keyframeId : null;
            UpdateSelectedUnitFromKeyframeSelection();
            _timeline?.RefreshSelection();
            _preview?.RefreshSelection();
            RefreshInspector();
            SetStatus($"{operationName}: {plans.Count} color keyframe(s).", false);
        }

        private void ClearClipboard()
        {
            _clipboard.Clear();
            _clipboardKind = ClipboardKind.None;
            _lightingUnitClipboardJson = null;
            _lightingUnitPasteCount = 0;
        }

        private void CleanupSelection()
        {
            if (!string.IsNullOrEmpty(SelectedUnitId) && Document.FindUnit(SelectedUnitId) == null)
                SelectedUnitId = null;

            SelectedColorKeyframeIds.RemoveWhere(id => Document.FindColorKeyframe(id) == null);
            if (!string.IsNullOrEmpty(_primarySelectedColorKeyframeId) &&
                !SelectedColorKeyframeIds.Contains(_primarySelectedColorKeyframeId))
                _primarySelectedColorKeyframeId = SelectedColorKeyframeIds.FirstOrDefault();

            if (SelectedColorKeyframeIds.Count > 0)
                UpdateSelectedUnitFromKeyframeSelection();
        }

        private void SetStatus(string message, bool isError)
        {
            if (_statusText == null) return;
            _statusText.text = message ?? string.Empty;
            _statusText.color = isError ? AppTheme.Error : AppTheme.TextSecondary;
        }
    }
}
