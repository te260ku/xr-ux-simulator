using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LightingScenarioTool
{
    internal sealed class ColorKeyframeView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private LightingScenarioApp _app;
        private string _unitId;
        private string _keyframeId;
        [SerializeField] private RectTransform _rt;
        [SerializeField] private Image _image;
        [SerializeField] private Outline _outline;
        [SerializeField] private RectTransform _selectionRing;
        private string _beforeDrag;
        private bool _dragging;
        private Vector2 _dragStartScreen;
        private Dictionary<string, float> _originalTimes;
        private float _primaryOriginalTime;

        private const float SelectionGap = 2f;
        private const float SelectionBorderThickness = 1.5f;

        internal RectTransform RectTransform => _rt;

        public void Initialize(LightingScenarioApp app, string unitId, string keyframeId)
        {
            _app = app;
            _unitId = unitId;
            _keyframeId = keyframeId;
            if (!HasRequiredVisual())
            {
                Debug.LogError(
                    "ColorKeyframe visual is incomplete. Regenerate the UI from " +
                    "Tools > Lighting Scenario > Build Complete UI In Current Scene.", this);
                enabled = false;
                return;
            }

            enabled = true;
            var key = _app.Document.FindColorKeyframe(unitId, keyframeId);
            _image.color = key != null ? key.color.ToUnityColor() : Color.black;
            _image.raycastTarget = true;
            RefreshPosition();
            RefreshSelection();
        }

        private void BindVisualReferences()
        {
            if (_rt == null) _rt = transform as RectTransform;
            if (_image == null) _image = GetComponent<Image>();
            if (_outline == null) _outline = GetComponent<Outline>();
            if (_selectionRing == null)
            {
                var ring = transform.Find("SelectionRing");
                _selectionRing = ring as RectTransform;
            }
        }

        internal bool HasRequiredVisual()
        {
            BindVisualReferences();
            return _rt != null && _image != null && _outline != null && _selectionRing != null;
        }

#if UNITY_EDITOR
        internal bool EnsureVisual()
        {
            BindVisualReferences();
            if (_rt == null) return false;

            var visualWasEmpty = _image == null && _outline == null;
            if (visualWasEmpty)
            {
                _rt.anchorMin = _rt.anchorMax = new Vector2(0f, 0.5f);
                _rt.pivot = new Vector2(0.5f, 0.5f);
                _rt.sizeDelta = new Vector2(14f, 14f);
                _rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            else if (_rt.sizeDelta.x <= 0f || _rt.sizeDelta.y <= 0f)
            {
                _rt.sizeDelta = new Vector2(14f, 14f);
            }

            _image ??= UiFactory.AddImage(gameObject, Color.black);
            _image.raycastTarget = true;
            _outline ??= gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            if (_outline.effectDistance == Vector2.zero)
                _outline.effectDistance = new Vector2(1f, 1f);
            if (_outline.effectColor.a <= 0f)
                _outline.effectColor = AppTheme.TextSecondary;

            EnsureSelectionRing();
            return HasRequiredVisual();
        }

        internal void BuildDefaultVisual()
        {
            _rt = transform as RectTransform;
            if (_rt == null) return;
            _rt.anchorMin = _rt.anchorMax = new Vector2(0f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = new Vector2(14f, 14f);
            _rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            _image = UiFactory.AddImage(gameObject, Color.black);
            _image.raycastTarget = true;
            _outline = gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            _outline.effectColor = AppTheme.TextSecondary;
            _outline.effectDistance = new Vector2(1f, 1f);
            EnsureSelectionRing();
        }



        private void EnsureSelectionRing()
        {
            if (_rt == null) _rt = transform as RectTransform;
            if (_selectionRing != null) return;

            var existing = transform.Find("SelectionRing") as RectTransform;
            if (existing != null)
            {
                _selectionRing = existing;
                return;
            }

            var ringGo = UiFactory.CreateUIObject("SelectionRing", transform);
            _selectionRing = (RectTransform)ringGo.transform;
            CreateSelectionBorderEdge("Top", _selectionRing);
            CreateSelectionBorderEdge("Bottom", _selectionRing);
            CreateSelectionBorderEdge("Left", _selectionRing);
            CreateSelectionBorderEdge("Right", _selectionRing);
            ConfigureSelectionRing(_selectionRing);
        }

        private void ConfigureSelectionRing(RectTransform ring)
        {
            if (ring == null || _rt == null) return;

            var outerSize = _rt.sizeDelta + Vector2.one * ((SelectionGap + SelectionBorderThickness) * 2f);
            ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
            ring.pivot = new Vector2(0.5f, 0.5f);
            ring.anchoredPosition = Vector2.zero;
            ring.sizeDelta = outerSize;
            ring.localRotation = Quaternion.identity;
            ring.localScale = Vector3.one;

            ConfigureSelectionBorderEdge(ring.Find("Top") as RectTransform, outerSize, BorderEdge.Top);
            ConfigureSelectionBorderEdge(ring.Find("Bottom") as RectTransform, outerSize, BorderEdge.Bottom);
            ConfigureSelectionBorderEdge(ring.Find("Left") as RectTransform, outerSize, BorderEdge.Left);
            ConfigureSelectionBorderEdge(ring.Find("Right") as RectTransform, outerSize, BorderEdge.Right);

            ring.gameObject.SetActive(false);
        }

        private static RectTransform CreateSelectionBorderEdge(string name, RectTransform parent)
        {
            var go = UiFactory.CreateUIObject(name, parent);
            var rt = (RectTransform)go.transform;
            var image = UiFactory.AddImage(go, AppTheme.Accent);
            image.raycastTarget = false;
            return rt;
        }

        private enum BorderEdge
        {
            Top,
            Bottom,
            Left,
            Right
        }

        private static void ConfigureSelectionBorderEdge(RectTransform edge, Vector2 outerSize, BorderEdge side)
        {
            if (edge == null) return;
            var image = edge.GetComponent<Image>() ?? UiFactory.AddImage(edge.gameObject, AppTheme.Accent);
            image.color = AppTheme.Accent;
            image.raycastTarget = false;
            edge.anchorMin = edge.anchorMax = new Vector2(0.5f, 0.5f);
            edge.pivot = new Vector2(0.5f, 0.5f);
            edge.localRotation = Quaternion.identity;
            edge.localScale = Vector3.one;

            var halfX = outerSize.x * 0.5f - SelectionBorderThickness * 0.5f;
            var halfY = outerSize.y * 0.5f - SelectionBorderThickness * 0.5f;
            switch (side)
            {
                case BorderEdge.Top:
                    edge.sizeDelta = new Vector2(outerSize.x, SelectionBorderThickness);
                    edge.anchoredPosition = new Vector2(0f, halfY);
                    break;
                case BorderEdge.Bottom:
                    edge.sizeDelta = new Vector2(outerSize.x, SelectionBorderThickness);
                    edge.anchoredPosition = new Vector2(0f, -halfY);
                    break;
                case BorderEdge.Left:
                    edge.sizeDelta = new Vector2(SelectionBorderThickness, outerSize.y);
                    edge.anchoredPosition = new Vector2(-halfX, 0f);
                    break;
                case BorderEdge.Right:
                    edge.sizeDelta = new Vector2(SelectionBorderThickness, outerSize.y);
                    edge.anchoredPosition = new Vector2(halfX, 0f);
                    break;
            }
        }

#endif

        public void RefreshPosition()
        {
            var key = _app.Document.FindColorKeyframe(_unitId, _keyframeId);
            if (key == null) return;
            _rt.anchoredPosition = new Vector2(
                TimelineCoordinates.TimeToX(
                    key.time,
                    _app.Document.Data.editorSettings.pixelsPerSecond),
                0f);
            _image.color = key.color.ToUnityColor();
        }

        public void RefreshSelection()
        {
            if (_image == null || _rt == null || _selectionRing == null) return;
            if (_selectionRing != null)
            {
                // Selection no longer changes border color based on the keyframe data color.
                // The scene-authored outer ring is simply shown/hidden.
                _selectionRing.gameObject.SetActive(_app.IsColorKeyframeSelected(_keyframeId));
            }
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
