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

        private readonly JsonScenarioRepository _repository = new JsonScenarioRepository();
        private readonly IScenarioExporter _exporter = new DummyBinaryScenarioExporter();
        private readonly List<KeyframeClipboardItem> _clipboard = new List<KeyframeClipboardItem>();

        private Canvas _canvas;
        private RectTransform _overlay;
        private RuntimePopup _popup;
        private PreviewPanel _preview;
        private TimelinePanel _timeline;

        private TMP_InputField _scenarioNameInput;
        private TMP_InputField _durationInput;
        private TMP_InputField _pathInput;
        private TMP_Text _scenarioIdText;
        private TMP_Text _timeText;
        private TMP_Text _statusText;
        private Toggle _loopToggle;
        private Toggle _snapToggle;
        private Toggle _multiSelectToggle;
        private Button _undoButton;
        private Button _redoButton;

        private TMP_InputField _unitNameInput;
        private TMP_InputField _keyframeTimeInput;
        private TMP_InputField _colorR;
        private TMP_InputField _colorG;
        private TMP_InputField _colorB;
        private Button _colorPickerButton;
        private Image _colorPreview;

        private bool _isPlaying;
        private bool _buildingUi;
        private string _primarySelectedColorKeyframeId;

        public ScenarioDocument Document { get; } = new ScenarioDocument();
        public string SelectedUnitId { get; private set; }
        public HashSet<string> SelectedColorKeyframeIds { get; } = new HashSet<string>();
        public float CurrentTime => Document.Data.editorSettings.currentTime;
        public bool MultiSelectEnabled => _multiSelectToggle != null && _multiSelectToggle.isOn;

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

            var settings = UiFactory.CreateUIObject("SettingsArea", background.transform);
            var settingsRt = (RectTransform)settings.transform;
            settingsRt.anchorMin = new Vector2(0f, 1f);
            settingsRt.anchorMax = new Vector2(1f, 1f);
            settingsRt.pivot = new Vector2(0.5f, 1f);
            settingsRt.sizeDelta = new Vector2(0f, 138f);
            UiFactory.AddImage(settings, new Color(0.095f, 0.095f, 0.095f, 1f));
            var layout = settings.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.spacing = 4f;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            BuildScenarioRow(settings.transform);
            BuildPlaybackRow(settings.transform);
            BuildInspectorRow(settings.transform);

            var body = UiFactory.CreateUIObject("Body", background.transform);
            var bodyRt = (RectTransform)body.transform;
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(8f, 8f);
            bodyRt.offsetMax = new Vector2(-8f, -146f);
            var bodyLayout = body.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.spacing = 8f;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandHeight = true;

            var timelineGo = UiFactory.CreateUIObject("TimelineArea", body.transform);
            UiFactory.AddImage(timelineGo, new Color(0.08f, 0.08f, 0.08f, 1f));
            timelineGo.AddComponent<LayoutElement>().flexibleWidth = 1.85f;
            _timeline = timelineGo.AddComponent<TimelinePanel>();
            _timeline.Initialize(this);

            var previewGo = UiFactory.CreateUIObject("PreviewArea", body.transform);
            UiFactory.AddImage(previewGo, new Color(0.035f, 0.035f, 0.035f, 1f));
            previewGo.AddComponent<LayoutElement>().flexibleWidth = 1f;
            _preview = previewGo.AddComponent<PreviewPanel>();
            _preview.Initialize(this);

            var titleGo = UiFactory.CreateUIObject("PreviewTitle", previewGo.transform);
            var titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(0f, 28f);
            UiFactory.AddText(
                titleGo,
                "Preview  (right-click empty area to add a light)",
                12,
                TextAnchor.MiddleCenter).raycastTarget = false;

            var overlayGo = UiFactory.CreateUIObject("Overlay", transform);
            _overlay = (RectTransform)overlayGo.transform;
            UiFactory.Stretch(_overlay);
            _popup = new RuntimePopup(_overlay);
            _buildingUi = false;
        }

        private void BuildScenarioRow(Transform parent)
        {
            var row = UiFactory.CreateRow(parent);
            UiFactory.CreateLabel(row, "Scenario", 52f);
            _scenarioNameInput = UiFactory.CreateInput(row, "New Scenario", 135f);
            _scenarioNameInput.onEndEdit.AddListener(SetScenarioName);

            UiFactory.CreateLabel(row, "Length(s)", 58f);
            _durationInput = UiFactory.CreateInput(row, "10", 55f);
            _durationInput.onEndEdit.AddListener(SetScenarioDuration);

            _scenarioIdText = UiFactory.CreateLabel(row, "ID", 120f);
            UiFactory.CreateLabel(row, "Path", 34f);
            _pathInput = UiFactory.CreateInput(row, "scenario.json", 135f);
            UiFactory.CreateButton(row, "Browse", BrowseProjectFile, 58f);
            UiFactory.CreateButton(row, "BG Image", BrowsePreviewBackgroundImage, 72f);
            UiFactory.CreateButton(row, "New", RequestNew, 52f);
            UiFactory.CreateButton(row, "Save", Save, 52f);
            UiFactory.CreateButton(row, "Load", Load, 52f);
            UiFactory.CreateButton(row, "Export", Export, 60f);
            UiFactory.CreateButton(row, "Exit", RequestExit, 48f);

            _statusText = UiFactory.CreateLabel(row, string.Empty, 90f);
            var statusLayout = _statusText.GetComponent<LayoutElement>();
            if (statusLayout != null) statusLayout.flexibleWidth = 1f;
        }

        private void BuildPlaybackRow(Transform parent)
        {
            var row = UiFactory.CreateRow(parent);
            UiFactory.CreateButton(row, "|<", JumpToStart, 42f);
            UiFactory.CreateButton(row, "Play", Play, 58f);
            UiFactory.CreateButton(row, "Pause", Pause, 62f);
            UiFactory.CreateButton(row, "Stop", Stop, 58f);
            UiFactory.CreateButton(row, ">|", JumpToEnd, 42f);
            _timeText = UiFactory.CreateLabel(row, "0.000 / 10.000 s", 140f);

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

            _multiSelectToggle = UiFactory.CreateToggle(row, "Multi", false);
            UiFactory.CreateButton(row, "Zoom -", ZoomOut, 64f);
            UiFactory.CreateButton(row, "Zoom +", ZoomIn, 64f);
            _undoButton = UiFactory.CreateButton(row, "Undo", () => Document.Undo(), 54f);
            _redoButton = UiFactory.CreateButton(row, "Redo", () => Document.Redo(), 54f);
            UiFactory.CreateButton(row, "Copy", CopySelected, 54f);
            UiFactory.CreateButton(row, "Paste", Paste, 54f);
            UiFactory.CreateButton(row, "Duplicate", Duplicate, 70f);
            UiFactory.CreateButton(row, "Delete", DeleteSelection, 60f);
        }

        private void BuildInspectorRow(Transform parent)
        {
            var row = UiFactory.CreateRow(parent, 38f);
            UiFactory.CreateLabel(row, "Unit Name", 62f);
            _unitNameInput = UiFactory.CreateInput(row, string.Empty, 135f);
            _unitNameInput.onEndEdit.AddListener(SetSelectedUnitName);

            UiFactory.CreateLabel(row, "KF Time(s)", 62f);
            _keyframeTimeInput = UiFactory.CreateInput(row, string.Empty, 72f);
            _keyframeTimeInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            _keyframeTimeInput.onEndEdit.AddListener(SetSelectedColorKeyframeTimeFromText);

            UiFactory.CreateLabel(row, "KF RGB", 44f);
            _colorR = UiFactory.CreateInput(row, string.Empty, 50f);
            _colorG = UiFactory.CreateInput(row, string.Empty, 50f);
            _colorB = UiFactory.CreateInput(row, string.Empty, 50f);
            _colorR.contentType = _colorG.contentType = _colorB.contentType = TMP_InputField.ContentType.DecimalNumber;
            _colorR.onEndEdit.AddListener(_ => ApplyKeyframeRgb());
            _colorG.onEndEdit.AddListener(_ => ApplyKeyframeRgb());
            _colorB.onEndEdit.AddListener(_ => ApplyKeyframeRgb());

            _colorPickerButton = UiFactory.CreateButton(row, "KF HSV", OpenHsvColorPicker, 58f);
            var previewGo = UiFactory.CreateUIObject("ColorPreview", row);
            _colorPreview = UiFactory.AddImage(previewGo, new Color(0.25f, 0.25f, 0.25f, 1f));
            _colorPreview.raycastTarget = false;
            var previewLayout = previewGo.AddComponent<LayoutElement>();
            previewLayout.preferredWidth = 26f;
            previewLayout.preferredHeight = 26f;

            var hint = UiFactory.CreateLabel(
                row,
                "Double-click: add keyframe / Ctrl-click or Multi: multi-select",
                390f);
            var hintLayout = hint.GetComponent<LayoutElement>();
            if (hintLayout != null) hintLayout.flexibleWidth = 1f;
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
            _durationInput?.SetTextWithoutNotify(Document.Data.metadata.duration.ToString("0.###"));
            if (_scenarioIdText != null)
            {
                var id = Document.Data.metadata.scenarioId ?? string.Empty;
                _scenarioIdText.text = "ID " + id.Substring(0, Mathf.Min(10, id.Length));
            }
            if (_loopToggle != null) _loopToggle.SetIsOnWithoutNotify(Document.Data.editorSettings.loop);
            if (_snapToggle != null) _snapToggle.SetIsOnWithoutNotify(Document.Data.editorSettings.snapEnabled);
            if (_undoButton != null) _undoButton.interactable = Document.CanUndo;
            if (_redoButton != null) _redoButton.interactable = Document.CanRedo;
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
            if (ctrl && ShortcutInput.ZPressedThisFrame)
            {
                if (shift) Document.Redo(); else Document.Undo();
                return;
            }
            if (ctrl && ShortcutInput.CPressedThisFrame) { CopySelected(); return; }
            if (ctrl && ShortcutInput.VPressedThisFrame) { Paste(); return; }
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
            if (!Document.IsDirty) NewNow();
            else _popup.ShowConfirm(
                "Unsaved changes exist. Create a new scenario and discard them?",
                NewNow);
        }

        private void NewNow()
        {
            _isPlaying = false;
            SelectedUnitId = null;
            SelectedColorKeyframeIds.Clear();
            _primarySelectedColorKeyframeId = null;
            _clipboard.Clear();
            Document.NewDocument();
            SetStatus("New scenario created.", false);
        }

        private void BrowseProjectFile()
        {
            var initial = _repository.ResolvePath(_pathInput != null ? _pathInput.text : null);
            if (!ProjectFilePicker.TryPickOpenProjectFile(initial, out var path)) return;
            _pathInput.SetTextWithoutNotify(path);
            SetStatus("Selected: " + path, false);
        }

        private void BrowsePreviewBackgroundImage()
        {
            var current = Document.Data.editorSettings.previewBackgroundImagePath;
            if (!ProjectFilePicker.TryPickOpenImageFile(current, out var path)) return;
            Document.Execute(d => d.editorSettings.previewBackgroundImagePath = path);
            SetStatus("Preview background: " + path, false);
        }

        private void Save()
        {
            try
            {
                _repository.Save(_pathInput.text, Document.Data);
                Document.MarkSaved();
                SetStatus("Saved: " + _repository.ResolvePath(_pathInput.text), false);
            }
            catch (Exception ex)
            {
                SetStatus("Save failed: " + ex.Message, true);
            }
        }

        private void Load()
        {
            try
            {
                _isPlaying = false;
                var data = _repository.Load(_pathInput.text);
                SelectedUnitId = null;
                SelectedColorKeyframeIds.Clear();
                _primarySelectedColorKeyframeId = null;
                Document.LoadDocument(data);
                SetStatus("Loaded: " + _repository.ResolvePath(_pathInput.text), false);
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
                var json = _repository.ResolvePath(_pathInput.text);
                var dir = Path.GetDirectoryName(json) ?? Application.persistentDataPath;
                var path = Path.Combine(dir, Path.GetFileNameWithoutExtension(json) + ".bin");
                _exporter.Export(path, Document.Data);
                SetStatus("Dummy export: " + path, false);
            }
            catch (Exception ex)
            {
                SetStatus("Export failed: " + ex.Message, true);
            }
        }

        private void RequestExit()
        {
            if (Document.IsDirty)
                _popup.ShowConfirm("Unsaved changes exist. Exit without saving?", QuitNow);
            else
                QuitNow();
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

        private void SetSelectedUnitName(string value)
        {
            if (_buildingUi || string.IsNullOrEmpty(SelectedUnitId)) return;
            var id = SelectedUnitId;
            var text = string.IsNullOrWhiteSpace(value) ? id : value.Trim();
            Document.Execute(d =>
            {
                var u = d.lightingUnits.FirstOrDefault(x => x.unitId == id);
                if (u != null) u.displayName = text;
            });
        }

        private void RefreshInspector()
        {
            if (_unitNameInput == null) return;
            _buildingUi = true;

            var unit = string.IsNullOrEmpty(SelectedUnitId)
                ? null
                : Document.FindUnit(SelectedUnitId);
            _unitNameInput.interactable = unit != null;
            _unitNameInput.SetTextWithoutNotify(unit != null ? unit.displayName : string.Empty);

            var selectedKeys = SelectedColorKeyframeIds
                .Select(id => Document.FindColorKeyframe(id))
                .Where(k => k != null)
                .ToList();
            var selectedUnits = SelectedColorKeyframeIds
                .Select(id => Document.FindUnitForColorKeyframe(id))
                .Where(u => u != null)
                .ToList();
            var allEditable = selectedKeys.Count > 0 && selectedUnits.All(u => !u.track.locked);
            var single = selectedKeys.Count == 1 ? selectedKeys[0] : null;

            _keyframeTimeInput.interactable = single != null && allEditable;
            _keyframeTimeInput.SetTextWithoutNotify(single != null ? single.time.ToString("0.###") : string.Empty);

            _colorR.interactable = _colorG.interactable = _colorB.interactable = allEditable;
            _colorPickerButton.interactable = allEditable;

            if (selectedKeys.Count > 0 && AllSameColor(selectedKeys, out var commonColor))
            {
                _colorR.SetTextWithoutNotify(Mathf.RoundToInt(commonColor.r * 255f).ToString());
                _colorG.SetTextWithoutNotify(Mathf.RoundToInt(commonColor.g * 255f).ToString());
                _colorB.SetTextWithoutNotify(Mathf.RoundToInt(commonColor.b * 255f).ToString());
                _colorPreview.color = commonColor;
            }
            else
            {
                _colorR.SetTextWithoutNotify(string.Empty);
                _colorG.SetTextWithoutNotify(string.Empty);
                _colorB.SetTextWithoutNotify(string.Empty);
                _colorPreview.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            }

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

        private void ApplyKeyframeRgb()
        {
            if (_buildingUi || SelectedColorKeyframeIds.Count == 0) return;
            if (!float.TryParse(_colorR.text, out var r) ||
                !float.TryParse(_colorG.text, out var g) ||
                !float.TryParse(_colorB.text, out var b))
            {
                SetStatus("RGB contains an invalid number.", true);
                RefreshInspector();
                return;
            }

            var color = new Color(
                Mathf.Clamp(r, 0f, 255f) / 255f,
                Mathf.Clamp(g, 0f, 255f) / 255f,
                Mathf.Clamp(b, 0f, 255f) / 255f,
                1f);
            if (!Document.TrySetColorKeyframesColor(SelectedColorKeyframeIds, color, out var error))
                SetStatus(error, true);
        }

        private void OpenHsvColorPicker()
        {
            if (_colorPickerButton != null)
                OpenColorPickerForSelection((RectTransform)_colorPickerButton.transform);
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

            SetStatus(
                _clipboard.Count > 0
                    ? $"Copied {_clipboard.Count} color keyframe(s)."
                    : "No color keyframes selected.",
                _clipboard.Count == 0);
        }

        private void Paste()
        {
            if (_clipboard.Count == 0)
            {
                SetStatus("Clipboard is empty.", true);
                return;
            }
            var earliest = _clipboard.Min(x => x.time);
            PasteClipboardWithDelta(CurrentTime - earliest, "Paste");
        }

        private void Duplicate()
        {
            if (SelectedColorKeyframeIds.Count == 0)
            {
                SetStatus("No color keyframes selected.", true);
                return;
            }

            CopySelected();
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
            PasteClipboardWithDelta(delta, "Duplicate");
        }

        private void PasteClipboardWithDelta(float delta, string operationName)
        {
            const float epsilon = 0.0001f;
            var before = Document.CaptureState();
            var plans = new List<KeyValuePair<LightingUnitData, ColorKeyframeData>>();
            var plannedTimesByUnit = new Dictionary<string, List<float>>();

            foreach (var source in _clipboard)
            {
                var unit = Document.FindUnit(source.unitId);
                if (unit == null || unit.track.locked)
                {
                    SetStatus(operationName + " failed because a source track is missing or locked.", true);
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
