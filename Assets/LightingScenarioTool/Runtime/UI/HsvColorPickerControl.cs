using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace LightingScenarioTool
{
    internal sealed class HsvColorPickerControl : MonoBehaviour
    {
        private RawImage _hueRing;
        private RawImage _svSquare;
        private RectTransform _hueMarker;
        private RectTransform _svMarker;
        private Image _preview;
        private TMP_InputField _r;
        private TMP_InputField _g;
        private TMP_InputField _b;
        private TMP_InputField _hInput;
        private TMP_InputField _sInput;
        private TMP_InputField _vInput;
        private Action<Color> _onCommitted;
        private Texture2D _ringTexture;
        private Texture2D _svTexture;
        private float _h;
        private float _s;
        private float _v;
        private bool _updatingInputs;

        public void Initialize(
            RawImage hueRing,
            RawImage svSquare,
            RectTransform hueMarker,
            RectTransform svMarker,
            Image preview,
            TMP_InputField r,
            TMP_InputField g,
            TMP_InputField b,
            TMP_InputField h,
            TMP_InputField s,
            TMP_InputField v,
            Color initialColor,
            Action<Color> onCommitted)
        {
            _hueRing = hueRing;
            _svSquare = svSquare;
            _hueMarker = hueMarker;
            _svMarker = svMarker;
            _preview = preview;
            _r = r;
            _g = g;
            _b = b;
            _hInput = h;
            _sInput = s;
            _vInput = v;
            _onCommitted = onCommitted;

            Color.RGBToHSV(initialColor, out _h, out _s, out _v);
            _ringTexture = CreateHueRingTexture(256);
            _hueRing.texture = _ringTexture;
            RebuildSvTexture();
            RefreshAll();

            if (_r != null) _r.onEndEdit.AddListener(_ => ApplyRgbInputs());
            if (_g != null) _g.onEndEdit.AddListener(_ => ApplyRgbInputs());
            if (_b != null) _b.onEndEdit.AddListener(_ => ApplyRgbInputs());
            if (_hInput != null) _hInput.onEndEdit.AddListener(_ => ApplyHsvInputs());
            if (_sInput != null) _sInput.onEndEdit.AddListener(_ => ApplyHsvInputs());
            if (_vInput != null) _vInput.onEndEdit.AddListener(_ => ApplyHsvInputs());
        }

        public Color SelectedColor => CurrentColor();

        public void SetColor(Color color, bool commit)
        {
            color.a = 1f;
            Color.RGBToHSV(color, out _h, out _s, out _v);
            RebuildSvTexture();
            RefreshAll();
            if (commit) Commit();
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
            RefreshAll();
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
            RefreshAll();
            return true;
        }

        public void Commit()
        {
            _onCommitted?.Invoke(CurrentColor());
        }

        private void ApplyRgbInputs()
        {
            if (_updatingInputs) return;
            if (!TryRead(_r, out var r) || !TryRead(_g, out var g) || !TryRead(_b, out var b))
            {
                RefreshInputs();
                return;
            }

            var color = new Color(
                Mathf.Clamp(r, 0f, 255f) / 255f,
                Mathf.Clamp(g, 0f, 255f) / 255f,
                Mathf.Clamp(b, 0f, 255f) / 255f,
                1f);
            Color.RGBToHSV(color, out _h, out _s, out _v);
            RebuildSvTexture();
            RefreshAll();
            Commit();
        }

        private void ApplyHsvInputs()
        {
            if (_updatingInputs) return;
            if (!TryRead(_hInput, out var h) || !TryRead(_sInput, out var s) || !TryRead(_vInput, out var v))
            {
                RefreshInputs();
                return;
            }

            _h = Mathf.Repeat(h, 360f) / 360f;
            _s = Mathf.Clamp01(s / 100f);
            _v = Mathf.Clamp01(v / 100f);
            RebuildSvTexture();
            RefreshAll();
            Commit();
        }

        private static bool TryRead(TMP_InputField field, out float value)
        {
            value = 0f;
            return field != null && float.TryParse(field.text, out value);
        }

        private Color CurrentColor()
        {
            return Color.HSVToRGB(_h, _s, _v);
        }

        private void RefreshAll()
        {
            RefreshMarkers();
            RefreshPreview();
            RefreshInputs();
        }

        private void RefreshPreview()
        {
            if (_preview != null) _preview.color = CurrentColor();
        }

        private void RefreshInputs()
        {
            _updatingInputs = true;
            var color = CurrentColor();
            if (_r != null) _r.SetTextWithoutNotify(Mathf.RoundToInt(color.r * 255f).ToString());
            if (_g != null) _g.SetTextWithoutNotify(Mathf.RoundToInt(color.g * 255f).ToString());
            if (_b != null) _b.SetTextWithoutNotify(Mathf.RoundToInt(color.b * 255f).ToString());
            if (_hInput != null) _hInput.SetTextWithoutNotify((_h * 360f).ToString("0.0"));
            if (_sInput != null) _sInput.SetTextWithoutNotify((_s * 100f).ToString("0.0"));
            if (_vInput != null) _vInput.SetTextWithoutNotify((_v * 100f).ToString("0.0"));
            _updatingInputs = false;
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
                    var radius = Mathf.Sqrt(dx * dx + dy * dy);
                    if (radius < 0.66f || radius > 0.98f)
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
}
