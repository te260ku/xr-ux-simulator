using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace LightingScenarioTool
{
    internal sealed class PreviewPanel : MonoBehaviour, IPointerClickHandler
    {
        private const float ToolbarHeight = 34f;

        private LightingScenarioApp _app;
        private RectTransform _root;
        private RectTransform _contentRoot;
        private RectTransform _lightsLayer;
        private Image _backgroundImage;
        private Texture2D _backgroundTexture;
        private Sprite _backgroundSprite;
        private string _loadedBackgroundPath;
        private Slider _lightSizeSlider;
        private TMP_Text _lightSizeText;
        private bool _updatingToolbar;
        private readonly Dictionary<string, PreviewLightView> _views = new Dictionary<string, PreviewLightView>();

        public void Initialize(LightingScenarioApp app)
        {
            _app = app;
            _root = (RectTransform)transform;
            BuildChrome();
        }

        private void BuildChrome()
        {
            var toolbar = UiFactory.CreateUIObject("PreviewToolbar", transform);
            var toolbarRt = (RectTransform)toolbar.transform;
            toolbarRt.anchorMin = new Vector2(0f, 1f);
            toolbarRt.anchorMax = new Vector2(1f, 1f);
            toolbarRt.pivot = new Vector2(0.5f, 1f);
            toolbarRt.sizeDelta = new Vector2(0f, ToolbarHeight);
            UiFactory.AddImage(toolbar, new Color(0.09f, 0.09f, 0.09f, 1f));
            var toolbarLayout = toolbar.AddComponent<HorizontalLayoutGroup>();
            toolbarLayout.padding = new RectOffset(6, 6, 2, 2);
            toolbarLayout.spacing = 6f;
            toolbarLayout.childControlWidth = true;
            toolbarLayout.childControlHeight = true;
            toolbarLayout.childForceExpandWidth = false;
            toolbarLayout.childForceExpandHeight = true;

            UiFactory.CreateButton(toolbar.transform, "Background Image...", _app.BrowsePreviewBackgroundImage, 128f);
            UiFactory.CreateLabel(toolbar.transform, "Light Size", 58f);
            _lightSizeSlider = UiFactory.CreateSlider(toolbar.transform, 20f, 120f, _app.PreviewLightSize, 105f);
            var sliderLayout = _lightSizeSlider.GetComponent<LayoutElement>();
            if (sliderLayout != null)
            {
                sliderLayout.minWidth = 65f;
                sliderLayout.flexibleWidth = 1f;
            }
            _lightSizeText = UiFactory.CreateLabel(toolbar.transform, _app.PreviewLightSize.ToString("0"), 30f);
            _lightSizeText.alignment = TextAlignmentOptions.Right;
            _lightSizeSlider.onValueChanged.AddListener(value =>
            {
                if (_updatingToolbar) return;
                _app.SetPreviewLightSizeFromUi(value);
                if (_lightSizeText != null) _lightSizeText.text = value.ToString("0");
            });

            var contentGo = UiFactory.CreateUIObject("PreviewContent", transform);
            _contentRoot = (RectTransform)contentGo.transform;
            _contentRoot.anchorMin = Vector2.zero;
            _contentRoot.anchorMax = Vector2.one;
            _contentRoot.offsetMin = Vector2.zero;
            _contentRoot.offsetMax = new Vector2(0f, -ToolbarHeight);

            var backgroundGo = UiFactory.CreateUIObject("PreviewBackgroundImage", _contentRoot);
            var backgroundRt = (RectTransform)backgroundGo.transform;
            UiFactory.Stretch(backgroundRt);
            _backgroundImage = UiFactory.AddImage(backgroundGo, Color.white);
            _backgroundImage.raycastTarget = false;
            _backgroundImage.preserveAspect = true;
            _backgroundImage.type = Image.Type.Simple;
            _backgroundImage.enabled = false;

            var lightsGo = UiFactory.CreateUIObject("LightsLayer", _contentRoot);
            _lightsLayer = (RectTransform)lightsGo.transform;
            UiFactory.Stretch(_lightsLayer);
        }

        public void Rebuild()
        {
            if (_lightsLayer == null) BuildChrome();
            for (var i = _lightsLayer.childCount - 1; i >= 0; i--)
                Destroy(_lightsLayer.GetChild(i).gameObject);
            _views.Clear();

            RefreshToolbar();
            RefreshBackground();

            foreach (var unit in _app.Document.Data.lightingUnits)
            {
                var go = UiFactory.CreateUIObject(unit.unitId, _lightsLayer);
                var view = go.AddComponent<PreviewLightView>();
                view.Initialize(_app, unit.unitId);
                _views[unit.unitId] = view;
            }

            RefreshColors();
            RefreshSelection();
        }

        private void RefreshToolbar()
        {
            _updatingToolbar = true;
            if (_lightSizeSlider != null) _lightSizeSlider.SetValueWithoutNotify(_app.PreviewLightSize);
            if (_lightSizeText != null) _lightSizeText.text = _app.PreviewLightSize.ToString("0");
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

    internal sealed class PreviewLightView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private LightingScenarioApp _app;
        private string _unitId;
        private RectTransform _rt;
        private Image _image;
        private Outline _outline;
        private TMP_Text _label;
        private string _beforeDrag;

        public void Initialize(LightingScenarioApp app, string unitId)
        {
            _app = app;
            _unitId = unitId;
            _rt = (RectTransform)transform;
            _image = UiFactory.AddImage(gameObject, Color.black);
            _outline = gameObject.AddComponent<Outline>();
            _outline.effectColor = new Color(1f, 0.8f, 0.1f, 1f);
            _outline.effectDistance = new Vector2(2f, 2f);
            _outline.enabled = false;

            var labelGo = UiFactory.CreateUIObject("Label", transform);
            var labelRt = (RectTransform)labelGo.transform;
            labelRt.anchorMin = labelRt.anchorMax = new Vector2(0.5f, 1f);
            labelRt.pivot = new Vector2(0.5f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, 3f);
            _label = UiFactory.AddText(labelGo, string.Empty, 11, TextAnchor.MiddleCenter);
            _label.color = Color.white;
            _label.raycastTarget = false;
            _label.overflowMode = TextOverflowModes.Ellipsis;

            RefreshSize();
            RefreshPosition();
            RefreshName();
        }

        public void RefreshSize()
        {
            if (_rt == null || _app == null) return;
            var size = _app.PreviewLightSize;
            _rt.sizeDelta = new Vector2(size, size);
            if (_label != null)
                ((RectTransform)_label.transform).sizeDelta = new Vector2(Mathf.Max(100f, size * 1.5f), 18f);
            RefreshPosition();
        }

        private void RefreshName()
        {
            if (_label == null) return;
            var unit = _app.Document.FindUnit(_unitId);
            _label.text = unit != null ? unit.displayName : _unitId;
        }

        public void RefreshPosition()
        {
            var unit = _app.Document.FindUnit(_unitId);
            if (unit == null) return;
            var anchor = new Vector2(unit.previewX, unit.previewY);
            _rt.anchorMin = anchor;
            _rt.anchorMax = anchor;
            _rt.anchoredPosition = Vector2.zero;
            RefreshName();
        }

        public void RefreshColor()
        {
            var unit = _app.Document.FindUnit(_unitId);
            if (unit != null) _image.color = ScenarioEvaluator.Evaluate(unit, _app.CurrentTime);
        }

        public void SetSelected(bool selected)
        {
            if (_outline != null) _outline.enabled = selected;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _app.SelectUnit(_unitId);
            }
            else if (eventData.button == PointerEventData.InputButton.Right)
            {
                _app.ShowContext(eventData.position, "Delete Lighting Unit", () => _app.RequestDeleteUnit(_unitId));
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _app.SelectUnit(_unitId);
            _beforeDrag = _app.Document.CaptureState();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            var parent = (RectTransform)_rt.parent;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out var local)) return;
            var unit = _app.Document.FindUnit(_unitId);
            if (unit == null || parent.rect.width <= 0f || parent.rect.height <= 0f) return;
            var halfWidth = _rt.rect.width * 0.5f;
            var halfHeight = _rt.rect.height * 0.5f;
            var minX = Mathf.Min(0.5f, halfWidth / parent.rect.width);
            var minY = Mathf.Min(0.5f, halfHeight / parent.rect.height);
            var topMargin = Mathf.Min(0.5f, (halfHeight + 21f) / parent.rect.height);
            unit.previewX = Mathf.Clamp(local.x / parent.rect.width + parent.pivot.x, minX, 1f - minX);
            unit.previewY = Mathf.Clamp(local.y / parent.rect.height + parent.pivot.y, minY, 1f - topMargin);
            RefreshPosition();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(_beforeDrag)) return;
            _app.Document.CommitExternalEdit(_beforeDrag);
            _beforeDrag = null;
        }
    }
}
