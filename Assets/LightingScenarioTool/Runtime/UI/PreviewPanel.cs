using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LightingScenarioTool
{
    internal sealed class PreviewPanel : MonoBehaviour, IPointerClickHandler
    {
        private LightingScenarioApp _app;
        private RectTransform _root;
        private RectTransform _lightsLayer;
        private Image _backgroundImage;
        private Texture2D _backgroundTexture;
        private Sprite _backgroundSprite;
        private string _loadedBackgroundPath;
        private readonly Dictionary<string, PreviewLightView> _views = new Dictionary<string, PreviewLightView>();

        public void Initialize(LightingScenarioApp app)
        {
            _app = app;
            _root = (RectTransform)transform;
            BuildLayers();
        }

        private void BuildLayers()
        {
            var backgroundGo = UiFactory.CreateUIObject("PreviewBackgroundImage", transform);
            var backgroundRt = (RectTransform)backgroundGo.transform;
            UiFactory.Stretch(backgroundRt);
            _backgroundImage = UiFactory.AddImage(backgroundGo, Color.white);
            _backgroundImage.raycastTarget = false;
            _backgroundImage.preserveAspect = true;
            _backgroundImage.type = Image.Type.Simple;
            _backgroundImage.enabled = false;
            backgroundGo.transform.SetAsFirstSibling();

            var lightsGo = UiFactory.CreateUIObject("LightsLayer", transform);
            _lightsLayer = (RectTransform)lightsGo.transform;
            UiFactory.Stretch(_lightsLayer);
        }

        public void Rebuild()
        {
            if (_lightsLayer == null) BuildLayers();
            for (var i = _lightsLayer.childCount - 1; i >= 0; i--)
                Destroy(_lightsLayer.GetChild(i).gameObject);
            _views.Clear();

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

        public void RefreshSelection()
        {
            foreach (var pair in _views)
                pair.Value.SetSelected(pair.Key == _app.SelectedUnitId);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left)
            {
                _app.ClearSelection();
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Right) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, eventData.position, eventData.pressEventCamera, out var local)) return;
            var halfSize = 27f;
            var minX = _root.rect.width > 0f ? Mathf.Min(0.5f, halfSize / _root.rect.width) : 0f;
            var minY = _root.rect.height > 0f ? Mathf.Min(0.5f, halfSize / _root.rect.height) : 0f;
            var x = Mathf.Clamp(local.x / _root.rect.width + _root.pivot.x, minX, 1f - minX);
            var y = Mathf.Clamp(local.y / _root.rect.height + _root.pivot.y, minY, 1f - minY);
            _app.ShowContext(eventData.position, "Create Lighting Unit", () =>
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
        private string _beforeDrag;

        public void Initialize(LightingScenarioApp app, string unitId)
        {
            _app = app;
            _unitId = unitId;
            _rt = (RectTransform)transform;
            _rt.sizeDelta = new Vector2(54f, 54f);
            _image = UiFactory.AddImage(gameObject, Color.black);
            _outline = gameObject.AddComponent<Outline>();
            _outline.effectColor = new Color(1f, 0.8f, 0.1f, 1f);
            _outline.effectDistance = new Vector2(2f, 2f);
            _outline.enabled = false;

            var labelGo = UiFactory.CreateUIObject("Label", transform);
            var labelRt = (RectTransform)labelGo.transform;
            UiFactory.Stretch(labelRt);
            var unit = _app.Document.FindUnit(unitId);
            var label = UiFactory.AddText(labelGo, unit != null ? unit.displayName : unitId, 11, TextAnchor.MiddleCenter);
            label.color = Color.white;
            label.raycastTarget = false;
            RefreshPosition();
        }

        public void RefreshPosition()
        {
            var unit = _app.Document.FindUnit(_unitId);
            if (unit == null) return;
            var anchor = new Vector2(unit.previewX, unit.previewY);
            _rt.anchorMin = anchor;
            _rt.anchorMax = anchor;
            _rt.anchoredPosition = Vector2.zero;
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
            if (unit == null) return;
            var minX = parent.rect.width > 0f ? Mathf.Min(0.5f, _rt.rect.width * 0.5f / parent.rect.width) : 0f;
            var minY = parent.rect.height > 0f ? Mathf.Min(0.5f, _rt.rect.height * 0.5f / parent.rect.height) : 0f;
            unit.previewX = Mathf.Clamp(local.x / parent.rect.width + parent.pivot.x, minX, 1f - minX);
            unit.previewY = Mathf.Clamp(local.y / parent.rect.height + parent.pivot.y, minY, 1f - minY);
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
