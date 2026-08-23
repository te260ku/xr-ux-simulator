using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LightingScenarioTool
{
    internal sealed class TimelinePanel : MonoBehaviour
    {
        private const float LabelWidth = 340f;
        private const float SectionHeaderHeight = 32f;
        private const float RulerHeight = 32f;
        private const float RowHeight = 40f;
        private const float LaneHeight = 30f;
        private const float ScrollbarHeight = 10f;

        private LightingScenarioApp _app;

        // These references are serialized intentionally. The timeline is editor-authored UI,
        // so runtime behavior must not depend on a fixed Transform path after the user
        // rearranges or renames objects in the Hierarchy.
        [SerializeField] private RectTransform _rulerViewport;
        [SerializeField] private RectTransform _rulerContent;
        [SerializeField] private RectTransform _labelsViewport;
        [SerializeField] private RectTransform _labelsContent;
        [SerializeField] private RectTransform _timeViewport;
        [SerializeField] private RectTransform _timeContent;
        [SerializeField] private RectTransform _marqueeSelection;
        private RectTransform _playhead;
        private RectTransform _rulerPlayhead;
        [SerializeField] private Scrollbar _horizontalScrollbar;
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
        private readonly Dictionary<string, Image> _trackAccentLines = new Dictionary<string, Image>();

        internal LightingScenarioApp App => _app;

        public void Initialize(LightingScenarioApp app)
        {
            _app = app;

            if (!BindExistingChrome())
            {
                Debug.LogError(
                    "Timeline UI hierarchy is incomplete. " +
                    "Run Tools > Lighting Scenario > Build Complete UI In Current Scene in Edit Mode.", this);
                enabled = false;
                return;
            }

            var wheel = GetComponent<TimelineWheelInput>();
            var pan = GetComponent<TimelineMiddleMousePanInput>();
            var marquee = _timeViewport.GetComponent<TimelineMarqueeSelectInput>();
            if (wheel == null || pan == null || marquee == null)
            {
                Debug.LogError(
                    "Timeline input components are incomplete. " +
                    "Run Tools > Lighting Scenario > Build Complete UI In Current Scene in Edit Mode.", this);
                enabled = false;
                return;
            }

            enabled = true;
            wheel.Initialize(this);
            pan.Initialize(this);
            marquee.Initialize(this);

            _horizontalScrollbar.onValueChanged.RemoveListener(OnHorizontalScrollbarChanged);
            _horizontalScrollbar.onValueChanged.AddListener(OnHorizontalScrollbarChanged);
        }

        private bool BindExistingChrome()
        {
            // Prefer serialized references. Once the editable UI has been built these keep
            // working even if the user moves or renames objects in the Hierarchy.
            if (HasRequiredChrome()) return true;

            // Migration path for scenes created by previous revisions. First try the
            // original paths, then fall back to exact-name recursive lookup so moving a
            // viewport into a layout container does not break runtime initialization.
            _rulerViewport ??= transform.Find("RulerViewport") as RectTransform;
            _rulerViewport ??= FindDescendant<RectTransform>(transform, "RulerViewport");

            _rulerContent ??= _rulerViewport != null ? _rulerViewport.Find("RulerContent") as RectTransform : null;
            _rulerContent ??= FindDescendant<RectTransform>(transform, "RulerContent");

            _labelsViewport ??= transform.Find("LabelsViewport") as RectTransform;
            _labelsViewport ??= FindDescendant<RectTransform>(transform, "LabelsViewport");

            _labelsContent ??= _labelsViewport != null ? _labelsViewport.Find("LabelsContent") as RectTransform : null;
            _labelsContent ??= FindDescendant<RectTransform>(transform, "LabelsContent");

            _timeViewport ??= transform.Find("TimeViewport") as RectTransform;
            _timeViewport ??= FindDescendant<RectTransform>(transform, "TimeViewport");

            _timeContent ??= _timeViewport != null ? _timeViewport.Find("TimeContent") as RectTransform : null;
            _timeContent ??= FindDescendant<RectTransform>(transform, "TimeContent");

            _marqueeSelection ??= _timeViewport != null ? _timeViewport.Find("MarqueeSelection") as RectTransform : null;
            _marqueeSelection ??= FindDescendant<RectTransform>(transform, "MarqueeSelection");

            if (_horizontalScrollbar == null)
            {
                var scrollbarTransform = FindDescendant(transform, "HorizontalScrollbar");
                _horizontalScrollbar = scrollbarTransform != null
                    ? scrollbarTransform.GetComponent<Scrollbar>()
                    : GetComponentInChildren<Scrollbar>(true);
            }

            return HasRequiredChrome();
        }

        private bool HasRequiredChrome()
        {
            return _rulerViewport != null && _rulerContent != null &&
                   _labelsViewport != null && _labelsContent != null &&
                   _timeViewport != null && _timeContent != null &&
                   _marqueeSelection != null && _horizontalScrollbar != null;
        }

#if UNITY_EDITOR
        internal void EditorBuildDefaultHierarchy(LightingScenarioApp app)
        {
            _app = app;
            EnsureChrome();

            if (GetComponent<TimelineWheelInput>() == null)
                gameObject.AddComponent<TimelineWheelInput>();
            if (GetComponent<TimelineMiddleMousePanInput>() == null)
                gameObject.AddComponent<TimelineMiddleMousePanInput>();
            if (_timeViewport != null && _timeViewport.GetComponent<TimelineMarqueeSelectInput>() == null)
                _timeViewport.gameObject.AddComponent<TimelineMarqueeSelectInput>();
        }

        private bool EnsureChrome()
        {
            BindExistingChrome();

            // Preserve an authored Timeline whenever possible. Only recreate the optional
            // decorative header/corner when this panel has no recognizable timeline chrome.
            var hasExistingTimelineChrome = _rulerViewport != null || _labelsViewport != null ||
                                            _timeViewport != null || _horizontalScrollbar != null ||
                                            FindDescendant(transform, "TimelineSectionHeader") != null ||
                                            FindDescendant(transform, "Corner") != null;
            if (!hasExistingTimelineChrome)
                EnsureDecorativeChrome();

            EnsureRulerHierarchy();
            EnsureLabelsHierarchy();
            EnsureTimeHierarchy();
            EnsureHorizontalScrollbar();
            EnsureTimelineDividers();

            BindExistingChrome();
            return HasRequiredChrome();
        }

        private void EnsureDecorativeChrome()
        {
            if (FindDescendant(transform, "TimelineSectionHeader") == null)
            {
                var sectionHeader = UiFactory.CreateUIObject("TimelineSectionHeader", transform);
                var sectionHeaderRt = (RectTransform)sectionHeader.transform;
                sectionHeaderRt.anchorMin = new Vector2(0f, 1f);
                sectionHeaderRt.anchorMax = new Vector2(1f, 1f);
                sectionHeaderRt.pivot = new Vector2(0.5f, 1f);
                sectionHeaderRt.sizeDelta = new Vector2(0f, SectionHeaderHeight);
                UiFactory.AddImage(sectionHeader, AppTheme.Panel).raycastTarget = false;
                var title = UiFactory.CreateSectionTitle(sectionHeader.transform, "Timeline", 140f);
                var titleRt = (RectTransform)title.transform;
                titleRt.anchorMin = Vector2.zero;
                titleRt.anchorMax = Vector2.one;
                titleRt.offsetMin = new Vector2(12f, 0f);
                titleRt.offsetMax = new Vector2(-12f, 0f);
            }

            if (FindDescendant(transform, "Corner") == null)
            {
                var corner = UiFactory.CreateUIObject("Corner", transform);
                var cornerRt = (RectTransform)corner.transform;
                cornerRt.anchorMin = cornerRt.anchorMax = new Vector2(0f, 1f);
                cornerRt.pivot = new Vector2(0f, 1f);
                cornerRt.anchoredPosition = new Vector2(0f, -SectionHeaderHeight);
                cornerRt.sizeDelta = new Vector2(LabelWidth, RulerHeight);
                UiFactory.AddImage(corner, AppTheme.Elevated);
                var cornerTextGo = UiFactory.CreateUIObject("CornerText", corner.transform);
                UiFactory.Stretch((RectTransform)cornerTextGo.transform);
                var cornerText = UiFactory.AddText(cornerTextGo, "Tracks", AppTheme.SecondarySize, TextAnchor.MiddleLeft);
                cornerText.color = AppTheme.TextSecondary;
                cornerText.margin = new Vector4(12f, 0f, 4f, 0f);
                cornerText.raycastTarget = false;
            }

            if (FindDescendant(transform, "LowerLeft") == null)
            {
                var lowerLeft = UiFactory.CreateUIObject("LowerLeft", transform);
                var lowerLeftRt = (RectTransform)lowerLeft.transform;
                lowerLeftRt.anchorMin = new Vector2(0f, 0f);
                lowerLeftRt.anchorMax = new Vector2(0f, 0f);
                lowerLeftRt.pivot = Vector2.zero;
                lowerLeftRt.anchoredPosition = Vector2.zero;
                lowerLeftRt.sizeDelta = new Vector2(LabelWidth, ScrollbarHeight);
                UiFactory.AddImage(lowerLeft, AppTheme.Panel).raycastTarget = false;
            }
        }

        private void EnsureRulerHierarchy()
        {
            if (_rulerViewport == null)
                _rulerViewport = FindDescendant<RectTransform>(transform, "RulerViewport");
            if (_rulerViewport == null)
            {
                _rulerViewport = CreateViewport("RulerViewport", transform, AppTheme.Elevated);
                _rulerViewport.anchorMin = new Vector2(0f, 1f);
                _rulerViewport.anchorMax = new Vector2(1f, 1f);
                _rulerViewport.pivot = new Vector2(0.5f, 1f);
                _rulerViewport.offsetMin = new Vector2(LabelWidth, -(SectionHeaderHeight + RulerHeight));
                _rulerViewport.offsetMax = new Vector2(0f, -SectionHeaderHeight);
            }
            EnsureViewportComponents(_rulerViewport, AppTheme.Elevated);

            if (_rulerContent == null)
                _rulerContent = FindDescendant<RectTransform>(_rulerViewport, "RulerContent");
            if (_rulerContent == null)
                _rulerContent = CreateTopLeftContent("RulerContent", _rulerViewport);
        }

        private void EnsureLabelsHierarchy()
        {
            if (_labelsViewport == null)
                _labelsViewport = FindDescendant<RectTransform>(transform, "LabelsViewport");
            if (_labelsViewport == null)
            {
                _labelsViewport = CreateViewport("LabelsViewport", transform, AppTheme.Panel);
                _labelsViewport.anchorMin = new Vector2(0f, 0f);
                _labelsViewport.anchorMax = new Vector2(0f, 1f);
                _labelsViewport.pivot = new Vector2(0f, 0.5f);
                _labelsViewport.offsetMin = new Vector2(0f, ScrollbarHeight);
                _labelsViewport.offsetMax = new Vector2(LabelWidth, -(SectionHeaderHeight + RulerHeight));
            }
            EnsureViewportComponents(_labelsViewport, AppTheme.Panel);

            if (_labelsContent == null)
                _labelsContent = FindDescendant<RectTransform>(_labelsViewport, "LabelsContent");
            if (_labelsContent == null)
                _labelsContent = CreateTopLeftContent("LabelsContent", _labelsViewport);
        }

        private void EnsureTimeHierarchy()
        {
            if (_timeViewport == null)
                _timeViewport = FindDescendant<RectTransform>(transform, "TimeViewport");
            if (_timeViewport == null)
            {
                _timeViewport = CreateViewport("TimeViewport", transform, AppTheme.TrackEven);
                _timeViewport.anchorMin = Vector2.zero;
                _timeViewport.anchorMax = Vector2.one;
                _timeViewport.offsetMin = new Vector2(LabelWidth, ScrollbarHeight);
                _timeViewport.offsetMax = new Vector2(0f, -(SectionHeaderHeight + RulerHeight));
            }
            EnsureViewportComponents(_timeViewport, AppTheme.TrackEven);

            if (_timeContent == null)
                _timeContent = FindDescendant<RectTransform>(_timeViewport, "TimeContent");
            if (_timeContent == null)
                _timeContent = CreateTopLeftContent("TimeContent", _timeViewport);

            if (_marqueeSelection == null)
                _marqueeSelection = FindDescendant<RectTransform>(_timeViewport, "MarqueeSelection");
            if (_marqueeSelection == null)
                BuildMarqueeSelection();
            else
                EnsureMarqueeVisual();
        }

        private static void EnsureViewportComponents(RectTransform viewport, Color defaultColor)
        {
            if (viewport == null) return;
            var image = viewport.GetComponent<Image>() ?? UiFactory.AddImage(viewport.gameObject, defaultColor);
            image.raycastTarget = true;
            if (viewport.GetComponent<RectMask2D>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();
        }

        private void EnsureMarqueeVisual()
        {
            if (_marqueeSelection == null) return;
            var image = _marqueeSelection.GetComponent<Image>() ??
                        UiFactory.AddImage(_marqueeSelection.gameObject,
                            new Color(AppTheme.Accent.r, AppTheme.Accent.g, AppTheme.Accent.b, 0.16f));
            image.raycastTarget = false;
            var outline = _marqueeSelection.GetComponent<Outline>() ?? _marqueeSelection.gameObject.AddComponent<Outline>();
            outline.effectColor = AppTheme.Accent;
            outline.effectDistance = new Vector2(1f, 1f);
        }

        private void EnsureHorizontalScrollbar()
        {
            if (_horizontalScrollbar == null)
            {
                var existing = FindDescendant(transform, "HorizontalScrollbar");
                if (existing != null)
                    _horizontalScrollbar = existing.GetComponent<Scrollbar>() ?? existing.gameObject.AddComponent<Scrollbar>();
            }

            if (_horizontalScrollbar == null)
            {
                BuildHorizontalScrollbar();
                return;
            }

            var scrollbarGo = _horizontalScrollbar.gameObject;
            var background = scrollbarGo.GetComponent<Image>() ?? UiFactory.AddImage(scrollbarGo, AppTheme.Background);
            background.raycastTarget = true;

            var slidingArea = scrollbarGo.transform.Find("Sliding Area") as RectTransform;
            if (slidingArea == null)
            {
                var slidingAreaGo = UiFactory.CreateUIObject("Sliding Area", scrollbarGo.transform);
                slidingArea = (RectTransform)slidingAreaGo.transform;
                UiFactory.Stretch(slidingArea);
                slidingArea.offsetMin = new Vector2(1f, 2f);
                slidingArea.offsetMax = new Vector2(-1f, -2f);
            }

            var handle = slidingArea.Find("Handle") as RectTransform;
            if (handle == null)
            {
                var handleGo = UiFactory.CreateUIObject("Handle", slidingArea);
                handle = (RectTransform)handleGo.transform;
                UiFactory.Stretch(handle);
            }

            var handleImage = handle.GetComponent<Image>() ?? UiFactory.AddImage(handle.gameObject, AppTheme.TextSecondary);
            _horizontalScrollbar.direction = Scrollbar.Direction.LeftToRight;
            _horizontalScrollbar.targetGraphic = handleImage;
            _horizontalScrollbar.handleRect = handle;
        }

        private void EnsureTimelineDividers()
        {
            EnsureHorizontalDivider("HeaderDivider", SectionHeaderHeight);
            EnsureHorizontalDivider("RulerDivider", SectionHeaderHeight + RulerHeight);
            EnsureHorizontalDividerFromBottom("ScrollbarDivider", ScrollbarHeight);

            var vertical = FindDescendant<RectTransform>(transform, "TrackColumnDivider");
            if (vertical == null)
            {
                var go = UiFactory.CreateUIObject("TrackColumnDivider", transform);
                vertical = (RectTransform)go.transform;
                UiFactory.AddImage(go, AppTheme.Divider).raycastTarget = false;
            }

            vertical.anchorMin = new Vector2(0f, 0f);
            vertical.anchorMax = new Vector2(0f, 1f);
            vertical.pivot = new Vector2(0.5f, 0.5f);
            vertical.offsetMin = new Vector2(LabelWidth - 0.5f, ScrollbarHeight);
            vertical.offsetMax = new Vector2(LabelWidth + 0.5f, -SectionHeaderHeight);
            vertical.SetAsLastSibling();
        }

        private void EnsureHorizontalDivider(string name, float yFromTop)
        {
            var line = FindDescendant<RectTransform>(transform, name);
            if (line == null)
            {
                var go = UiFactory.CreateUIObject(name, transform);
                line = (RectTransform)go.transform;
                UiFactory.AddImage(go, AppTheme.Divider).raycastTarget = false;
            }

            line.anchorMin = new Vector2(0f, 1f);
            line.anchorMax = new Vector2(1f, 1f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = new Vector2(0f, -yFromTop);
            line.sizeDelta = new Vector2(0f, 1f);
            line.SetAsLastSibling();
        }

        private void EnsureHorizontalDividerFromBottom(string name, float yFromBottom)
        {
            var line = FindDescendant<RectTransform>(transform, name);
            if (line == null)
            {
                var go = UiFactory.CreateUIObject(name, transform);
                line = (RectTransform)go.transform;
                UiFactory.AddImage(go, AppTheme.Divider).raycastTarget = false;
            }

            line.anchorMin = new Vector2(0f, 0f);
            line.anchorMax = new Vector2(1f, 0f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.anchoredPosition = new Vector2(0f, yFromBottom);
            line.sizeDelta = new Vector2(0f, 1f);
            line.SetAsLastSibling();
        }

#endif

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


#if UNITY_EDITOR
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

            var image = UiFactory.AddImage(go, new Color(AppTheme.Accent.r, AppTheme.Accent.g, AppTheme.Accent.b, 0.16f));
            image.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = AppTheme.Accent;
            outline.effectDistance = new Vector2(1f, 1f);
            go.SetActive(false);
        }
#endif

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

#if UNITY_EDITOR
        private void BuildHorizontalScrollbar()
        {
            var go = UiFactory.CreateUIObject("HorizontalScrollbar", transform);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(LabelWidth + 4f, 1f);
            rt.offsetMax = new Vector2(-4f, ScrollbarHeight - 1f);

            var background = UiFactory.AddImage(go, AppTheme.Background);
            var slidingArea = UiFactory.CreateUIObject("Sliding Area", go.transform);
            var slidingRt = (RectTransform)slidingArea.transform;
            UiFactory.Stretch(slidingRt);
            slidingRt.offsetMin = new Vector2(1f, 2f);
            slidingRt.offsetMax = new Vector2(-1f, -2f);

            var handleGo = UiFactory.CreateUIObject("Handle", slidingArea.transform);
            var handleRt = (RectTransform)handleGo.transform;
            UiFactory.Stretch(handleRt);
            var handleImage = UiFactory.AddImage(handleGo, AppTheme.TextSecondary);

            _horizontalScrollbar = go.AddComponent<Scrollbar>();
            _horizontalScrollbar.direction = Scrollbar.Direction.LeftToRight;
            _horizontalScrollbar.targetGraphic = handleImage;
            _horizontalScrollbar.handleRect = handleRt;
            _horizontalScrollbar.colors = new ColorBlock
            {
                normalColor = AppTheme.TextSecondary,
                highlightedColor = AppTheme.TextPrimary,
                pressedColor = AppTheme.Accent,
                selectedColor = AppTheme.TextPrimary,
                disabledColor = AppTheme.TextDisabled,
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            background.raycastTarget = true;
        }
#endif

        public void Rebuild()
        {
            if (!HasRequiredChrome())
            {
                Debug.LogError(
                    "Timeline hierarchy is incomplete. " +
                    "Regenerate the UI from Tools > Lighting Scenario > Build Complete UI In Current Scene.", this);
                return;
            }

            ClearChildren(_rulerContent);
            ClearChildren(_labelsContent);
            ClearChildren(_timeContent);
            _keyframeViews.Clear();
            _trackLabelImages.Clear();
            _trackLabelBaseColors.Clear();
            _trackTimeImages.Clear();
            _trackTimeBaseColors.Clear();
            _trackAccentLines.Clear();

            var duration = Mathf.Max(0.001f, _app.Document.Data.metadata.duration);
            var pps = _app.Document.Data.editorSettings.pixelsPerSecond;
            _timeContentWidth = TimelineCoordinates.ContentWidth(duration, pps);
            _rowsContentHeight = Mathf.Max(1, _app.Document.Data.lightingUnits.Count) * RowHeight;
            _rulerContent.sizeDelta = new Vector2(_timeContentWidth, RulerHeight);
            _labelsContent.sizeDelta = new Vector2(LabelWidth, _rowsContentHeight);
            _timeContent.sizeDelta = new Vector2(_timeContentWidth, _rowsContentHeight);

            BuildRuler();
            for (var i = 0; i < _app.Document.Data.lightingUnits.Count; i++)
                BuildTrackRow(_app.Document.Data.lightingUnits[i], i);
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
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
        }

        private void BuildRuler()
        {
            var interactionGo = Instantiate(_app.UiPrefabs.RulerInteractionPrefab, _rulerContent);
            interactionGo.name = "RulerInteraction";
            var interactionRt = (RectTransform)interactionGo.transform;
            interactionRt.sizeDelta = new Vector2(_timeContentWidth, RulerHeight);
            var rulerInput = interactionGo.GetComponent<TimelineRulerInput>();
            if (rulerInput != null) rulerInput.Initialize(_app, interactionRt);

            var pps = Mathf.Max(0.001f, _app.Document.Data.editorSettings.pixelsPerSecond);
            var duration = Mathf.Max(0f, _app.Document.Data.metadata.duration);
            var interval = GetMajorTickInterval(pps);
            var ticks = interactionGo.GetComponentInChildren<TimelineRulerTicksGraphic>(true);
            if (ticks != null) ticks.Configure(duration, pps, interval);

            var count = Mathf.CeilToInt(duration / interval);
            for (var i = 0; i <= count; i++)
            {
                var time = Mathf.Min(duration, i * interval);
                var x = TimelineCoordinates.TimeToX(time, pps);
                var tickLabel = Instantiate(_app.UiPrefabs.RulerLabelPrefab, _rulerContent);
                tickLabel.gameObject.name = "TickLabel_" + i;
                var textRt = (RectTransform)tickLabel.transform;
                textRt.anchoredPosition = new Vector2(x, -2f);
                tickLabel.text = FormatTickTime(time, interval);

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
            var rowGo = Instantiate(_app.UiPrefabs.TrackLabelPrefab, _labelsContent);
            rowGo.name = "Label_" + unit.unitId;
            rowGo.SetActive(true);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.anchoredPosition = new Vector2(0f, -index * RowHeight);
            rowRt.sizeDelta = new Vector2(LabelWidth, RowHeight);

            var selected = unit.unitId == _app.SelectedUnitId;
            var baseColor = index % 2 == 0 ? AppTheme.TrackHeaderEven : AppTheme.TrackHeaderOdd;
            var rowImage = rowGo.GetComponent<Image>();
            rowImage.color = selected ? AppTheme.AccentTint : baseColor;
            _trackLabelImages[unit.unitId] = rowImage;
            _trackLabelBaseColors[unit.unitId] = baseColor;
            ConfigureRowSeparator(rowGo.transform);
            var click = rowGo.GetComponent<TrackLabelClick>();
            click.Initialize(_app, unit.unitId);

            var accent = rowGo.transform.Find("SelectionAccent");
            var accentImage = accent != null ? accent.GetComponent<Image>() : null;
            if (accentImage != null)
            {
                accentImage.gameObject.SetActive(selected);
                _trackAccentLines[unit.unitId] = accentImage;
            }

            var nameTransform = rowGo.transform.Find("TrackNameInput");
            var nameInput = nameTransform != null ? nameTransform.GetComponent<TMP_InputField>() : null;
            if (nameInput != null)
            {
                nameInput.SetTextWithoutNotify(unit.displayName);
                var capturedUnitId = unit.unitId;
                nameInput.onEndEdit.AddListener(value => _app.SetUnitName(capturedUnitId, value));
            }

            var lockToggle = rowGo.transform.Find("Toggle_Lock")?.GetComponent<Toggle>();
            if (lockToggle != null)
            {
                lockToggle.SetIsOnWithoutNotify(unit.track.locked);
                lockToggle.onValueChanged.AddListener(v => _app.SetTrackLocked(unit.unitId, v));
            }
            var muteToggle = rowGo.transform.Find("Toggle_Mute")?.GetComponent<Toggle>();
            if (muteToggle != null)
            {
                muteToggle.SetIsOnWithoutNotify(unit.track.muted);
                muteToggle.onValueChanged.AddListener(v => _app.SetTrackMuted(unit.unitId, v));
            }
            var upButton = rowGo.transform.Find("Button_▲")?.GetComponent<Button>();
            if (upButton != null)
            {
                upButton.onClick.AddListener(() => _app.MoveTrack(unit.unitId, -1));
            }
            var downButton = rowGo.transform.Find("Button_▼")?.GetComponent<Button>();
            if (downButton != null)
            {
                downButton.onClick.AddListener(() => _app.MoveTrack(unit.unitId, 1));
            }
        }

        private void BuildTrackTimeArea(LightingUnitData unit, int index)
        {
            var rowGo = Instantiate(_app.UiPrefabs.TrackTimePrefab, _timeContent);
            rowGo.name = "Track_" + unit.unitId;
            rowGo.SetActive(true);
            var rowRt = (RectTransform)rowGo.transform;
            rowRt.anchoredPosition = new Vector2(0f, -index * RowHeight);
            rowRt.sizeDelta = new Vector2(_timeContentWidth, RowHeight);
            var timeBaseColor = index % 2 == 0 ? AppTheme.TrackEven : AppTheme.TrackOdd;
            var timeImage = rowGo.GetComponent<Image>();
            timeImage.color = unit.unitId == _app.SelectedUnitId ? AppTheme.AccentTintSoft : timeBaseColor;
            timeImage.raycastTarget = false;
            _trackTimeImages[unit.unitId] = timeImage;
            _trackTimeBaseColors[unit.unitId] = timeBaseColor;

            var pps = Mathf.Max(0.001f, _app.Document.Data.editorSettings.pixelsPerSecond);
            var duration = Mathf.Max(0f, _app.Document.Data.metadata.duration);
            var interval = GetMajorTickInterval(pps);
            var gridRect = rowGo.transform.Find("GridGraphic") as RectTransform;
            var grid = gridRect != null ? gridRect.GetComponent<TimelineTrackGridGraphic>() : null;
            if (gridRect == null || grid == null)
            {
                Debug.LogError(
                    "TimelineTrackTime prefab requires GridGraphic with TimelineTrackGridGraphic. " +
                    "Run Tools > Lighting Scenario > Build Complete UI In Current Scene to regenerate the prefab.",
                    rowGo);
                return;
            }

            // Keep the procedural grid exactly aligned to the instantiated row. The prefab is
            // authored at a nominal width, while runtime timeline width changes with duration/zoom.
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = Vector2.zero;
            gridRect.SetAsFirstSibling();
            grid.Configure(duration, pps, interval);

            ConfigureRowSeparator(rowGo.transform);

            var lane = rowGo.transform.Find("ColorKeyframeLane") as RectTransform;
            if (lane == null)
            {
                Debug.LogError("TimelineTrackTime prefab requires a ColorKeyframeLane child.", rowGo);
                return;
            }
            lane.sizeDelta = new Vector2(_timeContentWidth, LaneHeight);
            var input = lane.GetComponent<TrackColorInput>();
            if (input == null)
            {
                Debug.LogError("TimelineTrackTime prefab requires TrackColorInput on ColorKeyframeLane.", rowGo);
                return;
            }
            input.Initialize(_app, unit.unitId, lane);

            var gradientRect = lane.Find("ColorGradient") as RectTransform;
            if (gradientRect == null)
            {
                Debug.LogError(
                    "TimelineTrackTime prefab requires a ColorGradient child under ColorKeyframeLane. " +
                    "Run Tools > Lighting Scenario > Build Complete UI In Current Scene to regenerate the prefab.",
                    rowGo);
                return;
            }

            gradientRect.sizeDelta = new Vector2(_timeContentWidth, 5f);
            gradientRect.SetAsFirstSibling();
            var gradient = gradientRect.GetComponent<TimelineColorGradientGraphic>();
            if (gradient == null)
            {
                Debug.LogError(
                    "ColorGradient requires TimelineColorGradientGraphic. " +
                    "Run Tools > Lighting Scenario > Build Complete UI In Current Scene to regenerate the prefab.",
                    gradientRect);
                return;
            }
            gradient.Configure(unit.track.colorKeyframes, pps);

            foreach (var keyframe in unit.track.colorKeyframes)
            {
                var view = Instantiate(_app.UiPrefabs.ColorKeyframePrefab, lane);
                view.gameObject.name = "ColorKeyframe_" + keyframe.keyframeId;
                view.gameObject.SetActive(true);
                view.Initialize(_app, unit.unitId, keyframe.keyframeId);
                _keyframeViews[keyframe.keyframeId] = view;
            }
        }

        private static void ConfigureRowSeparator(Transform row)
        {
            var separator = row != null ? row.Find("BottomSeparator") as RectTransform : null;
            if (separator == null) return;

            separator.anchorMin = new Vector2(0f, 0f);
            separator.anchorMax = new Vector2(1f, 0f);
            separator.pivot = new Vector2(0.5f, 0f);
            separator.anchoredPosition = Vector2.zero;
            separator.sizeDelta = new Vector2(0f, 1f);
            separator.SetAsLastSibling();

            var image = separator.GetComponent<Image>();
            if (image != null)
            {
                image.color = AppTheme.Divider;
                image.raycastTarget = false;
                image.enabled = true;
            }
        }

        private void BuildPlayhead()
        {
            var bodyGo = Instantiate(_app.UiPrefabs.TimePlayheadPrefab, _timeContent);
            bodyGo.name = "Playhead";
            _playhead = (RectTransform)bodyGo.transform;
            _playhead.sizeDelta = new Vector2(12f, _rowsContentHeight);
            // The playhead line in the timeline body is display-only.
            // Scrubbing is intentionally limited to pointer-downs that start in RulerViewport.
            var bodyInput = bodyGo.GetComponent<PlayheadDragInput>();
            if (bodyInput != null) bodyInput.enabled = false;
            SetPlayheadRaycastTargets(bodyGo, false);
            var bodyLine = bodyGo.transform.Find("Line") as RectTransform;
            if (bodyLine != null)
            {
                bodyLine.anchorMin = new Vector2(0.5f, 0f);
                bodyLine.anchorMax = new Vector2(0.5f, 1f);
                bodyLine.pivot = new Vector2(0.5f, 0.5f);
                bodyLine.sizeDelta = new Vector2(2f, 0f);
            }
            _playhead.SetAsLastSibling();

            var rulerGo = Instantiate(_app.UiPrefabs.RulerPlayheadPrefab, _rulerContent);
            rulerGo.name = "RulerPlayhead";
            _rulerPlayhead = (RectTransform)rulerGo.transform;
            _rulerPlayhead.sizeDelta = new Vector2(12f, RulerHeight);
            var rulerInput = rulerGo.GetComponent<PlayheadDragInput>();
            if (rulerInput != null)
            {
                rulerInput.enabled = true;
                rulerInput.Initialize(_app, _rulerContent, _rulerViewport);
            }
            _rulerPlayhead.SetAsLastSibling();
        }

        private static void SetPlayheadRaycastTargets(GameObject playhead, bool raycastTarget)
        {
            if (playhead == null) return;
            var graphics = playhead.GetComponentsInChildren<Graphic>(true);
            foreach (var graphic in graphics)
                graphic.raycastTarget = raycastTarget;
        }

        public void RefreshPlayhead()
        {
            var x = TimelineCoordinates.TimeToX(
                _app.CurrentTime,
                _app.Document.Data.editorSettings.pixelsPerSecond);
            if (_playhead != null) _playhead.anchoredPosition = new Vector2(x, 0f);
            if (_rulerPlayhead != null) _rulerPlayhead.anchoredPosition = new Vector2(x, 0f);
        }

        public void RefreshGeometry()
        {
            foreach (var pair in _keyframeViews)
                pair.Value.RefreshPosition();
            RefreshPlayhead();
        }


        public void RefreshSelection()
        {
            foreach (var pair in _keyframeViews)
                pair.Value.RefreshSelection();

            foreach (var pair in _trackLabelImages)
            {
                if (pair.Value == null) continue;
                pair.Value.color = pair.Key == _app.SelectedUnitId
                    ? AppTheme.AccentTint
                    : _trackLabelBaseColors[pair.Key];
            }

            foreach (var pair in _trackTimeImages)
            {
                if (pair.Value == null) continue;
                pair.Value.color = pair.Key == _app.SelectedUnitId
                    ? AppTheme.AccentTintSoft
                    : _trackTimeBaseColors[pair.Key];
            }

            foreach (var pair in _trackAccentLines)
                if (pair.Value != null) pair.Value.gameObject.SetActive(pair.Key == _app.SelectedUnitId);
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
}
