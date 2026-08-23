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
    internal sealed class PreviewPanel : MonoBehaviour, IPointerClickHandler
    {
        private const float SectionHeaderHeight = 32f;
        private const float ToolbarHeight = 52f;

        private LightingScenarioApp _app;

        // Persist scene references so designers can freely move or rename the authored UI.
        [SerializeField] private RectTransform _contentRoot;
        [SerializeField] private RectTransform _lightsLayer;
        [SerializeField] private Image _backgroundImage;
        private Texture2D _backgroundTexture;
        private Sprite _backgroundSprite;
        private string _loadedBackgroundPath;
        [SerializeField] private Slider _lightSizeSlider;
        [SerializeField] private TMP_Text _lightSizeText;
        [SerializeField] private Button _selectImageButton;
        [SerializeField] private Toggle _showUnitNamesToggle;
        private bool _updatingToolbar;
        private readonly Dictionary<string, PreviewLightView> _views = new Dictionary<string, PreviewLightView>();

        public void Initialize(LightingScenarioApp app)
        {
            _app = app;

            if (!BindExistingChrome())
            {
                Debug.LogError(
                    "Preview UI hierarchy is incomplete. " +
                    "Run Tools > Lighting Scenario > Build Complete UI In Current Scene in Edit Mode.", this);
                enabled = false;
                return;
            }

            enabled = true;

            if (_lightSizeSlider != null)
            {
                _lightSizeSlider.minValue = ScenarioDataUtility.MinPreviewLightSize;
                _lightSizeSlider.maxValue = ScenarioDataUtility.MaxPreviewLightSize;
                _lightSizeSlider.SetValueWithoutNotify(_app.PreviewLightSize);
                _lightSizeSlider.onValueChanged.RemoveListener(OnLightSizeChanged);
                _lightSizeSlider.onValueChanged.AddListener(OnLightSizeChanged);
            }

            if (_selectImageButton == null)
            {
                var selectImage = FindDescendant(transform, "Button_Select Image");
                _selectImageButton = selectImage != null ? selectImage.GetComponent<Button>() : null;
            }
            if (_selectImageButton != null)
            {
                _selectImageButton.onClick.RemoveListener(_app.BrowsePreviewBackgroundImage);
                _selectImageButton.onClick.AddListener(_app.BrowsePreviewBackgroundImage);
            }

            if (_showUnitNamesToggle != null)
            {
                _showUnitNamesToggle.SetIsOnWithoutNotify(_app.ShowPreviewUnitNames);
                _showUnitNamesToggle.onValueChanged.RemoveListener(_app.SetPreviewUnitNamesVisible);
                _showUnitNamesToggle.onValueChanged.AddListener(_app.SetPreviewUnitNamesVisible);
            }
        }

        private void OnLightSizeChanged(float value)
        {
            if (_updatingToolbar) return;
            _app.SetPreviewLightSizeFromUi(value);
            if (_lightSizeText != null) _lightSizeText.text = value.ToString("0");
        }

        private bool BindExistingChrome()
        {
            if (HasRequiredChrome()) return true;

            _contentRoot ??= transform.Find("PreviewContent") as RectTransform;
            _contentRoot ??= FindDescendant<RectTransform>(transform, "PreviewContent");

            if (_backgroundImage == null)
            {
                var background = _contentRoot != null ? _contentRoot.Find("PreviewBackgroundImage") : null;
                background ??= FindDescendant(transform, "PreviewBackgroundImage");
                _backgroundImage = background != null ? background.GetComponent<Image>() : null;
            }

            _lightsLayer ??= _contentRoot != null ? _contentRoot.Find("LightsLayer") as RectTransform : null;
            _lightsLayer ??= FindDescendant<RectTransform>(transform, "LightsLayer");

            if (_lightSizeSlider == null)
            {
                var slider = FindDescendant(transform, "LightSizeSlider") ?? FindDescendant(transform, "Slider");
                _lightSizeSlider = slider != null ? slider.GetComponent<Slider>() : null;
            }

            if (_lightSizeText == null)
            {
                var value = FindDescendant(transform, "LightSizeValue");
                if (value != null) _lightSizeText = value.GetComponent<TMP_Text>();
            }

            if (_selectImageButton == null)
            {
                var selectImage = FindDescendant(transform, "Button_Select Image");
                _selectImageButton = selectImage != null ? selectImage.GetComponent<Button>() : null;
            }

            if (_showUnitNamesToggle == null)
            {
                var names = FindDescendant(transform, "Toggle_Unit Names");
                _showUnitNamesToggle = names != null ? names.GetComponent<Toggle>() : null;
            }

            return HasRequiredChrome();
        }

        private bool HasRequiredChrome()
        {
            return _contentRoot != null && _backgroundImage != null && _lightsLayer != null &&
                   _lightSizeSlider != null && _lightSizeText != null;
        }

#if UNITY_EDITOR
        internal void EditorBuildDefaultHierarchy(LightingScenarioApp app)
        {
            _app = app;
            EnsureChrome();
        }

        private bool EnsureChrome()
        {
            if (BindExistingChrome())
                return true;

            BuildChrome();
            BindExistingChrome();
            return HasRequiredChrome();
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
        private void BuildChrome()
        {
            var hasExistingPreviewChrome = FindDescendant(transform, "PreviewToolbar") != null ||
                                           FindDescendant(transform, "PreviewContent") != null ||
                                           FindDescendant(transform, "PreviewSectionHeader") != null;

            // Only create the decorative section header for a completely empty/default panel.
            // A partially authored/customized Preview should not regain optional chrome that a
            // designer intentionally removed.
            if (!hasExistingPreviewChrome)
                EnsureSectionHeader();

            EnsureToolbar();
            EnsureContentHierarchy();
        }

        private void EnsureSectionHeader()
        {
            if (FindDescendant(transform, "PreviewSectionHeader") != null)
                return;

            var sectionHeader = UiFactory.CreateUIObject("PreviewSectionHeader", transform);
            var sectionHeaderRt = (RectTransform)sectionHeader.transform;
            sectionHeaderRt.anchorMin = new Vector2(0f, 1f);
            sectionHeaderRt.anchorMax = new Vector2(1f, 1f);
            sectionHeaderRt.pivot = new Vector2(0.5f, 1f);
            sectionHeaderRt.sizeDelta = new Vector2(0f, SectionHeaderHeight);
            UiFactory.AddImage(sectionHeader, AppTheme.Panel).raycastTarget = false;
            CreateBottomDivider(sectionHeader.transform, "HeaderDivider");

            var title = UiFactory.CreateSectionTitle(sectionHeader.transform, "Preview", 120f);
            var titleRt = (RectTransform)title.transform;
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = new Vector2(12f, 0f);
            titleRt.offsetMax = new Vector2(-12f, 0f);
        }

        private void EnsureToolbar()
        {
            var toolbar = FindDescendant(transform, "PreviewToolbar");
            var createdToolbar = toolbar == null;
            if (createdToolbar)
            {
                var toolbarGo = UiFactory.CreateUIObject("PreviewToolbar", transform);
                toolbar = toolbarGo.transform;

                var toolbarRt = (RectTransform)toolbar;
                toolbarRt.anchorMin = new Vector2(0f, 1f);
                toolbarRt.anchorMax = new Vector2(1f, 1f);
                toolbarRt.pivot = new Vector2(0.5f, 1f);
                var headerHeight = FindDescendant(transform, "PreviewSectionHeader") != null
                    ? SectionHeaderHeight
                    : 0f;
                toolbarRt.anchoredPosition = new Vector2(0f, -headerHeight);
                toolbarRt.sizeDelta = new Vector2(0f, ToolbarHeight);
                UiFactory.AddImage(toolbarGo, AppTheme.Elevated);
                CreateBottomDivider(toolbarGo.transform, "ToolbarDivider");

                var toolbarLayout = toolbarGo.AddComponent<HorizontalLayoutGroup>();
                toolbarLayout.padding = new RectOffset(12, 12, 10, 10);
                toolbarLayout.spacing = AppTheme.SpacingS;
                toolbarLayout.childControlWidth = true;
                toolbarLayout.childControlHeight = true;
                toolbarLayout.childForceExpandWidth = false;
                toolbarLayout.childForceExpandHeight = true;
                toolbarLayout.childAlignment = TextAnchor.MiddleLeft;
            }

            if (_selectImageButton == null)
            {
                var selectImage = FindDescendant(toolbar, "Button_Select Image");
                _selectImageButton = selectImage != null ? selectImage.GetComponent<Button>() : null;
            }
            if (_selectImageButton == null)
            {
                if (FindDescendant(toolbar, "BackgroundLabel") == null)
                {
                    var backgroundLabel = UiFactory.CreateSecondaryLabel(toolbar, "Background", 72f);
                    backgroundLabel.gameObject.name = "BackgroundLabel";
                }

                _selectImageButton = UiFactory.CreateButton(
                    toolbar,
                    "Select Image",
                    null,
                    94f,
                    AppButtonStyle.Secondary);
            }

            if (createdToolbar && FindDescendant(toolbar, "ToolbarGap") == null)
            {
                var gap = UiFactory.CreateUIObject("ToolbarGap", toolbar);
                gap.AddComponent<LayoutElement>().preferredWidth = AppTheme.SpacingS;
            }

            if (_lightSizeSlider == null)
            {
                var slider = FindDescendant(toolbar, "LightSizeSlider");
                slider ??= FindDescendant(toolbar, "Slider");
                _lightSizeSlider = slider != null ? slider.GetComponent<Slider>() : null;
            }
            if (_lightSizeSlider == null)
            {
                if (FindDescendant(toolbar, "LightSizeLabel") == null)
                {
                    var sizeLabel = UiFactory.CreateSecondaryLabel(toolbar, "Light Size", 60f);
                    sizeLabel.gameObject.name = "LightSizeLabel";
                }

                _lightSizeSlider = UiFactory.CreateSlider(
                    toolbar,
                    ScenarioDataUtility.MinPreviewLightSize,
                    ScenarioDataUtility.MaxPreviewLightSize,
                    _app != null ? _app.PreviewLightSize : ScenarioDataUtility.DefaultPreviewLightSize,
                    120f);
                _lightSizeSlider.gameObject.name = "LightSizeSlider";

                var sliderLayout = _lightSizeSlider.GetComponent<LayoutElement>();
                if (sliderLayout != null)
                {
                    sliderLayout.minWidth = 90f;
                    sliderLayout.flexibleWidth = 1f;
                }
            }

            if (_lightSizeText == null)
            {
                var value = FindDescendant(toolbar, "LightSizeValue");
                _lightSizeText = value != null ? value.GetComponent<TMP_Text>() : null;
            }
            if (_lightSizeText == null)
            {
                var initialSize = _app != null
                    ? _app.PreviewLightSize
                    : ScenarioDataUtility.DefaultPreviewLightSize;
                _lightSizeText = UiFactory.CreateSecondaryLabel(toolbar, initialSize.ToString("0"), 34f);
                _lightSizeText.gameObject.name = "LightSizeValue";
                _lightSizeText.alignment = TextAlignmentOptions.Right;
            }

            if (_showUnitNamesToggle == null)
            {
                var names = FindDescendant(toolbar, "Toggle_Unit Names");
                _showUnitNamesToggle = names != null ? names.GetComponent<Toggle>() : null;
            }
            if (_showUnitNamesToggle == null)
            {
                _showUnitNamesToggle = UiFactory.CreateToggle(toolbar, "Unit Names", true);
                _showUnitNamesToggle.gameObject.name = "Toggle_Unit Names";
            }
        }

        private static void CreateBottomDivider(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            var rt = existing;
            if (rt == null)
            {
                var go = UiFactory.CreateUIObject(name, parent);
                rt = (RectTransform)go.transform;
                UiFactory.AddImage(go, AppTheme.Divider).raycastTarget = false;
            }

            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 1f);
            rt.SetAsLastSibling();
        }

        private void EnsureContentHierarchy()
        {
            if (_contentRoot == null)
            {
                _contentRoot = FindDescendant<RectTransform>(transform, "PreviewContent");
            }

            if (_contentRoot == null)
            {
                var contentGo = UiFactory.CreateUIObject("PreviewContent", transform);
                _contentRoot = (RectTransform)contentGo.transform;
                _contentRoot.anchorMin = Vector2.zero;
                _contentRoot.anchorMax = Vector2.one;
                _contentRoot.offsetMin = Vector2.zero;
                var headerHeight = FindDescendant(transform, "PreviewSectionHeader") != null
                    ? SectionHeaderHeight
                    : 0f;
                _contentRoot.offsetMax = new Vector2(0f, -(headerHeight + ToolbarHeight));
                UiFactory.AddImage(contentGo, AppTheme.PreviewCanvas).raycastTarget = true;
            }

            if (_backgroundImage == null)
            {
                var background = FindDescendant(_contentRoot, "PreviewBackgroundImage");
                if (background != null)
                    _backgroundImage = background.GetComponent<Image>() ?? UiFactory.AddImage(background.gameObject, Color.white);
            }
            if (_backgroundImage == null)
            {
                var backgroundGo = UiFactory.CreateUIObject("PreviewBackgroundImage", _contentRoot);
                var backgroundRt = (RectTransform)backgroundGo.transform;
                UiFactory.Stretch(backgroundRt);
                _backgroundImage = UiFactory.AddImage(backgroundGo, Color.white);
            }

            if (_backgroundImage != null)
            {
                _backgroundImage.raycastTarget = false;
                _backgroundImage.preserveAspect = true;
                _backgroundImage.type = Image.Type.Simple;
                if (_backgroundImage.sprite == null)
                    _backgroundImage.enabled = false;
            }

            if (_lightsLayer == null)
            {
                _lightsLayer = FindDescendant<RectTransform>(_contentRoot, "LightsLayer");
            }
            if (_lightsLayer == null)
            {
                var lightsGo = UiFactory.CreateUIObject("LightsLayer", _contentRoot);
                _lightsLayer = (RectTransform)lightsGo.transform;
                UiFactory.Stretch(_lightsLayer);
            }
        }


#endif

        public void Rebuild()
        {
            if (!HasRequiredChrome())
            {
                Debug.LogError(
                    "Preview hierarchy is incomplete. " +
                    "Regenerate the UI from Tools > Lighting Scenario > Build Complete UI In Current Scene.", this);
                return;
            }

            RefreshToolbar();
            RefreshBackground();

            // Reconcile by unit ID instead of destroying every preview object for every
            // document change. This keeps input/selection state stable and avoids creating
            // garbage when editing keyframes, toggles, names, or timeline settings.
            var units = _app.Document.Data.lightingUnits;
            var currentIds = new HashSet<string>(units.Select(unit => unit.unitId));
            foreach (var staleId in _views.Keys.Where(id => !currentIds.Contains(id)).ToList())
            {
                if (_views.TryGetValue(staleId, out var staleView) && staleView != null)
                    Destroy(staleView.gameObject);
                _views.Remove(staleId);
            }

            for (var i = 0; i < units.Count; i++)
            {
                var unit = units[i];
                if (!_views.TryGetValue(unit.unitId, out var view) || view == null)
                {
                    view = Instantiate(_app.UiPrefabs.PreviewLightPrefab, _lightsLayer);
                    view.gameObject.name = unit.unitId;
                    view.gameObject.SetActive(true);
                    _views[unit.unitId] = view;
                }

                view.Initialize(_app, unit.unitId);
                view.transform.SetSiblingIndex(i);
            }

            RefreshColors();
            RefreshSelection();
            RefreshUnitNameVisibility();
        }

        private void RefreshToolbar()
        {
            _updatingToolbar = true;
            if (_lightSizeSlider != null)
            {
                _lightSizeSlider.minValue = ScenarioDataUtility.MinPreviewLightSize;
                _lightSizeSlider.maxValue = ScenarioDataUtility.MaxPreviewLightSize;
                _lightSizeSlider.SetValueWithoutNotify(_app.PreviewLightSize);
            }
            if (_lightSizeText != null) _lightSizeText.text = _app.PreviewLightSize.ToString("0");
            if (_showUnitNamesToggle != null) _showUnitNamesToggle.SetIsOnWithoutNotify(_app.ShowPreviewUnitNames);
            _updatingToolbar = false;
        }

        public void RefreshBackground()
        {
            if (_backgroundImage == null) return;
            var path = _app.Document.Data.editorSettings?.previewBackgroundImagePath;
            path = string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();

            if (string.Equals(path, _loadedBackgroundPath, StringComparison.OrdinalIgnoreCase))
                return;

            ReleaseBackgroundTexture();
            _loadedBackgroundPath = path;

            if (string.IsNullOrEmpty(path))
            {
                _backgroundImage.enabled = false;
                return;
            }

            try
            {
                if (!File.Exists(path))
                {
                    _backgroundImage.enabled = false;
                    _app.ReportStatus("Preview background image was not found: " + path, true);
                    return;
                }

                var bytes = File.ReadAllBytes(path);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "LightingScenarioTool Preview Background",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                if (!texture.LoadImage(bytes, false))
                {
                    Destroy(texture);
                    _backgroundImage.enabled = false;
                    _app.ReportStatus("Preview background image could not be decoded.", true);
                    return;
                }

                var sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                sprite.name = "LightingScenarioTool Preview Background Sprite";

                _backgroundTexture = texture;
                _backgroundSprite = sprite;
                _backgroundImage.sprite = sprite;
                _backgroundImage.preserveAspect = true;
                _backgroundImage.enabled = true;
            }
            catch (Exception ex)
            {
                _backgroundImage.enabled = false;
                _app.ReportStatus("Preview background image load failed: " + ex.Message, true);
            }
        }

        private void ReleaseBackgroundTexture()
        {
            if (_backgroundImage != null) _backgroundImage.sprite = null;
            if (_backgroundSprite != null) Destroy(_backgroundSprite);
            if (_backgroundTexture != null) Destroy(_backgroundTexture);
            _backgroundSprite = null;
            _backgroundTexture = null;
        }

        private void OnDestroy()
        {
            ReleaseBackgroundTexture();
        }

        public void RefreshColors()
        {
            foreach (var pair in _views)
                pair.Value.RefreshColor();
        }

        public void RefreshLightSizes()
        {
            RefreshToolbar();
            foreach (var pair in _views)
                pair.Value.RefreshSize();
        }

        public void RefreshSelection()
        {
            foreach (var pair in _views)
                pair.Value.SetSelected(pair.Key == _app.SelectedUnitId);
        }

        public void RefreshUnitNameVisibility()
        {
            var visible = _app != null && _app.ShowPreviewUnitNames;
            foreach (var pair in _views)
                pair.Value.SetNameVisible(visible);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_contentRoot == null || !RectTransformUtility.RectangleContainsScreenPoint(_contentRoot, eventData.position, eventData.pressEventCamera))
                return;

            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _app.ClearSelection();
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Right) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_contentRoot, eventData.position, eventData.pressEventCamera, out var local)) return;
            if (_contentRoot.rect.width <= 0f || _contentRoot.rect.height <= 0f) return;

            var halfSize = _app.PreviewLightSize * 0.5f;
            var minX = Mathf.Min(0.5f, halfSize / _contentRoot.rect.width);
            var minY = Mathf.Min(0.5f, halfSize / _contentRoot.rect.height);
            var topMargin = Mathf.Min(0.5f, (halfSize + 21f) / _contentRoot.rect.height);
            var x = Mathf.Clamp(local.x / _contentRoot.rect.width + _contentRoot.pivot.x, minX, 1f - minX);
            var y = Mathf.Clamp(local.y / _contentRoot.rect.height + _contentRoot.pivot.y, minY, 1f - topMargin);
            _app.ShowContext(eventData.position, "Add Lighting Unit", () =>
            {
                var unit = _app.Document.AddUnit(x, y);
                if (unit != null) _app.SelectUnit(unit.unitId);
            });
        }
    }
}
