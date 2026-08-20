using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LightingScenarioTool
{
    internal sealed class RuntimePopup
    {
        private readonly RectTransform _overlayRoot;
        private GameObject _current;
        private RectTransform _dismissBounds;
        private int _openedFrame = -1;

        public RuntimePopup(RectTransform overlayRoot)
        {
            _overlayRoot = overlayRoot;
        }

        public bool IsOpen => _current != null;

        public void Close()
        {
            if (_current != null) UnityEngine.Object.Destroy(_current);
            _current = null;
            _dismissBounds = null;
            _openedFrame = -1;
        }

        public void Tick()
        {
            if (_current == null || _dismissBounds == null) return;
            if (Time.frameCount == _openedFrame) return;
            if (!TryGetPointerPress(out var screenPosition)) return;

            if (!RectTransformUtility.RectangleContainsScreenPoint(_dismissBounds, screenPosition, null))
            {
                Close();
            }
        }

        public void ShowContext(Vector2 screenPosition, string label, Action action)
        {
            Close();
            _current = UiFactory.CreateUIObject("ContextMenu", _overlayRoot);
            _openedFrame = Time.frameCount;
            var rt = (RectTransform)_current.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(190f, 38f);
            SetScreenPosition(rt, screenPosition);
            _dismissBounds = rt;
            UiFactory.AddImage(_current, new Color(0.08f, 0.08f, 0.08f, 0.98f));
            var button = UiFactory.CreateButton(_current.transform, label, () =>
            {
                Close();
                action?.Invoke();
            }, 180f);
            var brt = (RectTransform)button.transform;
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(180f, 30f);
        }

        public void ShowHsvColorPicker(Vector2 screenPosition, Color currentColor, Action<Color> onSelected)
        {
            Close();

            _current = UiFactory.CreateUIObject("HsvColorPicker", _overlayRoot);
            _openedFrame = Time.frameCount;
            var rt = (RectTransform)_current.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(268f, 302f);
            SetScreenPosition(rt, screenPosition);
            UiFactory.AddImage(_current, new Color(0.08f, 0.08f, 0.08f, 0.99f));
            _dismissBounds = rt;

            var titleGo = UiFactory.CreateUIObject("Title", _current.transform);
            var titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -5f);
            titleRt.sizeDelta = new Vector2(-10f, 24f);
            UiFactory.AddText(titleGo, "HSV Color Picker", 12, TextAnchor.MiddleCenter).raycastTarget = false;

            var pickerRoot = UiFactory.CreateUIObject("Picker", _current.transform);
            var pickerRt = (RectTransform)pickerRoot.transform;
            pickerRt.anchorMin = pickerRt.anchorMax = new Vector2(0.5f, 1f);
            pickerRt.pivot = new Vector2(0.5f, 1f);
            pickerRt.anchoredPosition = new Vector2(0f, -32f);
            pickerRt.sizeDelta = new Vector2(228f, 228f);

            var ringGo = UiFactory.CreateUIObject("HueRing", pickerRoot.transform);
            var ringRt = (RectTransform)ringGo.transform;
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            ringRt.sizeDelta = new Vector2(220f, 220f);
            var ring = ringGo.AddComponent<RawImage>();

            var svGo = UiFactory.CreateUIObject("SVSquare", pickerRoot.transform);
            var svRt = (RectTransform)svGo.transform;
            svRt.anchorMin = svRt.anchorMax = new Vector2(0.5f, 0.5f);
            svRt.sizeDelta = new Vector2(132f, 132f);
            var sv = svGo.AddComponent<RawImage>();

            var hueMarkerGo = CreateMarker("HueMarker", pickerRoot.transform, 12f);
            var hueMarker = (RectTransform)hueMarkerGo.transform;

            var svMarkerGo = CreateMarker("SVMarker", svGo.transform, 11f);
            var svMarker = (RectTransform)svMarkerGo.transform;

            var previewGo = UiFactory.CreateUIObject("Preview", _current.transform);
            var previewRt = (RectTransform)previewGo.transform;
            previewRt.anchorMin = previewRt.anchorMax = new Vector2(0f, 0f);
            previewRt.pivot = new Vector2(0f, 0f);
            previewRt.anchoredPosition = new Vector2(14f, 12f);
            previewRt.sizeDelta = new Vector2(40f, 28f);
            var preview = UiFactory.AddImage(previewGo, currentColor);
            preview.raycastTarget = false;

            var hintGo = UiFactory.CreateUIObject("Hint", _current.transform);
            var hintRt = (RectTransform)hintGo.transform;
            hintRt.anchorMin = hintRt.anchorMax = new Vector2(0f, 0f);
            hintRt.pivot = new Vector2(0f, 0f);
            hintRt.anchoredPosition = new Vector2(62f, 11f);
            hintRt.sizeDelta = new Vector2(192f, 30f);
            var hint = UiFactory.AddText(hintGo, "Hue Ring / Saturation-Value", 11, TextAnchor.MiddleLeft);
            hint.raycastTarget = false;

            var control = pickerRoot.AddComponent<HsvColorPickerControl>();
            control.Initialize(ring, sv, hueMarker, svMarker, preview, currentColor, onSelected);

            var hueInput = ringGo.AddComponent<HsvHueRingInput>();
            hueInput.Initialize(control, ringRt);
            var svInput = svGo.AddComponent<HsvSvSquareInput>();
            svInput.Initialize(control, svRt);
        }

        private static GameObject CreateMarker(string name, Transform parent, float size)
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
            return go;
        }

        public void ShowConfirm(string message, Action yes, Action no = null)
        {
            Close();
            _current = UiFactory.CreateUIObject("ConfirmOverlay", _overlayRoot);
            _openedFrame = Time.frameCount;
            var overlayRt = (RectTransform)_current.transform;
            UiFactory.Stretch(overlayRt);
            UiFactory.AddImage(_current, new Color(0f, 0f, 0f, 0.6f));

            var panel = UiFactory.CreateUIObject("Panel", _current.transform);
            var panelRt = (RectTransform)panel.transform;
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(420f, 160f);
            UiFactory.AddImage(panel, new Color(0.14f, 0.14f, 0.14f, 1f));

            var msgGo = UiFactory.CreateUIObject("Message", panel.transform);
            var msgRt = (RectTransform)msgGo.transform;
            msgRt.anchorMin = new Vector2(0f, 0.42f);
            msgRt.anchorMax = new Vector2(1f, 1f);
            msgRt.offsetMin = new Vector2(18f, 0f);
            msgRt.offsetMax = new Vector2(-18f, -12f);
            var msg = UiFactory.AddText(msgGo, message, 14, TextAnchor.MiddleCenter);
            msg.enableWordWrapping = true;
            msg.overflowMode = TextOverflowModes.Overflow;

            var yesButton = UiFactory.CreateButton(panel.transform, "Yes", () =>
            {
                Close();
                yes?.Invoke();
            }, 90f);
            var yesRt = (RectTransform)yesButton.transform;
            yesRt.anchorMin = yesRt.anchorMax = new Vector2(0.5f, 0f);
            yesRt.pivot = new Vector2(1f, 0f);
            yesRt.anchoredPosition = new Vector2(-8f, 18f);
            yesRt.sizeDelta = new Vector2(90f, 32f);

            var noButton = UiFactory.CreateButton(panel.transform, "No", () =>
            {
                Close();
                no?.Invoke();
            }, 90f);
            var noRt = (RectTransform)noButton.transform;
            noRt.anchorMin = noRt.anchorMax = new Vector2(0.5f, 0f);
            noRt.pivot = new Vector2(0f, 0f);
            noRt.anchoredPosition = new Vector2(8f, 18f);
            noRt.sizeDelta = new Vector2(90f, 32f);
        }

        private void SetScreenPosition(RectTransform target, Vector2 screenPosition)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlayRoot, screenPosition, null, out var local))
            {
                target.anchoredPosition = local + new Vector2(_overlayRoot.pivot.x * _overlayRoot.rect.width, _overlayRoot.pivot.y * _overlayRoot.rect.height);
            }
            else
            {
                target.position = screenPosition;
            }

            Canvas.ForceUpdateCanvases();
            ClampToOverlay(target);
        }

        private void ClampToOverlay(RectTransform target)
        {
            var size = target.rect.size;
            var x = Mathf.Clamp(target.anchoredPosition.x, 0f, Mathf.Max(0f, _overlayRoot.rect.width - size.x));
            var y = Mathf.Clamp(target.anchoredPosition.y, size.y, Mathf.Max(size.y, _overlayRoot.rect.height));
            target.anchoredPosition = new Vector2(x, y);
        }

        private static bool TryGetPointerPress(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null &&
                (Mouse.current.leftButton.wasPressedThisFrame ||
                 Mouse.current.rightButton.wasPressedThisFrame ||
                 Mouse.current.middleButton.wasPressedThisFrame))
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }
#else
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                screenPosition = Input.mousePosition;
                return true;
            }
