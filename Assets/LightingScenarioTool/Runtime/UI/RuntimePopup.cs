using System;
using UnityEngine;
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
        private const float MenuWidth = 210f;
        private const float MenuItemHeight = 28f;
        private const float MenuSeparatorHeight = 9f;
        private const float MenuPadding = 5f;

        private readonly RectTransform _overlayRoot;
        private readonly LightingScenarioUiPrefabCatalog _prefabs;
        private GameObject _current;
        private RectTransform _dismissBounds;
        private int _openedFrame = -1;
        private GameObject _auxiliary;
        private RectTransform _auxiliaryBounds;
        private int _auxiliaryOpenedFrame = -1;

        public RuntimePopup(RectTransform overlayRoot, LightingScenarioUiPrefabCatalog prefabs)
        {
            _overlayRoot = overlayRoot;
            _prefabs = prefabs;
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
            ShowMenuInternal(
                screenPosition,
                new[] { RuntimeMenuItem.Command(label, action) },
                "ContextMenu",
                MenuWidth);
        }

        public void ShowMenu(Vector2 screenPosition, System.Collections.Generic.IReadOnlyList<RuntimeMenuItem> items)
        {
            ShowMenuInternal(screenPosition, items, "MenuPopup", MenuWidth);
        }

        private void ShowMenuInternal(
            Vector2 screenPosition,
            System.Collections.Generic.IReadOnlyList<RuntimeMenuItem> items,
            string name,
            float width)
        {
            Close();
            if (items == null || items.Count == 0) return;

            var height = MenuPadding * 2f;
            for (var i = 0; i < items.Count; i++)
                height += items[i] != null && items[i].IsSeparator ? MenuSeparatorHeight : MenuItemHeight;

            var view = UnityEngine.Object.Instantiate(_prefabs.MenuPopupPrefab, _overlayRoot);
            _current = view.gameObject;
            _current.name = name;
            _openedFrame = Time.frameCount;
            var rt = view.RectTransform;
            rt.sizeDelta = new Vector2(width, height);
            _dismissBounds = rt;

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null) continue;
                if (item.IsSeparator)
                {
                    var separator = UnityEngine.Object.Instantiate(_prefabs.MenuItemPrefab, view.Content);
                    separator.gameObject.name = "Separator_" + i;
                    separator.InitializeSeparator();
                    continue;
                }

                var captured = item;
                var itemView = UnityEngine.Object.Instantiate(_prefabs.MenuItemPrefab, view.Content);
                itemView.gameObject.name = "Item_" + i;
                itemView.Initialize(item.Label, () =>
                {
                    Close();
                    captured.Callback?.Invoke();
                });
            }

            SetScreenPosition(rt, screenPosition);
        }

        public void ShowHsvColorPicker(Vector2 screenPosition, Color currentColor, Action<Color> onSelected)
        {
            Close();

            var view = UnityEngine.Object.Instantiate(_prefabs.ColorPickerPrefab, _overlayRoot);
            _current = view.gameObject;
            _current.name = "HsvColorPicker";
            _openedFrame = Time.frameCount;
            var rt = view.RectTransform;
            _dismissBounds = rt;

            var control = view.Control;
            control.Initialize(
                view.HueRing,
                view.SvSquare,
                view.HueMarker,
                view.SvMarker,
                view.Preview,
                view.R,
                view.G,
                view.B,
                view.H,
                view.S,
                view.V,
                currentColor,
                onSelected);
            view.HueInput.Initialize(control, (RectTransform)view.HueRing.transform);
            view.SvInput.Initialize(control, (RectTransform)view.SvSquare.transform);

            view.SavePresetButton.onClick.RemoveAllListeners();
            view.SavePresetButton.onClick.AddListener(() =>
            {
                ColorPresetStore.Add(control.SelectedColor);
                RebuildPresetSwatches(control, view.PresetRows);
            });

            RebuildPresetSwatches(control, view.PresetRows);
            SetScreenPosition(rt, screenPosition);
        }

        private void RebuildPresetSwatches(HsvColorPickerControl control, System.Collections.Generic.IReadOnlyList<Transform> rows)
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

                var capturedColor = color;
                var capturedIndex = i;
                var swatch = UnityEngine.Object.Instantiate(_prefabs.PresetSwatchPrefab, row);
                swatch.gameObject.name = "Preset_" + i;
                swatch.Initialize(
                    color,
                    () => control.SetColor(capturedColor, true),
                    screenPosition =>
                    {
                        ShowPresetDeleteContext(screenPosition, () =>
                        {
                            ColorPresetStore.RemoveAt(capturedIndex);
                            RebuildPresetSwatches(control, rows);
                        });
                    });
            }
        }

        private void ShowPresetDeleteContext(Vector2 screenPosition, Action deleteAction)
        {
            CloseAuxiliary();

            var view = UnityEngine.Object.Instantiate(_prefabs.MenuPopupPrefab, _overlayRoot);
            _auxiliary = view.gameObject;
            _auxiliary.name = "PresetContextMenu";
            _auxiliaryOpenedFrame = Time.frameCount;
            var rt = view.RectTransform;
            rt.sizeDelta = new Vector2(160f, MenuPadding * 2f + MenuItemHeight);
            _auxiliaryBounds = rt;

            var item = UnityEngine.Object.Instantiate(_prefabs.MenuItemPrefab, view.Content);
            item.Initialize("Delete Preset", () =>
            {
                CloseAuxiliary();
                deleteAction?.Invoke();
            });

            SetScreenPosition(rt, screenPosition);
        }

        public void ShowConfirm(string message, Action yes, Action no = null)
        {
            Close();
            var view = UnityEngine.Object.Instantiate(_prefabs.DialogPrefab, _overlayRoot);
            _current = view.gameObject;
            _current.name = "ConfirmOverlay";
            _openedFrame = Time.frameCount;
            _dismissBounds = null;
            Stretch(view.RectTransform);
            view.Initialize(
                message,
                new DialogAction("Yes", () =>
                {
                    Close();
                    yes?.Invoke();
                }),
                new DialogAction("No", () =>
                {
                    Close();
                    no?.Invoke();
                }));
        }

        public void ShowSaveDiscardCancel(
            string message,
            Action saveAndContinue,
            Action discardAndContinue,
            Action cancel = null)
        {
            Close();
            var view = UnityEngine.Object.Instantiate(_prefabs.DialogPrefab, _overlayRoot);
            _current = view.gameObject;
            _current.name = "UnsavedChangesOverlay";
            _openedFrame = Time.frameCount;
            _dismissBounds = null;
            Stretch(view.RectTransform);
            view.Initialize(
                message,
                new DialogAction("Save", () =>
                {
                    Close();
                    saveAndContinue?.Invoke();
                }),
                new DialogAction("Don't Save", () =>
                {
                    Close();
                    discardAndContinue?.Invoke();
                }),
                new DialogAction("Cancel", () =>
                {
                    Close();
                    cancel?.Invoke();
                }));
        }

        private void SetScreenPosition(RectTransform target, Vector2 screenPosition)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlayRoot, screenPosition, null, out var local))
            {
                target.anchoredPosition = local + new Vector2(
                    _overlayRoot.pivot.x * _overlayRoot.rect.width,
                    _overlayRoot.pivot.y * _overlayRoot.rect.height);
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

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
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
}
