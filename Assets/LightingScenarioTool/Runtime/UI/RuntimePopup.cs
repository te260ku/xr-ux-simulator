using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LightingScenarioTool
{

    internal sealed class RuntimeMenuItem
    {
        public string Label { get; private set; }
        public Action Callback { get; private set; }
        public bool IsSeparator { get; private set; }

        private RuntimeMenuItem() { }

        public static RuntimeMenuItem Command(string label, Action callback)
        {
            return new RuntimeMenuItem { Label = label ?? string.Empty, Callback = callback };
        }

        public static RuntimeMenuItem Separator()
        {
            return new RuntimeMenuItem { IsSeparator = true };
        }
    }
    internal sealed class RuntimePopup
    {
        private readonly RectTransform _overlayRoot;
        private GameObject _current;
        private RectTransform _dismissBounds;
        private int _openedFrame = -1;
        private GameObject _auxiliary;
        private RectTransform _auxiliaryBounds;
        private int _auxiliaryOpenedFrame = -1;

        public RuntimePopup(RectTransform overlayRoot)
        {
            _overlayRoot = overlayRoot;
        }

        public bool IsOpen => _current != null;

        public void Close()
        {
            CloseAuxiliary();
            if (_current != null) UnityEngine.Object.Destroy(_current);
            _current = null;
            _dismissBounds = null;
            _openedFrame = -1;
        }

        private void CloseAuxiliary()
        {
            if (_auxiliary != null) UnityEngine.Object.Destroy(_auxiliary);
            _auxiliary = null;
            _auxiliaryBounds = null;
            _auxiliaryOpenedFrame = -1;
        }

        public void Tick()
        {
            if (_current == null || _dismissBounds == null) return;
            if (!TryGetPointerPress(out var screenPosition)) return;

            if (_auxiliary != null && _auxiliaryBounds != null && Time.frameCount != _auxiliaryOpenedFrame)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(_auxiliaryBounds, screenPosition, null))
                    return;
                CloseAuxiliary();
            }

            if (Time.frameCount == _openedFrame) return;
            if (!RectTransformUtility.RectangleContainsScreenPoint(_dismissBounds, screenPosition, null))
                Close();
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

        public void ShowMenu(Vector2 screenPosition, IReadOnlyList<RuntimeMenuItem> items)
        {
            Close();
            if (items == null || items.Count == 0) return;

            const float width = 210f;
            const float itemHeight = 30f;
            const float separatorHeight = 9f;
            const float padding = 5f;
            var height = padding * 2f;
            for (var i = 0; i < items.Count; i++)
                height += items[i] != null && items[i].IsSeparator ? separatorHeight : itemHeight;

            _current = UiFactory.CreateUIObject("MenuPopup", _overlayRoot);
            _openedFrame = Time.frameCount;
            var rt = (RectTransform)_current.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            UiFactory.AddImage(_current, new Color(0.075f, 0.075f, 0.075f, 0.995f));
            _dismissBounds = rt;
            SetScreenPosition(rt, screenPosition);

            var y = -padding;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;
                if (item.IsSeparator)
                {
                    var line = UiFactory.CreateUIObject("Separator_" + i, _current.transform);
                    var lineRt = (RectTransform)line.transform;
                    lineRt.anchorMin = lineRt.anchorMax = new Vector2(0f, 1f);
                    lineRt.pivot = new Vector2(0f, 0.5f);
                    lineRt.anchoredPosition = new Vector2(8f, y - separatorHeight * 0.5f);
                    lineRt.sizeDelta = new Vector2(width - 16f, 1f);
                    UiFactory.AddImage(line, new Color(0.28f, 0.28f, 0.28f, 1f)).raycastTarget = false;
                    y -= separatorHeight;
                    continue;
                }

                var captured = item;
                var button = UiFactory.CreateButton(_current.transform, item.Label, () =>
                {
                    Close();
                    captured.Callback?.Invoke();
                }, width - 10f);
                var brt = (RectTransform)button.transform;
                brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f);
                brt.pivot = new Vector2(0f, 1f);
                brt.anchoredPosition = new Vector2(5f, y);
                brt.sizeDelta = new Vector2(width - 10f, itemHeight - 2f);
                var text = button.GetComponentInChildren<TMP_Text>();
                if (text != null)
                {
                    text.alignment = TextAlignmentOptions.Left;
                    text.margin = new Vector4(8f, 0f, 4f, 0f);
                }
                y -= itemHeight;
            }

            ClampToOverlay(rt);
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
            rt.sizeDelta = new Vector2(340f, 474f);
            UiFactory.AddImage(_current, new Color(0.08f, 0.08f, 0.08f, 0.995f));
            _dismissBounds = rt;
            SetScreenPosition(rt, screenPosition);

            var titleGo = UiFactory.CreateUIObject("Title", _current.transform);
            var titleRt = (RectTransform)titleGo.transform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -5f);
            titleRt.sizeDelta = new Vector2(-10f, 24f);
            UiFactory.AddText(titleGo, "Color Picker", 13, TextAnchor.MiddleCenter).raycastTarget = false;

            var pickerRoot = UiFactory.CreateUIObject("Picker", _current.transform);
            var pickerRt = (RectTransform)pickerRoot.transform;
            pickerRt.anchorMin = pickerRt.anchorMax = new Vector2(0.5f, 1f);
            pickerRt.pivot = new Vector2(0.5f, 1f);
            pickerRt.anchoredPosition = new Vector2(0f, -32f);
            pickerRt.sizeDelta = new Vector2(220f, 220f);

            var ringGo = UiFactory.CreateUIObject("HueRing", pickerRoot.transform);
            var ringRt = (RectTransform)ringGo.transform;
            ringRt.anchorMin = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
            ringRt.sizeDelta = new Vector2(214f, 214f);
            var ring = ringGo.AddComponent<RawImage>();

            var svGo = UiFactory.CreateUIObject("SVSquare", pickerRoot.transform);
            var svRt = (RectTransform)svGo.transform;
            svRt.anchorMin = svRt.anchorMax = new Vector2(0.5f, 0.5f);
            svRt.sizeDelta = new Vector2(96f, 96f);
            var sv = svGo.AddComponent<RawImage>();

            var hueMarkerGo = CreateMarker("HueMarker", pickerRoot.transform, 12f);
            var hueMarker = (RectTransform)hueMarkerGo.transform;
            var svMarkerGo = CreateMarker("SVMarker", svGo.transform, 11f);
            var svMarker = (RectTransform)svMarkerGo.transform;

            var previewRow = CreatePickerRow(_current.transform, "PreviewRow", -258f);
            UiFactory.CreateLabel(previewRow, "Current", 54f);
            var previewGo = UiFactory.CreateUIObject("Preview", previewRow);
            var previewLayout = previewGo.AddComponent<LayoutElement>();
            previewLayout.preferredWidth = 46f;
            previewLayout.preferredHeight = 24f;
            var preview = UiFactory.AddImage(previewGo, currentColor);
            preview.raycastTarget = false;

            var rgbRow = CreatePickerRow(_current.transform, "RgbRow", -294f);
            UiFactory.CreateLabel(rgbRow, "RGB", 36f);
            UiFactory.CreateLabel(rgbRow, "R", 14f);
            var r = UiFactory.CreateInput(rgbRow, string.Empty, 55f);
            UiFactory.CreateLabel(rgbRow, "G", 14f);
            var g = UiFactory.CreateInput(rgbRow, string.Empty, 55f);
            UiFactory.CreateLabel(rgbRow, "B", 14f);
            var b = UiFactory.CreateInput(rgbRow, string.Empty, 55f);

            var hsvRow = CreatePickerRow(_current.transform, "HsvRow", -330f);
            UiFactory.CreateLabel(hsvRow, "HSV", 36f);
            UiFactory.CreateLabel(hsvRow, "H", 14f);
            var h = UiFactory.CreateInput(hsvRow, string.Empty, 55f);
            UiFactory.CreateLabel(hsvRow, "S", 14f);
            var sat = UiFactory.CreateInput(hsvRow, string.Empty, 55f);
            UiFactory.CreateLabel(hsvRow, "V", 14f);
            var val = UiFactory.CreateInput(hsvRow, string.Empty, 55f);

            r.contentType = g.contentType = b.contentType = TMP_InputField.ContentType.DecimalNumber;
            h.contentType = sat.contentType = val.contentType = TMP_InputField.ContentType.DecimalNumber;

            var control = pickerRoot.AddComponent<HsvColorPickerControl>();
            control.Initialize(ring, sv, hueMarker, svMarker, preview, r, g, b, h, sat, val, currentColor, onSelected);

            var hueInput = ringGo.AddComponent<HsvHueRingInput>();
            hueInput.Initialize(control, ringRt);
            var svInput = svGo.AddComponent<HsvSvSquareInput>();
            svInput.Initialize(control, svRt);

            var presetRows = new Transform[2];
            presetRows[0] = CreatePresetRow(_current.transform, "PresetRow1", -400f);
            presetRows[1] = CreatePresetRow(_current.transform, "PresetRow2", -434f);

            var presetHeader = CreatePickerRow(_current.transform, "PresetHeader", -366f);
            UiFactory.CreateLabel(presetHeader, "Presets", 62f);
            var presetSpacer = UiFactory.CreateUIObject("PresetSpacer", presetHeader);
            presetSpacer.AddComponent<LayoutElement>().flexibleWidth = 1f;
            UiFactory.CreateButton(presetHeader, "Save Preset", () =>
            {
                ColorPresetStore.Add(control.SelectedColor);
                RebuildPresetSwatches(control, presetRows);
            }, 104f);

            RebuildPresetSwatches(control, presetRows);

            ClampToOverlay(rt);
        }

        private static Transform CreatePresetRow(Transform parent, string name, float yFromTop)
        {
            var row = UiFactory.CreateRow(parent, 30f);
            row.gameObject.name = name;
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, yFromTop);
            row.sizeDelta = new Vector2(316f, 30f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(3, 3, 2, 2);
            layout.spacing = 5f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return row;
        }

        private void RebuildPresetSwatches(HsvColorPickerControl control, IReadOnlyList<Transform> rows)
        {
            if (control == null || rows == null) return;
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];
                if (row == null) continue;
                for (var i = row.childCount - 1; i >= 0; i--)
                {
                    var child = row.GetChild(i).gameObject;
                    child.SetActive(false);
                    UnityEngine.Object.Destroy(child);
                }
            }

            var presets = ColorPresetStore.Load();
            const int perRow = 6;
            for (var i = 0; i < presets.Count && i < perRow * rows.Count; i++)
            {
                var color = presets[i];
                var row = rows[i / perRow];
                if (row == null) continue;

                var swatchGo = UiFactory.CreateUIObject("Preset_" + i, row);
                var layout = swatchGo.AddComponent<LayoutElement>();
                layout.preferredWidth = 44f;
                layout.preferredHeight = 24f;
                layout.minWidth = 44f;
                layout.minHeight = 24f;
                var image = UiFactory.AddImage(swatchGo, color);
                var button = swatchGo.AddComponent<Button>();
                button.targetGraphic = image;
                var captured = color;
                var capturedIndex = i;
                button.onClick.AddListener(() => control.SetColor(captured, true));

                var rightClick = swatchGo.AddComponent<PresetSwatchRightClick>();
                rightClick.Initialize(screenPosition =>
                {
                    ShowPresetDeleteContext(screenPosition, () =>
                    {
                        ColorPresetStore.RemoveAt(capturedIndex);
                        RebuildPresetSwatches(control, rows);
                    });
                });

                var outline = swatchGo.AddComponent<Outline>();
                outline.effectColor = new Color(0.78f, 0.78f, 0.78f, 1f);
                outline.effectDistance = new Vector2(1f, 1f);
            }
        }

        private void ShowPresetDeleteContext(Vector2 screenPosition, Action deleteAction)
        {
            CloseAuxiliary();

            _auxiliary = UiFactory.CreateUIObject("PresetContextMenu", _overlayRoot);
            _auxiliaryOpenedFrame = Time.frameCount;
            var rt = (RectTransform)_auxiliary.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(150f, 40f);
            UiFactory.AddImage(_auxiliary, new Color(0.075f, 0.075f, 0.075f, 0.995f));
            _auxiliaryBounds = rt;
            SetScreenPosition(rt, screenPosition);

            var button = UiFactory.CreateButton(_auxiliary.transform, "Delete Preset", () =>
            {
                CloseAuxiliary();
                deleteAction?.Invoke();
            }, 140f);
            var brt = (RectTransform)button.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(140f, 30f);
        }

        private static Transform CreatePickerRow(Transform parent, string name, float yFromTop)
        {
            var row = UiFactory.CreateRow(parent, 32f);
            row.gameObject.name = name;
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, yFromTop);
            row.sizeDelta = new Vector2(316f, 32f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(3, 3, 1, 1);
            layout.spacing = 3f;
            return row;
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

        public void ShowSaveDiscardCancel(
            string message,
            Action saveAndContinue,
            Action discardAndContinue,
            Action cancel = null)
        {
            Close();
            _current = UiFactory.CreateUIObject("UnsavedChangesOverlay", _overlayRoot);
            _openedFrame = Time.frameCount;
            var overlayRt = (RectTransform)_current.transform;
            UiFactory.Stretch(overlayRt);
            UiFactory.AddImage(_current, new Color(0f, 0f, 0f, 0.6f));

            var panel = UiFactory.CreateUIObject("Panel", _current.transform);
            var panelRt = (RectTransform)panel.transform;
            panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(520f, 176f);
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

            var saveButton = UiFactory.CreateButton(panel.transform, "Save", () =>
            {
                Close();
                saveAndContinue?.Invoke();
            }, 130f);
            var saveRt = (RectTransform)saveButton.transform;
            saveRt.anchorMin = saveRt.anchorMax = new Vector2(0.5f, 0f);
            saveRt.pivot = new Vector2(1f, 0f);
            saveRt.anchoredPosition = new Vector2(-82f, 18f);
            saveRt.sizeDelta = new Vector2(130f, 32f);

            var discardButton = UiFactory.CreateButton(panel.transform, "Don't Save", () =>
            {
                Close();
                discardAndContinue?.Invoke();
            }, 130f);
            var discardRt = (RectTransform)discardButton.transform;
            discardRt.anchorMin = discardRt.anchorMax = new Vector2(0.5f, 0f);
            discardRt.pivot = new Vector2(0.5f, 0f);
            discardRt.anchoredPosition = new Vector2(0f, 18f);
            discardRt.sizeDelta = new Vector2(130f, 32f);

            var cancelButton = UiFactory.CreateButton(panel.transform, "Cancel", () =>
            {
                Close();
                cancel?.Invoke();
            }, 130f);
            var cancelRt = (RectTransform)cancelButton.transform;
            cancelRt.anchorMin = cancelRt.anchorMax = new Vector2(0.5f, 0f);
            cancelRt.pivot = new Vector2(0f, 0f);
            cancelRt.anchoredPosition = new Vector2(82f, 18f);
            cancelRt.sizeDelta = new Vector2(130f, 32f);
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

    internal sealed class PresetSwatchRightClick : MonoBehaviour, IPointerClickHandler
    {
        private Action<Vector2> _onRightClick;

        public void Initialize(Action<Vector2> onRightClick)
        {
            _onRightClick = onRightClick;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right) return;
            _onRightClick?.Invoke(eventData.position);
        }
    }

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
