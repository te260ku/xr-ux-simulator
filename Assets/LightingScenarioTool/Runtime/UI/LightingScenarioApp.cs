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

        private Canvas _canvas;
        private RectTransform _overlay;
        private RuntimePopup _popup;
        private PreviewPanel _preview;
        private TimelinePanel _timeline;

        private TMP_InputField _scenarioNameInput;
        private TMP_InputField _durationInput;
        private TMP_Text _projectStateText;
        private TMP_Text _timeText;
        private TMP_Text _statusText;
        private Toggle _loopToggle;
        private Toggle _snapToggle;

        private GameObject _selectionInspectorContent;
        private TMP_InputField _keyframeTimeInput;
        private Button _colorSwatchButton;
        private Image _colorSwatchImage;

        private bool _isPlaying;
        private bool _buildingUi;
        private string _primarySelectedColorKeyframeId;

        public ScenarioDocument Document { get; } = new ScenarioDocument();
        public string SelectedUnitId { get; private set; }
        public HashSet<string> SelectedColorKeyframeIds { get; } = new HashSet<string>();
        public float CurrentTime => Document.Data.editorSettings.currentTime;
        public float PreviewLightSize => Mathf.Clamp(Document.Data.editorSettings.previewLightSize <= 0f ? 54f : Document.Data.editorSettings.previewLightSize, 20f, 120f);

        private void Awake()
        {
            UiFactory.EnsureEventSystem();
            Document.Changed += OnDocumentChanged;
            BuildUi();
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

        private void BuildUi()
        {
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
            UiFactory.AddImage(background, new Color(0.055f, 0.055f, 0.055f, 1f));

            const float menuHeight = 30f;
            const float projectHeight = 38f;
            const float toolbarHeight = 38f;
            const float inspectorHeight = 40f;
            const float gap = 1f;
            var y = 0f;

            var menuBar = CreateTopBar("MenuBar", background.transform, y, menuHeight, new Color(0.075f, 0.075f, 0.075f, 1f));
            BuildMenuBar(menuBar.transform);
            y += menuHeight + gap;

            var projectBar = CreateTopBar("ProjectScenarioBar", background.transform, y, projectHeight, new Color(0.095f, 0.095f, 0.095f, 1f));
            BuildProjectScenarioBar(projectBar.transform);
            y += projectHeight + gap;

            var toolbar = CreateTopBar("TimelineToolbar", background.transform, y, toolbarHeight, new Color(0.085f, 0.085f, 0.085f, 1f));
            BuildTimelineToolbar(toolbar.transform);
            y += toolbarHeight + gap;

            var inspector = CreateTopBar("SelectionInspector", background.transform, y, inspectorHeight, new Color(0.10f, 0.10f, 0.10f, 1f));
            BuildSelectionInspector(inspector.transform);
            y += inspectorHeight + gap;

            var workspace = UiFactory.CreateUIObject("Workspace", background.transform);
            var workspaceRt = (RectTransform)workspace.transform;
            workspaceRt.anchorMin = Vector2.zero;
            workspaceRt.anchorMax = Vector2.one;
            workspaceRt.offsetMin = new Vector2(8f, 8f);
            workspaceRt.offsetMax = new Vector2(-8f, -y - 6f);
            var workspaceLayout = workspace.AddComponent<HorizontalLayoutGroup>();
            workspaceLayout.spacing = 8f;
            workspaceLayout.childControlWidth = true;
            workspaceLayout.childControlHeight = true;
            workspaceLayout.childForceExpandWidth = true;
            workspaceLayout.childForceExpandHeight = true;

            var timelineGo = UiFactory.CreateUIObject("TimelineArea", workspace.transform);
            UiFactory.AddImage(timelineGo, new Color(0.08f, 0.08f, 0.08f, 1f));
            var timelineLayout = timelineGo.AddComponent<LayoutElement>();
            timelineLayout.minWidth = 420f;
            timelineLayout.flexibleWidth = 2f;
            _timeline = timelineGo.AddComponent<TimelinePanel>();
            _timeline.Initialize(this);

            var previewGo = UiFactory.CreateUIObject("PreviewArea", workspace.transform);
            UiFactory.AddImage(previewGo, new Color(0.035f, 0.035f, 0.035f, 1f));
            var previewLayout = previewGo.AddComponent<LayoutElement>();
            previewLayout.minWidth = 320f;
            previewLayout.flexibleWidth = 1f;
            _preview = previewGo.AddComponent<PreviewPanel>();
            _preview.Initialize(this);

            var overlayGo = UiFactory.CreateUIObject("Overlay", transform);
            _overlay = (RectTransform)overlayGo.transform;
            UiFactory.Stretch(_overlay);
            _popup = new RuntimePopup(_overlay);
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
            return bar;
        }

        private void BuildMenuBar(Transform parent)
        {
            var row = UiFactory.CreateRow(parent, 30f);
            UiFactory.Stretch(row);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 1, 1);
            layout.spacing = 2f;

            Button fileButton = null;
            fileButton = UiFactory.CreateButton(row, "File", () => OpenFileMenu((RectTransform)fileButton.transform), 54f);
            Button viewButton = null;
            viewButton = UiFactory.CreateButton(row, "View", () => OpenViewMenu((RectTransform)viewButton.transform), 54f);
            Button helpButton = null;
            helpButton = UiFactory.CreateButton(row, "Help", () => OpenHelpMenu((RectTransform)helpButton.transform), 54f);

            var spacer = UiFactory.CreateUIObject("MenuSpacer", row);
            var spacerLayout = spacer.AddComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1f;
            spacerLayout.minWidth = 8f;

            _statusText = UiFactory.CreateLabel(row, string.Empty, 320f);
            var statusLayout = _statusText.GetComponent<LayoutElement>();
            statusLayout.minWidth = 80f;
            statusLayout.flexibleWidth = 1f;
            _statusText.alignment = TextAlignmentOptions.Right;
            _statusText.overflowMode = TextOverflowModes.Ellipsis;

            UiFactory.CreateButton(row, "Exit", RequestExit, 58f);
        }

        private void BuildProjectScenarioBar(Transform parent)
        {
            var row = UiFactory.CreateRow(parent, 38f);
            UiFactory.Stretch(row);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 3, 3);
            layout.spacing = 6f;

            UiFactory.CreateLabel(row, "Scenario", 62f);
            _scenarioNameInput = UiFactory.CreateInput(row, "New Scenario", 190f);
            _scenarioNameInput.onEndEdit.AddListener(SetScenarioName);

            UiFactory.CreateLabel(row, "Duration", 62f);
            _durationInput = UiFactory.CreateInput(row, "10.000", 78f);
            _durationInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            _durationInput.onEndEdit.AddListener(SetScenarioDuration);
            UiFactory.CreateLabel(row, "s", 16f);

            UiFactory.CreateLabel(row, "Project", 52f);
            _projectStateText = UiFactory.CreateLabel(row, "Untitled", 220f);
            var projectLayout = _projectStateText.GetComponent<LayoutElement>();
            projectLayout.minWidth = 100f;
            projectLayout.flexibleWidth = 1f;
            _projectStateText.overflowMode = TextOverflowModes.Ellipsis;
        }

        private void BuildTimelineToolbar(Transform parent)
        {
            var row = UiFactory.CreateRow(parent, 38f);
            UiFactory.Stretch(row);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 3, 3);
            layout.spacing = 5f;

            UiFactory.CreateButton(row, "|<", JumpToStart, 42f);
            UiFactory.CreateButton(row, "Play", Play, 58f);
            UiFactory.CreateButton(row, "Pause", Pause, 62f);
            UiFactory.CreateButton(row, "Stop", Stop, 58f);
            UiFactory.CreateButton(row, ">|", JumpToEnd, 42f);
            _timeText = UiFactory.CreateLabel(row, "0.000 / 10.000 s", 150f);

            AddVerticalSeparator(row);
            _loopToggle = UiFactory.CreateToggle(row, "Loop", false);
            _loopToggle.onValueChanged.AddListener(v =>
            {
                if (!_buildingUi) Document.Execute(d => d.editorSettings.loop = v);
            });
            _snapToggle = UiFactory.CreateToggle(row, "Snap", true);
            _snapToggle.onValueChanged.AddListener(v =>
            {
                if (!_buildingUi) Document.Execute(d => d.editorSettings.snapEnabled = v);
            });

            var spacer = UiFactory.CreateUIObject("ToolbarSpacer", row);
            var spacerLayout = spacer.AddComponent<LayoutElement>();
            spacerLayout.flexibleWidth = 1f;
            spacerLayout.minWidth = 8f;

            UiFactory.CreateButton(row, "Zoom -", ZoomOut, 68f);
            UiFactory.CreateButton(row, "Zoom +", ZoomIn, 68f);
        }

        private static void AddVerticalSeparator(Transform parent)
        {
            var go = UiFactory.CreateUIObject("Separator", parent);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 1f;
            le.preferredHeight = 24f;
            UiFactory.AddImage(go, new Color(0.28f, 0.28f, 0.28f, 1f)).raycastTarget = false;
        }

        private void BuildSelectionInspector(Transform parent)
        {
            _selectionInspectorContent = UiFactory.CreateUIObject("ColorKeyframeInspector", parent);
            var rt = (RectTransform)_selectionInspectorContent.transform;
            UiFactory.Stretch(rt);
            var layout = _selectionInspectorContent.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var title = UiFactory.CreateLabel(_selectionInspectorContent.transform, "Color Keyframe", 106f);
            title.fontStyle = FontStyles.Bold;
            UiFactory.CreateLabel(_selectionInspectorContent.transform, "Time", 34f);
            _keyframeTimeInput = UiFactory.CreateInput(_selectionInspectorContent.transform, string.Empty, 78f);
            _keyframeTimeInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            _keyframeTimeInput.onEndEdit.AddListener(SetSelectedColorKeyframeTimeFromText);
            UiFactory.CreateLabel(_selectionInspectorContent.transform, "s", 16f);
            UiFactory.CreateLabel(_selectionInspectorContent.transform, "Color", 38f);

            var swatchGo = UiFactory.CreateUIObject("ColorSwatch", _selectionInspectorContent.transform);
            var swatchLayout = swatchGo.AddComponent<LayoutElement>();
            swatchLayout.preferredWidth = 32f;
            swatchLayout.preferredHeight = 28f;
            _colorSwatchImage = UiFactory.AddImage(swatchGo, Color.black);
            _colorSwatchButton = swatchGo.AddComponent<Button>();
            _colorSwatchButton.targetGraphic = _colorSwatchImage;
            var outline = swatchGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            outline.effectDistance = new Vector2(1f, 1f);
            _colorSwatchButton.onClick.AddListener(OpenHsvColorPicker);

            var spacer = UiFactory.CreateUIObject("InspectorSpacer", _selectionInspectorContent.transform);
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
            _selectionInspectorContent.SetActive(false);
        }

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
            Document.Data.editorSettings.currentTime = Mathf.Clamp(time, 0f, Document.Data.metadata.duration);
            RefreshTimeLabel();
            _timeline?.RefreshPlayhead();
            _preview?.RefreshColors();
        }

        public void RefreshPreview() => _preview?.RefreshColors();

        public void SetPreviewLightSizeFromUi(float value)
        {
            if (_buildingUi) return;
            var clamped = Mathf.Clamp(value, 20f, 120f);
            Document.Data.editorSettings.previewLightSize = clamped;
            Document.MarkDirtyWithoutNotification();
            RefreshProjectStateDisplay();
            _preview?.RefreshLightSizes();
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

            var selectedUnitIds = SelectedColorKeyframeIds
                .Select(id => Document.FindUnitForColorKeyframe(id)?.unitId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();
            SelectedUnitId = selectedUnitIds.Count == 1 ? selectedUnitIds[0] : null;

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

            SelectedUnitId = unitId;
            if (refreshTimeline) _timeline?.RefreshSelection();
            _preview?.RefreshSelection();
            RefreshInspector();
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
                25f,
                400f));

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
                SetStatus("Saved: " + Path.GetFileName(resolved), false);
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
                SetStatus("Loaded: " + Path.GetFileName(resolved), false);
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
            if (_projectStateText != null)
                _projectStateText.text = GetProjectDisplayName();
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
            if (!hasSelection)
            {
                _buildingUi = false;
                return;
            }

            var allEditable = selectedUnits.Count == selectedKeys.Count && selectedUnits.All(u => !u.track.locked);
            var single = selectedKeys.Count == 1 ? selectedKeys[0] : null;
            _keyframeTimeInput.interactable = single != null && allEditable;
            _keyframeTimeInput.SetTextWithoutNotify(single != null ? single.time.ToString("0.000") : string.Empty);
            _colorSwatchButton.interactable = allEditable;

            if (AllSameColor(selectedKeys, out var commonColor))
                _colorSwatchImage.color = commonColor;
            else
                _colorSwatchImage.color = new Color(0.35f, 0.35f, 0.35f, 1f);

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
            const float epsilon = 0.0001f;
            var before = Document.CaptureState();
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

            foreach (var plan in plans)
                plan.Key.track.colorKeyframes.Add(plan.Value);
            foreach (var unit in plans.Select(x => x.Key).Distinct())
                unit.track.colorKeyframes.Sort((a, b) => a.time.CompareTo(b.time));

            Document.CommitExternalEdit(before);
            SelectedColorKeyframeIds.Clear();
            foreach (var plan in plans) SelectedColorKeyframeIds.Add(plan.Value.keyframeId);
            _primarySelectedColorKeyframeId = plans.Count > 0 ? plans[plans.Count - 1].Value.keyframeId : null;
            if (plans.Count > 0) SelectedUnitId = plans[plans.Count - 1].Key.unitId;
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

            if (!string.IsNullOrEmpty(_primarySelectedColorKeyframeId))
            {
                var unit = Document.FindUnitForColorKeyframe(_primarySelectedColorKeyframeId);
                if (unit != null) SelectedUnitId = unit.unitId;
            }
        }

        private void SetStatus(string message, bool isError)
        {
            if (_statusText == null) return;
            _statusText.text = message ?? string.Empty;
            _statusText.color = isError
                ? new Color(1f, 0.45f, 0.4f, 1f)
                : new Color(0.65f, 0.85f, 0.65f, 1f);
        }
    }
}