#endif
            screenPosition = default;
            return false;
        }
    }

    internal sealed class HsvColorPickerControl : MonoBehaviour
    {
        private RawImage _hueRing;
        private RawImage _svSquare;
        private RectTransform _hueMarker;
        private RectTransform _svMarker;
        private Image _preview;
        private Action<Color> _onCommitted;
        private Texture2D _ringTexture;
        private Texture2D _svTexture;
        private float _h;
        private float _s;
        private float _v;

        public void Initialize(
            RawImage hueRing,
            RawImage svSquare,
            RectTransform hueMarker,
            RectTransform svMarker,
            Image preview,
            Color initialColor,
            Action<Color> onCommitted)
        {
            _hueRing = hueRing;
            _svSquare = svSquare;
            _hueMarker = hueMarker;
            _svMarker = svMarker;
            _preview = preview;
            _onCommitted = onCommitted;

            Color.RGBToHSV(initialColor, out _h, out _s, out _v);
            _ringTexture = CreateHueRingTexture(256);
            _hueRing.texture = _ringTexture;
            RebuildSvTexture();
            RefreshMarkers();
            RefreshPreview();
        }

        public bool SetHueFromScreen(RectTransform rect, PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out var local)) return false;
            var radius = Mathf.Min(rect.rect.width, rect.rect.height) * 0.5f;
            var normalizedRadius = local.magnitude / Mathf.Max(1f, radius);
            if (normalizedRadius < 0.62f || normalizedRadius > 1.05f) return false;

            var angle = Mathf.Atan2(local.y, local.x);
            _h = angle / (Mathf.PI * 2f);
            if (_h < 0f) _h += 1f;
            RebuildSvTexture();
            RefreshMarkers();
            RefreshPreview();
            return true;
        }

        public bool SetSvFromScreen(RectTransform rect, PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, eventData.pressEventCamera, out var local)) return false;
            var xMin = rect.rect.xMin;
            var xMax = rect.rect.xMax;
            var yMin = rect.rect.yMin;
            var yMax = rect.rect.yMax;
            _s = Mathf.InverseLerp(xMin, xMax, Mathf.Clamp(local.x, xMin, xMax));
            _v = Mathf.InverseLerp(yMin, yMax, Mathf.Clamp(local.y, yMin, yMax));
            RefreshMarkers();
            RefreshPreview();
            return true;
        }

        public void Commit()
        {
            _onCommitted?.Invoke(CurrentColor());
        }

        private Color CurrentColor()
        {
            return Color.HSVToRGB(_h, _s, _v);
        }

        private void RefreshPreview()
        {
            if (_preview != null) _preview.color = CurrentColor();
        }

        private void RefreshMarkers()
        {
            if (_hueMarker != null && _hueRing != null)
            {
                var radius = Mathf.Min(_hueRing.rectTransform.rect.width, _hueRing.rectTransform.rect.height) * 0.435f;
                var angle = _h * Mathf.PI * 2f;
                _hueMarker.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }

            if (_svMarker != null && _svSquare != null)
            {
                var rect = _svSquare.rectTransform.rect;
                _svMarker.anchoredPosition = new Vector2(
                    Mathf.Lerp(rect.xMin, rect.xMax, _s),
                    Mathf.Lerp(rect.yMin, rect.yMax, _v));
            }
        }

        private void RebuildSvTexture()
        {
            const int size = 128;
            if (_svTexture == null)
            {
                _svTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                _svTexture.wrapMode = TextureWrapMode.Clamp;
                _svTexture.filterMode = FilterMode.Bilinear;
            }

            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                var value = y / (float)(size - 1);
                for (var x = 0; x < size; x++)
                {
                    var saturation = x / (float)(size - 1);
                    pixels[y * size + x] = Color.HSVToRGB(_h, saturation, value);
                }
            }
            _svTexture.SetPixels32(pixels);
            _svTexture.Apply(false, false);
            _svSquare.texture = _svTexture;
        }

        private static Texture2D CreateHueRingTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            var center = (size - 1) * 0.5f;
            var maxRadius = center;

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x - center) / maxRadius;
                    var dy = (y - center) / maxRadius;
                    var r = Mathf.Sqrt(dx * dx + dy * dy);
                    if (r < 0.66f || r > 0.98f)
                    {
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    var angle = Mathf.Atan2(dy, dx) / (Mathf.PI * 2f);
                    if (angle < 0f) angle += 1f;
                    pixels[y * size + x] = Color.HSVToRGB(angle, 1f, 1f);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private void OnDestroy()
        {
            if (_ringTexture != null) Destroy(_ringTexture);
            if (_svTexture != null) Destroy(_svTexture);
        }
    }

    internal sealed class HsvHueRingInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private HsvColorPickerControl _picker;
        private RectTransform _rect;
        private bool _dragging;

        public void Initialize(HsvColorPickerControl picker, RectTransform rect)
        {
            _picker = picker;
            _rect = rect;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _dragging = _picker.SetHueFromScreen(_rect, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _picker.SetHueFromScreen(_rect, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_dragging) return;
            _picker.SetHueFromScreen(_rect, eventData);
            _picker.Commit();
            _dragging = false;
        }
    }

    internal sealed class HsvSvSquareInput : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private HsvColorPickerControl _picker;
        private RectTransform _rect;
        private bool _dragging;

        public void Initialize(HsvColorPickerControl picker, RectTransform rect)
        {
            _picker = picker;
            _rect = rect;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _dragging = _picker.SetSvFromScreen(_rect, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _picker.SetSvFromScreen(_rect, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_dragging) return;
            _picker.SetSvFromScreen(_rect, eventData);
            _picker.Commit();
            _dragging = false;
        }
    }
}
