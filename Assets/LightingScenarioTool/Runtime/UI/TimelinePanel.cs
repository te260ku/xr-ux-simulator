using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace LightingScenarioTool
{
    internal sealed class TimelinePanel : MonoBehaviour
    {
        private const float LabelWidth = 230f;
        private const float RulerHeight = 36f;
        private const float RowHeight = 48f;
        private const float LaneHeight = 38f;
        private const float ScrollbarHeight = 12f;

        private LightingScenarioApp _app;
        private RectTransform _rulerViewport;
        private RectTransform _rulerContent;
        private RectTransform _labelsViewport;
        private RectTransform _labelsContent;
        private RectTransform _timeViewport;
        private RectTransform _timeContent;
        private RectTransform _marqueeSelection;
        private RectTransform _playhead;
        private RectTransform _rulerPlayhead;
        private Scrollbar _horizontalScrollbar;
        private float _timeContentWidth;
        private float _rowsContentHeight;
        private float _verticalOffset;
        private float _horizontalNormalized;
        private bool _updatingScrollbar;
        private Vector2 _lastViewportSize;

        private readonly Dictionary<string, ColorKeyframeView> _keyframeViews = new Dictionary<string, ColorKeyframeView>();
        private readonly Dictionary<string, Image> _trackLabelImages = new Dictionary<string, Image>();
        private readonly Dictionary<string, Color> _trackLabelBaseColors = new Dictionary<string, Color>();
        private readonly Dictionary<string, Image> _trackTimeImages = new Dictionary<string, Image>();
        private readonly Dictionary<string, Color> _trackTimeBaseColors = new Dictionary<string, Color>();

        internal LightingScenarioApp App => _app;

        public void Initialize(LightingScenarioApp app)
        {
            _app = app;
            BuildChrome();
            var wheel = gameObject.GetComponent<TimelineWheelInput>() ?? gameObject.AddComponent<TimelineWheelInput>();
            wheel.Initialize(this);
            var pan = gameObject.GetComponent<TimelineMiddleMousePanInput>() ?? gameObject.AddComponent<TimelineMiddleMousePanInput>();
            pan.Initialize(this);
        }

        private void BuildChrome()
        {
            var corner = UiFactory.CreateUIObject("Corner", transform);
            var cornerRt = (RectTransform)corner.transform;
            cornerRt.anchorMin = cornerRt.anchorMax = new Vector2(0f, 1f);
            cornerRt.pivot = new Vector2(0f, 1f);
            cornerRt.sizeDelta = new Vector2(LabelWidth, RulerHeight);
            UiFactory.AddImage(corner, new Color(0.13f, 0.13f, 0.13f, 1f));
            var cornerTextGo = UiFactory.CreateUIObject("CornerText", corner.transform);
            UiFactory.Stretch((RectTransform)cornerTextGo.transform);
            var cornerText = UiFactory.AddText(cornerTextGo, "Lighting Tracks / Time", 13, TextAnchor.MiddleLeft);
            cornerText.margin = new Vector4(8f, 0f, 4f, 0f);
            cornerText.raycastTarget = false;

            _rulerViewport = CreateViewport("RulerViewport", transform, new Color(0.13f, 0.13f, 0.13f, 1f));
            _rulerViewport.anchorMin = new Vector2(0f, 1f);
            _rulerViewport.anchorMax = new Vector2(1f, 1f);
            _rulerViewport.pivot = new Vector2(0.5f, 1f);
            _rulerViewport.offsetMin = new Vector2(LabelWidth, -RulerHeight);
            _rulerViewport.offsetMax = Vector2.zero;
            _rulerContent = CreateTopLeftContent("RulerContent", _rulerViewport);

            _labelsViewport = CreateViewport("LabelsViewport", transform, new Color(0.12f, 0.12f, 0.12f, 1f));
            _labelsViewport.anchorMin = new Vector2(0f, 0f);
            _labelsViewport.anchorMax = new Vector2(0f, 1f);
            _labelsViewport.pivot = new Vector2(0f, 0.5f);
            _labelsViewport.offsetMin = new Vector2(0f, ScrollbarHeight);
            _labelsViewport.offsetMax = new Vector2(LabelWidth, -RulerHeight);
            _labelsContent = CreateTopLeftContent("LabelsContent", _labelsViewport);

            _timeViewport = CreateViewport("TimeViewport", transform, new Color(0.09f, 0.09f, 0.09f, 1f));
            _timeViewport.anchorMin = Vector2.zero;
            _timeViewport.anchorMax = Vector2.one;
            _timeViewport.offsetMin = new Vector2(LabelWidth, ScrollbarHeight);
            _timeViewport.offsetMax = new Vector2(0f, -RulerHeight);
            _timeContent = CreateTopLeftContent("TimeContent", _timeViewport);
            BuildMarqueeSelection();
            var marqueeInput = _timeViewport.gameObject.AddComponent<TimelineMarqueeSelectInput>();
            marqueeInput.Initialize(this);

            var lowerLeft = UiFactory.CreateUIObject("LowerLeft", transform);
            var lowerLeftRt = (RectTransform)lowerLeft.transform;
            lowerLeftRt.anchorMin = new Vector2(0f, 0f);
            lowerLeftRt.anchorMax = new Vector2(0f, 0f);
            lowerLeftRt.pivot = Vector2.zero;
            lowerLeftRt.anchoredPosition = Vector2.zero;
            lowerLeftRt.sizeDelta = new Vector2(LabelWidth, ScrollbarHeight);
            UiFactory.AddImage(lowerLeft, new Color(0.12f, 0.12f, 0.12f, 1f)).raycastTarget = false;

            BuildHorizontalScrollbar();
        }

        private static RectTransform CreateViewport(string name, Transform parent, Color color)
        {
            var go = UiFactory.CreateUIObject(name, parent);
            var rt = (RectTransform)go.transform;
            UiFactory.AddImage(go, color).raycastTarget = true;
            go.AddComponent<RectMask2D>();
            return rt;
        }

        private static RectTransform CreateTopLeftContent(string name, Transform parent)
        {
            var go = UiFactory.CreateUIObject(name, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            return rt;
        }

        private void BuildMarqueeSelection()
        {
            var go = UiFactory.CreateUIObject("MarqueeSelection", _timeViewport);
            _marqueeSelection = (RectTransform)go.transform;
            _marqueeSelection.anchorMin = _marqueeSelection.anchorMax = Vector2.zero;
            _marqueeSelection.pivot = Vector2.zero;
            _marqueeSelection.anchoredPosition = Vector2.zero;
            _marqueeSelection.sizeDelta = Vector2.zero;

            var image = UiFactory.AddImage(go, new Color(0.25f, 0.55f, 1f, 0.16f));
            image.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.45f, 0.72f, 1f, 0.95f);
            outline.effectDistance = new Vector2(1f, 1f);
            go.SetActive(false);
        }

        internal void BeginMarqueeSelection(Vector2 startScreen, Camera eventCamera)
        {
            UpdateMarqueeSelection(startScreen, startScreen, eventCamera);
            if (_marqueeSelection != null)
            {
                _marqueeSelection.gameObject.SetActive(true);
                _marqueeSelection.SetAsLastSibling();
            }
        }

        internal void UpdateMarqueeSelection(Vector2 startScreen, Vector2 currentScreen, Camera eventCamera)
        {
            if (_marqueeSelection == null || _timeViewport == null) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _timeViewport, startScreen, eventCamera, out var startLocal)) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _timeViewport, currentScreen, eventCamera, out var currentLocal)) return;

            var rect = _timeViewport.rect;
            var pivotOffset = Vector2.Scale(rect.size, _timeViewport.pivot);
            startLocal += pivotOffset;
            currentLocal += pivotOffset;

            startLocal.x = Mathf.Clamp(startLocal.x, 0f, rect.width);
            startLocal.y = Mathf.Clamp(startLocal.y, 0f, rect.height);
            currentLocal.x = Mathf.Clamp(currentLocal.x, 0f, rect.width);
            currentLocal.y = Mathf.Clamp(currentLocal.y, 0f, rect.height);

            var min = Vector2.Min(startLocal, currentLocal);
            var max = Vector2.Max(startLocal, currentLocal);
            _marqueeSelection.anchoredPosition = min;
            _marqueeSelection.sizeDelta = max - min;
        }

        internal void EndMarqueeSelection(Vector2 startScreen, Vector2 endScreen, bool additive)
        {
            if (_marqueeSelection != null) _marqueeSelection.gameObject.SetActive(false);

            var selectionRect = Rect.MinMaxRect(
                Mathf.Min(startScreen.x, endScreen.x),
                Mathf.Min(startScreen.y, endScreen.y),
                Mathf.Max(startScreen.x, endScreen.x),
                Mathf.Max(startScreen.y, endScreen.y));

            var selectedIds = new List<string>();
            foreach (var pair in _keyframeViews)
            {
                var view = pair.Value;
                if (view == null || view.RectTransform == null) continue;
                var corners = new Vector3[4];
                view.RectTransform.GetWorldCorners(corners);
                var canvas = view.GetComponentInParent<Canvas>();
                var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
                var a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                var b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                var keyRect = Rect.MinMaxRect(
                    Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y),
                    Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
                if (selectionRect.Overlaps(keyRect, true)) selectedIds.Add(pair.Key);
            }

            _app.SetColorKeyframeSelection(selectedIds, additive);
        }

        internal void CancelMarqueeSelection()
        {
            if (_marqueeSelection != null) _marqueeSelection.gameObject.SetActive(false);
        }

        private void BuildHorizontalScrollbar()
        {
            var go = UiFactory.CreateUIObject("HorizontalScrollbar", transform);
            var rt = (RectTransform)go.transform;
            // Fixed 12 px high bar. Using explicit bottom offsets avoids the previous
            // stretched-height behavior on some resolutions / CanvasScaler factors.
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(LabelWidth + 2f, 1f);
            rt.offsetMax = new Vector2(-2f, ScrollbarHeight - 1f);

            var background = UiFactory.AddImage(go, new Color(0.10f, 0.10f, 0.10f, 1f));
            var slidingArea = UiFactory.CreateUIObject("Sliding Area", go.transform);
            var slidingRt = (RectTransform)slidingArea.transform;
            UiFactory.Stretch(slidingRt);
            slidingRt.offsetMin = new Vector2(2f, 2f);
            slidingRt.offsetMax = new Vector2(-2f, -2f);

            var handleGo = UiFactory.CreateUIObject("Handle", slidingArea.transform);
            var handleRt = (RectTransform)handleGo.transform;
            // A newly-created RectTransform starts with a 100x100 sizeDelta.
            // Setting only the anchors to (0,0)-(1,1) leaves that sizeDelta in place,
            // which makes the Scrollbar handle about 100 px too tall/wide.
            // Stretch() also resets offsetMin/offsetMax, so the handle is constrained
            // exactly to the sliding area's height.
            UiFactory.Stretch(handleRt);
            var handleImage = UiFactory.AddImage(handleGo, new Color(0.42f, 0.42f, 0.42f, 1f));

            _horizontalScrollbar = go.AddComponent<Scrollbar>();
            _horizontalScrollbar.direction = Scrollbar.Direction.LeftToRight;
            _horizontalScrollbar.targetGraphic = handleImage;
            _horizontalScrollbar.handleRect = handleRt;
            _horizontalScrollbar.onValueChanged.AddListener(OnHorizontalScrollbarChanged);
            background.raycastTarget = true;
        }

        public void Rebuild()
        {
            ClearChildren(_rulerContent);
            ClearChildren(_labelsContent);
            ClearChildren(_timeContent);
            _keyframeViews.Clear();
            _trackLabelImages.Clear();
            _trackLabelBaseColors.Clear();
            _trackTimeImages.Clear();
            _trackTimeBaseColors.Clear();

            var duration = Mathf.Max(0.001f, _app.Document.Data.metadata.duration);
            var pps = _app.Document.Data.editorSettings.pixelsPerSecond;
            _timeContentWidth = duration * pps + 60f;
            _rowsContentHeight = Mathf.Max(1, _app.Document.Data.lightingUnits.Count) * RowHeight;
            _rulerContent.sizeDelta = new Vector2(_timeContentWidth, RulerHeight);
            _labelsContent.sizeDelta = new Vector2(LabelWidth, _rowsContentHeight);
            _timeContent.sizeDelta = new Vector2(_timeContentWidth, _rowsContentHeight);

            BuildRuler();
            for (var i = 0; i < _app.Document.Data.lightingUnits.Count; i++)
                BuildTrackRow(_app.Document.Data.lightingUnits[i], i);
            BuildHorizontalTrackSeparators();
            BuildPlayhead();

            Canvas.ForceUpdateCanvases();
            ClampScrollOffsets();
            UpdateScrollbarVisual();
            ApplyScrollOffsets();
            RefreshSelection();
            RefreshPlayhead();
        }

        private static void ClearChildren(RectTransform parent)
        {
            if (parent == null) return;
            for (var i = parent.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.GetChild(i).gameObject);
        }

        private void BuildRuler()
        {
            var interactionGo = UiFactory.CreateUIObject("RulerInteraction", _rulerContent);
            var interactionRt = (RectTransform)interactionGo.transform;
            interactionRt.anchorMin = interactionRt.anchorMax = new Vector2(0f, 1f);
            interactionRt.pivot = new Vector2(0f, 1f);
            interactionRt.sizeDelta = new Vector2(_timeContentWidth, RulerHeight);
            UiFactory.AddImage(interactionGo, new Color(0f, 0f, 0f, 0f)).raycastTarget = true;
            interactionGo.AddComponent<TimelineRulerInput>().Initialize(_app, interactionRt);

            var pps = Mathf.Max(0.001f, _app.Document.Data.editorSettings.pixelsPerSecond);
            var duration = Mathf.Max(0f, _app.Document.Data.metadata.duration);
            var interval = GetMajorTickInterval(pps);
            var count = Mathf.CeilToInt(duration / interval);
            for (var i = 0; i <= count; i++)
            {
                var time = Mathf.Min(duration, i * interval);
                var x = time * pps;
                var tickGo = UiFactory.CreateUIObject("Tick_" + i, _rulerContent);
                var tickRt = (RectTransform)tickGo.transform;
                tickRt.anchorMin = tickRt.anchorMax = new Vector2(0f, 0f);
                tickRt.pivot = new Vector2(0.5f, 0f);
                tickRt.anchoredPosition = new Vector2(x, 0f);
                tickRt.sizeDelta = new Vector2(1f, 13f);
                UiFactory.AddImage(tickGo, new Color(0.55f, 0.55f, 0.55f, 1f)).raycastTarget = false;

                var textGo = UiFactory.CreateUIObject("TickLabel_" + i, _rulerContent);
                var textRt = (RectTransform)textGo.transform;
                textRt.anchorMin = textRt.anchorMax = new Vector2(0f, 1f);
                textRt.pivot = new Vector2(0.5f, 1f);
                textRt.anchoredPosition = new Vector2(x, -2f);
                textRt.sizeDelta = new Vector2(78f, 20f);
                UiFactory.AddText(textGo, FormatTickTime(time, interval), 11, TextAnchor.MiddleCenter).raycastTarget = false;

                if (time >= duration - 0.0001f) break;
            }
        }

        private static float GetMajorTickInterval(float pixelsPerSecond)
        {
            var targetSeconds = 72f / Mathf.Max(0.001f, pixelsPerSecond);
            var candidates = new[] { 0.1f, 0.2f, 0.5f, 1f, 2f, 5f, 10f, 20f, 50f, 100f };
            for (var i = 0; i < candidates.Length; i++)
                if (candidates[i] >= targetSeconds) return candidates[i];
            return candidates[candidates.Length - 1];
        }

        private static string FormatTickTime(float time, float interval)
        {
            if (interval < 1f) return time.ToString("0.0##") + "s";
            return Mathf.Approximately(time, Mathf.Round(time))
                ? Mathf.RoundToInt(time) + "s"
                : time.ToString("0.##") + "s";
        }

        private void BuildTrackRow(LightingUnitData unit, int index)
        {
            BuildTrackLabel(unit, index);
            BuildTrackTimeArea(unit, index);
        }

        private void BuildTrackLabel(LightingUnitData unit, int index)
        {
            var rowGo = UiFactory.CreateUIObject("Label_" + unit.unitId, _labelsContent);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = rowRt.anchorMax = new Vector2(0f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -index * RowHeight);
            rowRt.sizeDelta = new Vector2(LabelWidth, RowHeight);

            var selected = unit.unitId == _app.SelectedUnitId;
            var baseColor = index % 2 == 0
                ? new Color(0.15f, 0.15f, 0.15f, 1f)
                : new Color(0.17f, 0.17f, 0.17f, 1f);
            var rowImage = UiFactory.AddImage(rowGo,
                selected ? new Color(0.24f, 0.24f, 0.16f, 1f) : baseColor);
            _trackLabelImages[unit.unitId] = rowImage;
            _trackLabelBaseColors[unit.unitId] = baseColor;
            rowGo.AddComponent<TrackLabelClick>().Initialize(_app, unit.unitId);

            var nameInput = UiFactory.CreateInput(rowGo.transform, unit.displayName, 80f);
            nameInput.gameObject.name = "TrackNameInput";
            var nameRt = (RectTransform)nameInput.transform;
            nameRt.anchorMin = nameRt.anchorMax = new Vector2(0f, 0.5f);
            nameRt.pivot = new Vector2(0f, 0.5f);
            nameRt.anchoredPosition = new Vector2(8f, 8f);
            nameRt.sizeDelta = new Vector2(80f, 22f);
            var nameLayout = nameInput.GetComponent<LayoutElement>();
            if (nameLayout != null) nameLayout.preferredHeight = 22f;
            if (nameInput.textComponent != null) nameInput.textComponent.fontSize = 12f;
            var capturedUnitId = unit.unitId;
            nameInput.onEndEdit.AddListener(value => _app.SetUnitName(capturedUnitId, value));

            CreateMiniToggle(rowGo.transform, "L", unit.track.locked, new Vector2(92f, 8f))
                .onValueChanged.AddListener(v => _app.SetTrackLocked(unit.unitId, v));
            CreateMiniToggle(rowGo.transform, "M", unit.track.muted, new Vector2(126f, 8f))
                .onValueChanged.AddListener(v => _app.SetTrackMuted(unit.unitId, v));
            CreateMiniButton(rowGo.transform, "▲", new Vector2(162f, 8f))
                .onClick.AddListener(() => _app.MoveTrack(unit.unitId, -1));
            CreateMiniButton(rowGo.transform, "▼", new Vector2(196f, 8f))
                .onClick.AddListener(() => _app.MoveTrack(unit.unitId, 1));

        }

        private void BuildTrackTimeArea(LightingUnitData unit, int index)
        {
            var rowGo = UiFactory.CreateUIObject("Track_" + unit.unitId, _timeContent);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.anchorMin = rowRt.anchorMax = new Vector2(0f, 1f);
            rowRt.pivot = new Vector2(0f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, -index * RowHeight);
            rowRt.sizeDelta = new Vector2(_timeContentWidth, RowHeight);
            var timeBaseColor = index % 2 == 0
                ? new Color(0.105f, 0.105f, 0.105f, 1f)
                : new Color(0.12f, 0.12f, 0.12f, 1f);
            var timeImage = UiFactory.AddImage(rowGo,
                unit.unitId == _app.SelectedUnitId
                    ? new Color(0.145f, 0.145f, 0.105f, 1f)
                    : timeBaseColor);
            timeImage.raycastTarget = false;
            _trackTimeImages[unit.unitId] = timeImage;
            _trackTimeBaseColors[unit.unitId] = timeBaseColor;

            BuildGridLines(rowGo.transform);

            var lane = CreateLane("ColorKeyframeLane", rowGo.transform, 5f, LaneHeight,
                new Color(0.12f, 0.12f, 0.12f, 0.22f));
            lane.sizeDelta = new Vector2(_timeContentWidth, LaneHeight);
            lane.gameObject.AddComponent<TrackColorInput>().Initialize(_app, unit.unitId, lane);
            BuildColorSegments(lane, unit);

            foreach (var keyframe in unit.track.colorKeyframes)
            {
                var keyGo = UiFactory.CreateUIObject("ColorKeyframe_" + keyframe.keyframeId, lane);
                var view = keyGo.AddComponent<ColorKeyframeView>();
                view.Initialize(_app, unit.unitId, keyframe.keyframeId);
                _keyframeViews[keyframe.keyframeId] = view;
            }
        }

        private void BuildColorSegments(RectTransform lane, LightingUnitData unit)
        {
            if (lane == null || unit?.track?.colorKeyframes == null || unit.track.colorKeyframes.Count < 2) return;
            var pps = _app.Document.Data.editorSettings.pixelsPerSecond;
            for (var i = 0; i < unit.track.colorKeyframes.Count - 1; i++)
            {
                var a = unit.track.colorKeyframes[i];
                var b = unit.track.colorKeyframes[i + 1];
                var x0 = a.time * pps;
                var width = Mathf.Max(1f, (b.time - a.time) * pps);
                var pieces = Mathf.Clamp(Mathf.CeilToInt(width / 10f), 1, 64);
                for (var piece = 0; piece < pieces; piece++)
                {
                    var t0 = piece / (float)pieces;
                    var t1 = (piece + 1) / (float)pieces;
                    var go = UiFactory.CreateUIObject("ColorSegment_" + i + "_" + piece, lane);
                    var rt = (RectTransform)go.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0f, 0.5f);
                    rt.anchoredPosition = new Vector2(x0 + width * t0, 0f);
                    rt.sizeDelta = new Vector2(Mathf.Max(1.5f, width * (t1 - t0) + 0.5f), 5f);
                    var color = Color.Lerp(a.color.ToUnityColor(), b.color.ToUnityColor(), (t0 + t1) * 0.5f);
                    UiFactory.AddImage(go, color).raycastTarget = false;
                }
            }
        }

        private static RectTransform CreateLane(string name, Transform parent, float yFromTop, float height, Color color)
        {
            var go = UiFactory.CreateUIObject(name, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, -yFromTop);
            rt.sizeDelta = new Vector2(((RectTransform)parent).rect.width, height);
            var image = UiFactory.AddImage(go, color);
            image.raycastTarget = true;
            return rt;
        }

        private void BuildGridLines(Transform row)
        {
            var pps = Mathf.Max(0.001f, _app.Document.Data.editorSettings.pixelsPerSecond);
            var duration = Mathf.Max(0f, _app.Document.Data.metadata.duration);
            var interval = GetMajorTickInterval(pps);
            var count = Mathf.CeilToInt(duration / interval);
            for (var i = 0; i <= count; i++)
            {
                var time = Mathf.Min(duration, i * interval);
                var lineGo = UiFactory.CreateUIObject("Grid_" + i, row);
                var lineRt = (RectTransform)lineGo.transform;
                lineRt.anchorMin = lineRt.anchorMax = new Vector2(0f, 1f);
                lineRt.pivot = new Vector2(0.5f, 1f);
                lineRt.anchoredPosition = new Vector2(time * pps, 0f);
                lineRt.sizeDelta = new Vector2(1f, RowHeight);
                UiFactory.AddImage(lineGo, new Color(0.29f, 0.29f, 0.29f, 0.62f)).raycastTarget = false;
                if (time >= duration - 0.0001f) break;
            }
        }

        private void BuildHorizontalTrackSeparators()
        {
            var count = _app.Document.Data.lightingUnits.Count;
            for (var row = 0; row < count; row++)
            {
                var boundaryY = -((row + 1) * RowHeight) + 0.5f;
                AddHorizontalSeparator(_labelsContent, LabelWidth, boundaryY, "LabelRowSeparator_" + (row + 1));
                AddHorizontalSeparator(_timeContent, _timeContentWidth, boundaryY, "TimeRowSeparator_" + (row + 1));
            }
        }

        private static void AddHorizontalSeparator(Transform parent, float width, float y, string name)
        {
            var lineGo = UiFactory.CreateUIObject(name, parent);
            var lineRt = (RectTransform)lineGo.transform;
            lineRt.anchorMin = lineRt.anchorMax = new Vector2(0f, 1f);
            lineRt.pivot = new Vector2(0f, 0.5f);
            lineRt.anchoredPosition = new Vector2(0f, y);
            lineRt.sizeDelta = new Vector2(width, 1f);
            UiFactory.AddImage(lineGo, new Color(0.38f, 0.38f, 0.38f, 1f)).raycastTarget = false;
            lineGo.transform.SetAsLastSibling();
        }

        private Toggle CreateMiniToggle(Transform parent, string text, bool value, Vector2 pos)
        {
            var root = UiFactory.CreateUIObject("Toggle_" + text, parent);
            var rt = (RectTransform)root.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(28f, 24f);
            var bg = UiFactory.AddImage(root, new Color(0.22f, 0.22f, 0.22f, 1f));
            var check = UiFactory.CreateUIObject("Check", root.transform);
            var crt = (RectTransform)check.transform;
            UiFactory.Stretch(crt);
            crt.offsetMin = new Vector2(3f, 3f);
            crt.offsetMax = new Vector2(-3f, -3f);
            var checkImage = UiFactory.AddImage(check, new Color(0.55f, 0.55f, 0.2f, 0.8f));
            var txtGo = UiFactory.CreateUIObject("Text", root.transform);
            UiFactory.Stretch((RectTransform)txtGo.transform);
            UiFactory.AddText(txtGo, text, 11, TextAnchor.MiddleCenter).raycastTarget = false;
            var toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = checkImage;
            toggle.isOn = value;
            return toggle;
        }

        private Button CreateMiniButton(Transform parent, string text, Vector2 pos)
        {
            var go = UiFactory.CreateUIObject("Button_" + text, parent);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(28f, 24f);
            UiFactory.AddImage(go, new Color(0.22f, 0.22f, 0.22f, 1f));
            var button = go.AddComponent<Button>();
            var txtGo = UiFactory.CreateUIObject("Text", go.transform);
            UiFactory.Stretch((RectTransform)txtGo.transform);
            UiFactory.AddText(txtGo, text, 11, TextAnchor.MiddleCenter).raycastTarget = false;
            return button;
        }

        private void BuildPlayhead()
        {
            var bodyGo = UiFactory.CreateUIObject("Playhead", _timeContent);
            _playhead = (RectTransform)bodyGo.transform;
            _playhead.anchorMin = _playhead.anchorMax = new Vector2(0f, 1f);
            _playhead.pivot = new Vector2(0.5f, 1f);
            _playhead.sizeDelta = new Vector2(12f, _rowsContentHeight);
            UiFactory.AddImage(bodyGo, new Color(0f, 0f, 0f, 0f)).raycastTarget = true;
            bodyGo.AddComponent<PlayheadDragInput>().Initialize(_app, _timeContent);
            var bodyLine = UiFactory.CreateUIObject("Line", bodyGo.transform);
            var bodyLineRt = (RectTransform)bodyLine.transform;
            bodyLineRt.anchorMin = new Vector2(0.5f, 0f);
            bodyLineRt.anchorMax = new Vector2(0.5f, 1f);
            bodyLineRt.pivot = new Vector2(0.5f, 0.5f);
            bodyLineRt.sizeDelta = new Vector2(2f, 0f);
            UiFactory.AddImage(bodyLine, new Color(1f, 0.3f, 0.25f, 1f)).raycastTarget = false;
            _playhead.SetAsLastSibling();

            var rulerGo = UiFactory.CreateUIObject("RulerPlayhead", _rulerContent);
            _rulerPlayhead = (RectTransform)rulerGo.transform;
            _rulerPlayhead.anchorMin = _rulerPlayhead.anchorMax = new Vector2(0f, 1f);
            _rulerPlayhead.pivot = new Vector2(0.5f, 1f);
            _rulerPlayhead.sizeDelta = new Vector2(12f, RulerHeight);
            UiFactory.AddImage(rulerGo, new Color(0f, 0f, 0f, 0f)).raycastTarget = true;
            rulerGo.AddComponent<PlayheadDragInput>().Initialize(_app, _rulerContent);
            var rulerLine = UiFactory.CreateUIObject("Line", rulerGo.transform);
            var rulerLineRt = (RectTransform)rulerLine.transform;
            rulerLineRt.anchorMin = new Vector2(0.5f, 0f);
            rulerLineRt.anchorMax = new Vector2(0.5f, 1f);
            rulerLineRt.pivot = new Vector2(0.5f, 0.5f);
            rulerLineRt.sizeDelta = new Vector2(2f, 0f);
            UiFactory.AddImage(rulerLine, new Color(1f, 0.3f, 0.25f, 1f)).raycastTarget = false;
            var headGo = UiFactory.CreateUIObject("Head", rulerGo.transform);
            var headRt = (RectTransform)headGo.transform;
            headRt.anchorMin = headRt.anchorMax = new Vector2(0.5f, 1f);
            headRt.pivot = new Vector2(0.5f, 0.5f);
            headRt.anchoredPosition = new Vector2(0f, -5f);
            headRt.sizeDelta = new Vector2(10f, 10f);
            headRt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            UiFactory.AddImage(headGo, new Color(1f, 0.3f, 0.25f, 1f)).raycastTarget = false;
            _rulerPlayhead.SetAsLastSibling();
        }

        public void RefreshPlayhead()
        {
            var x = _app.CurrentTime * _app.Document.Data.editorSettings.pixelsPerSecond;
            if (_playhead != null) _playhead.anchoredPosition = new Vector2(x, 0f);
            if (_rulerPlayhead != null) _rulerPlayhead.anchoredPosition = new Vector2(x, 0f);
        }

        public void RefreshGeometry()
        {
            foreach (var pair in _keyframeViews)
                pair.Value.RefreshPosition();
            RefreshPlayhead();
        }

        public void PositionColorKeyframeMarker(string unitId, string keyframeId, RectTransform marker)
        {
            var keyframe = _app.Document.FindColorKeyframe(unitId, keyframeId);
            if (keyframe == null || marker == null) return;
            marker.anchoredPosition = new Vector2(
                keyframe.time * _app.Document.Data.editorSettings.pixelsPerSecond,
                0f);
        }

        public void RefreshSelection()
        {
            foreach (var pair in _keyframeViews)
                pair.Value.RefreshSelection();

            foreach (var pair in _trackLabelImages)
            {
                if (pair.Value == null) continue;
                pair.Value.color = pair.Key == _app.SelectedUnitId
                    ? new Color(0.24f, 0.24f, 0.16f, 1f)
                    : _trackLabelBaseColors[pair.Key];
            }

            foreach (var pair in _trackTimeImages)
            {
                if (pair.Value == null) continue;
                pair.Value.color = pair.Key == _app.SelectedUnitId
                    ? new Color(0.145f, 0.145f, 0.105f, 1f)
                    : _trackTimeBaseColors[pair.Key];
            }
        }

        internal void ScrollVertical(float wheelDelta)
        {
            if (Mathf.Approximately(wheelDelta, 0f)) return;
            _verticalOffset += -wheelDelta * 34f;
            ClampScrollOffsets();
            ApplyScrollOffsets();
        }

        private void OnHorizontalScrollbarChanged(float value)
        {
            if (_updatingScrollbar) return;
            _horizontalNormalized = value;
            ApplyScrollOffsets();
        }

        private void LateUpdate()
        {
            if (_timeViewport == null) return;
            var size = _timeViewport.rect.size;
            if ((size - _lastViewportSize).sqrMagnitude < 0.25f) return;
            _lastViewportSize = size;
            ClampScrollOffsets();
            UpdateScrollbarVisual();
            ApplyScrollOffsets();
        }

        private void ClampScrollOffsets()
        {
            var viewportHeight = _timeViewport != null ? Mathf.Max(0f, _timeViewport.rect.height) : 0f;
            _verticalOffset = Mathf.Clamp(_verticalOffset, 0f, Mathf.Max(0f, _rowsContentHeight - viewportHeight));
            _horizontalNormalized = Mathf.Clamp01(_horizontalNormalized);
        }

        private void UpdateScrollbarVisual()
        {
            if (_horizontalScrollbar == null || _timeViewport == null) return;
            var viewportWidth = Mathf.Max(0f, _timeViewport.rect.width);
            var scrollable = _timeContentWidth > viewportWidth + 0.5f;
            _updatingScrollbar = true;
            _horizontalScrollbar.interactable = scrollable;
            _horizontalScrollbar.size = scrollable && _timeContentWidth > 0f
                ? Mathf.Clamp01(viewportWidth / _timeContentWidth)
                : 1f;
            _horizontalScrollbar.value = scrollable ? _horizontalNormalized : 0f;
            _updatingScrollbar = false;
            if (!scrollable) _horizontalNormalized = 0f;
        }

        internal bool CanStartHorizontalPan(Vector2 screenPoint)
        {
            return ContainsScreenPoint(_timeViewport, screenPoint) || ContainsScreenPoint(_rulerViewport, screenPoint);
        }

        private static bool ContainsScreenPoint(RectTransform rect, Vector2 screenPoint)
        {
            if (rect == null) return false;
            var canvas = rect.GetComponentInParent<Canvas>();
            Camera eventCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                eventCamera = canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPoint, eventCamera);
        }

        internal void ScrollHorizontalByPixels(float deltaPixels)
        {
            if (_timeViewport == null) return;
            var viewportWidth = Mathf.Max(0f, _timeViewport.rect.width);
            var maxHorizontal = Mathf.Max(0f, _timeContentWidth - viewportWidth);
            if (maxHorizontal <= 0.001f)
            {
                _horizontalNormalized = 0f;
                UpdateScrollbarVisual();
                ApplyScrollOffsets();
                return;
            }

            var currentPixels = _horizontalNormalized * maxHorizontal;
            var nextPixels = Mathf.Clamp(currentPixels + deltaPixels, 0f, maxHorizontal);
            _horizontalNormalized = nextPixels / maxHorizontal;
            UpdateScrollbarVisual();
            ApplyScrollOffsets();
        }

        private void ApplyScrollOffsets()
        {
            if (_timeViewport == null) return;
            var maxHorizontal = Mathf.Max(0f, _timeContentWidth - Mathf.Max(0f, _timeViewport.rect.width));
            var horizontalPixels = _horizontalNormalized * maxHorizontal;
            if (_rulerContent != null) _rulerContent.anchoredPosition = new Vector2(-horizontalPixels, 0f);
            if (_timeContent != null) _timeContent.anchoredPosition = new Vector2(-horizontalPixels, _verticalOffset);
            if (_labelsContent != null) _labelsContent.anchoredPosition = new Vector2(0f, _verticalOffset);
        }
    }

    internal sealed class TimelineMiddleMousePanInput : MonoBehaviour
    {
        private TimelinePanel _panel;
        private bool _panning;
        private Vector2 _lastPointerPosition;

        public void Initialize(TimelinePanel panel)
        {
            _panel = panel;
        }

        private void Update()
        {
            if (_panel == null) return;

            var pointerPosition = ShortcutInput.PointerPosition;
            if (!_panning)
            {
                if (!ShortcutInput.MiddleMousePressedThisFrame || !_panel.CanStartHorizontalPan(pointerPosition))
                    return;

                _panning = true;
                _lastPointerPosition = pointerPosition;
                return;
            }

            if (!ShortcutInput.MiddleMousePressed)
            {
                _panning = false;
                return;
            }

            var delta = pointerPosition - _lastPointerPosition;
            _lastPointerPosition = pointerPosition;

            // Grab-and-drag semantics: dragging the pointer right pulls the timeline
            // content right, so the scroll position itself moves left.
            if (Mathf.Abs(delta.x) > 0.001f)
                _panel.ScrollHorizontalByPixels(-delta.x);
        }

        private void OnDisable()
        {
            _panning = false;
        }
    }

    internal sealed class TimelineWheelInput : MonoBehaviour, IScrollHandler
    {
        private TimelinePanel _panel;
        public void Initialize(TimelinePanel panel) => _panel = panel;

        public void OnScroll(PointerEventData eventData)
        {
            if (_panel == null) return;
            if (ShortcutInput.CtrlPressed)
            {
                if (eventData.scrollDelta.y > 0.001f) _panel.App.ZoomIn();
                else if (eventData.scrollDelta.y < -0.001f) _panel.App.ZoomOut();
            }
            else
            {
                _panel.ScrollVertical(eventData.scrollDelta.y);
            }
            eventData.Use();
        }
    }

    internal sealed class PlayheadDragInput : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        private LightingScenarioApp _app;
        private RectTransform _content;
        public void Initialize(LightingScenarioApp app, RectTransform content) { _app = app; _content = content; }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) SetTime(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) SetTime(eventData);
        }

        private void SetTime(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _content, eventData.position, eventData.pressEventCamera, out var local)) return;
            var x = local.x + _content.pivot.x * _content.rect.width;
            _app.SetCurrentTime(x / _app.Document.Data.editorSettings.pixelsPerSecond);
        }
    }

    internal sealed class TimelineRulerInput : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        private LightingScenarioApp _app;
        private RectTransform _rt;
        public void Initialize(LightingScenarioApp app, RectTransform rt) { _app = app; _rt = rt; }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) SetTime(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) SetTime(eventData);
        }

        private void SetTime(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rt, eventData.position, eventData.pressEventCamera, out var local)) return;
            var x = local.x + _rt.pivot.x * _rt.rect.width;
            _app.SetCurrentTime(x / _app.Document.Data.editorSettings.pixelsPerSecond);
        }
    }

    internal sealed class TrackColorInput : MonoBehaviour, IPointerClickHandler
    {
        private LightingScenarioApp _app;
        private string _unitId;
        private RectTransform _rt;
        public void Initialize(LightingScenarioApp app, string unitId, RectTransform rt)
        {
            _app = app;
            _unitId = unitId;
            _rt = rt;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (eventData.clickCount < 2)
            {
                _app.SelectUnit(_unitId);
                return;
            }

            var unit = _app.Document.FindUnit(_unitId);
            if (unit == null || unit.track.locked) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rt, eventData.position, eventData.pressEventCamera, out var local)) return;
            var x = local.x + _rt.pivot.x * _rt.rect.width;
            _app.CreateColorKeyframe(
                _unitId,
                x / _app.Document.Data.editorSettings.pixelsPerSecond);
        }
    }

    internal sealed class TrackLabelClick : MonoBehaviour, IPointerClickHandler
    {
        private LightingScenarioApp _app;
        private string _unitId;
        public void Initialize(LightingScenarioApp app, string unitId) { _app = app; _unitId = unitId; }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
                _app.SelectUnit(_unitId);
        }
    }

    internal sealed class TimelineMarqueeSelectInput : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private TimelinePanel _panel;
        private bool _dragging;
        private Vector2 _startScreen;
        private Camera _eventCamera;
        private bool _additive;

        public void Initialize(TimelinePanel panel) => _panel = panel;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_panel == null || eventData.button != PointerEventData.InputButton.Left) return;
            _dragging = true;
            _startScreen = eventData.position;
            _eventCamera = eventData.pressEventCamera;
            _additive = ShortcutInput.CtrlPressed;
            _panel.BeginMarqueeSelection(_startScreen, _eventCamera);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _panel == null) return;
            _panel.UpdateMarqueeSelection(_startScreen, eventData.position, _eventCamera);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging || _panel == null) return;
            _dragging = false;
            _panel.EndMarqueeSelection(_startScreen, eventData.position, _additive);
        }

        private void OnDisable()
        {
            _dragging = false;
            _panel?.CancelMarqueeSelection();
        }
    }

    internal sealed class ColorKeyframeView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private LightingScenarioApp _app;
        private string _unitId;
        private string _keyframeId;
        private RectTransform _rt;
        private Image _image;
        private Outline _outline;
        private string _beforeDrag;
        private bool _dragging;
        private Vector2 _dragStartScreen;
        private Dictionary<string, float> _originalTimes;
        private float _primaryOriginalTime;

        internal RectTransform RectTransform => _rt;

        public void Initialize(LightingScenarioApp app, string unitId, string keyframeId)
        {
            _app = app;
            _unitId = unitId;
            _keyframeId = keyframeId;
            _rt = (RectTransform)transform;
            _rt.anchorMin = _rt.anchorMax = new Vector2(0f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = new Vector2(14f, 14f);
            _rt.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var key = _app.Document.FindColorKeyframe(unitId, keyframeId);
            _image = UiFactory.AddImage(gameObject, key != null ? key.color.ToUnityColor() : Color.black);
            _image.raycastTarget = true;
            _outline = gameObject.AddComponent<Outline>();
            _outline.effectColor = new Color(1f, 0.85f, 0.15f, 1f);
            _outline.effectDistance = new Vector2(2f, 2f);
            RefreshPosition();
            RefreshSelection();
        }

        public void RefreshPosition()
        {
            var key = _app.Document.FindColorKeyframe(_unitId, _keyframeId);
            if (key == null) return;
            _rt.anchoredPosition = new Vector2(
                key.time * _app.Document.Data.editorSettings.pixelsPerSecond,
                0f);
            _image.color = key.color.ToUnityColor();
        }

        public void RefreshSelection()
        {
            if (_outline == null || _image == null) return;
            var selected = _app.IsColorKeyframeSelected(_keyframeId);
            if (selected)
            {
                _outline.enabled = true;
                _outline.effectColor = new Color(1f, 0.82f, 0.12f, 1f);
                _outline.effectDistance = new Vector2(2f, 2f);
                return;
            }

            var c = _image.color;
            var luminance = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            _outline.enabled = true;
            _outline.effectColor = luminance < 0.25f
                ? new Color(0.78f, 0.78f, 0.78f, 1f)
                : new Color(0.05f, 0.05f, 0.05f, 1f);
            _outline.effectDistance = new Vector2(1.2f, 1.2f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                var additive = ShortcutInput.CtrlPressed;
                if (eventData.clickCount >= 2)
                {
                    // Do not toggle an already selected keyframe off on the second click
                    // when Ctrl multi-selection is active. Double-click is reserved for color edit.
                    if (!_app.IsColorKeyframeSelected(_keyframeId))
                        _app.SelectColorKeyframe(_unitId, _keyframeId, additive);
                    _app.OpenColorPickerForSelection(_rt);
                }
                else
                {
                    _app.SelectColorKeyframe(_unitId, _keyframeId, additive);
                }
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                if (!_app.IsColorKeyframeSelected(_keyframeId))
                    _app.SelectColorKeyframe(_unitId, _keyframeId, false);
                var unit = _app.Document.FindUnit(_unitId);
                if (unit == null || unit.track.locked) return;
                var label = _app.SelectedColorKeyframeIds.Count > 1
                    ? "Delete Selected Keyframes"
                    : "Delete Color Keyframe";
                _app.ShowContext(eventData.position, label, _app.DeleteSelectedColorKeyframes);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            var unit = _app.Document.FindUnit(_unitId);
            if (unit == null || unit.track.locked) return;

            if (!_app.IsColorKeyframeSelected(_keyframeId))
            {
                var additive = ShortcutInput.CtrlPressed;
                _app.SelectColorKeyframe(_unitId, _keyframeId, additive, false);
            }

            _originalTimes = new Dictionary<string, float>();
            foreach (var id in _app.SelectedColorKeyframeIds)
            {
                var selectedUnit = _app.Document.FindUnitForColorKeyframe(id);
                var key = _app.Document.FindColorKeyframe(id);
                if (selectedUnit == null || key == null || selectedUnit.track.locked)
                {
                    _originalTimes = null;
                    return;
                }
                _originalTimes[id] = key.time;
            }

            if (!_originalTimes.TryGetValue(_keyframeId, out _primaryOriginalTime)) return;
            _dragging = true;
            _dragStartScreen = eventData.position;
            _beforeDrag = _app.Document.CaptureState();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || _originalTimes == null || _originalTimes.Count == 0) return;
            var canvas = GetComponentInParent<Canvas>();
            var scale = canvas != null ? canvas.scaleFactor : 1f;
            var delta = (eventData.position.x - _dragStartScreen.x)
                        / Mathf.Max(0.0001f, scale)
                        / Mathf.Max(0.0001f, _app.Document.Data.editorSettings.pixelsPerSecond);

            var minOriginal = _originalTimes.Values.Min();
            var maxOriginal = _originalTimes.Values.Max();
            delta = Mathf.Clamp(delta, -minOriginal, _app.Document.Data.metadata.duration - maxOriginal);

            var desiredPrimary = _primaryOriginalTime + delta;
            var snappedPrimary = _app.Document.SnapColorKeyframeTime(
                desiredPrimary,
                _originalTimes.Keys);
            var adjusted = snappedPrimary - _primaryOriginalTime;
            adjusted = Mathf.Clamp(adjusted, -minOriginal, _app.Document.Data.metadata.duration - maxOriginal);

            var proposed = new Dictionary<string, float>();
            foreach (var pair in _originalTimes)
                proposed[pair.Key] = pair.Value + adjusted;

            if (_app.SetColorKeyframeTimesNoHistory(proposed, out _))
                _app.RefreshTimelineGeometry();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            _app.CommitExternalEdit(_beforeDrag);
            _beforeDrag = null;
            _originalTimes = null;
        }

        private void OnDisable()
        {
            if (_dragging)
            {
                _dragging = false;
                _app?.CommitExternalEdit(_beforeDrag);
            }
            _beforeDrag = null;
            _originalTimes = null;
        }
    }
}
