using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace LightingScenarioTool
{
    internal sealed class PreviewLightView : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private LightingScenarioApp _app;
        private string _unitId;
        [SerializeField] private RectTransform _rt;
        [SerializeField] private Image _image;
        [SerializeField] private Outline _outline;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private RectTransform _labelBackgroundRt;
        private string _beforeDrag;

        public void Initialize(LightingScenarioApp app, string unitId)
        {
            _app = app;
            _unitId = unitId;
            if (!HasRequiredVisual())
            {
                Debug.LogError(
                    "Preview light visual is incomplete. Regenerate the UI from " +
                    "Tools > Lighting Scenario > Build Complete UI In Current Scene.", this);
                enabled = false;
                return;
            }

            enabled = true;
            RefreshSize();
            RefreshPosition();
            RefreshName();
        }

        private void BindVisualReferences()
        {
            if (_rt == null) _rt = (RectTransform)transform;
            if (_image == null) _image = GetComponent<Image>();
            if (_outline == null) _outline = GetComponent<Outline>();
            var labelBg = transform.Find("LabelBackground");
            if (_labelBackgroundRt == null) _labelBackgroundRt = labelBg as RectTransform;
            if (_label == null && labelBg != null) _label = labelBg.GetComponentInChildren<TMP_Text>(true);
        }

        public bool HasRequiredVisual()
        {
            BindVisualReferences();
            return _rt != null && _image != null && _outline != null &&
                   _label != null && _labelBackgroundRt != null;
        }

#if UNITY_EDITOR
        internal bool EnsureVisual()
        {
            BindVisualReferences();

            _rt ??= (RectTransform)transform;
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 0.5f);
            if (_rt.sizeDelta.x <= 0f || _rt.sizeDelta.y <= 0f)
                _rt.sizeDelta = new Vector2(54f, 54f);

            _image ??= UiFactory.AddImage(gameObject, Color.black);
            _outline ??= gameObject.GetComponent<Outline>() ?? gameObject.AddComponent<Outline>();
            _outline.effectColor = AppTheme.Accent;
            _outline.effectDistance = new Vector2(2f, 2f);
            _outline.enabled = false;

            if (_labelBackgroundRt == null)
            {
                var existingLabelBg = transform.Find("LabelBackground");
                _labelBackgroundRt = existingLabelBg as RectTransform;
            }

            if (_labelBackgroundRt == null)
            {
                var labelBgGo = UiFactory.CreateUIObject("LabelBackground", transform);
                _labelBackgroundRt = (RectTransform)labelBgGo.transform;
                _labelBackgroundRt.anchorMin = _labelBackgroundRt.anchorMax = new Vector2(0.5f, 1f);
                _labelBackgroundRt.pivot = new Vector2(0.5f, 0f);
                _labelBackgroundRt.anchoredPosition = new Vector2(0f, 5f);
                _labelBackgroundRt.sizeDelta = new Vector2(112f, 22f);
                UiFactory.AddImage(labelBgGo, new Color(0.06f, 0.06f, 0.07f, 0.78f)).raycastTarget = false;
            }
            else
            {
                var labelBgImage = _labelBackgroundRt.GetComponent<Image>();
                if (labelBgImage == null)
                    UiFactory.AddImage(_labelBackgroundRt.gameObject, new Color(0.06f, 0.06f, 0.07f, 0.78f)).raycastTarget = false;
            }

            if (_label == null)
                _label = _labelBackgroundRt.GetComponentInChildren<TMP_Text>(true);

            if (_label == null)
            {
                var labelTransform = _labelBackgroundRt.Find("Label");
                var labelGo = labelTransform != null
                    ? labelTransform.gameObject
                    : UiFactory.CreateUIObject("Label", _labelBackgroundRt);

                var labelRt = (RectTransform)labelGo.transform;
                UiFactory.Stretch(labelRt);
                labelRt.offsetMin = new Vector2(6f, 2f);
                labelRt.offsetMax = new Vector2(-6f, -2f);
                _label = UiFactory.AddText(labelGo, "Light", AppTheme.SecondarySize, TextAnchor.MiddleCenter);
            }

            if (_label != null)
            {
                _label.color = AppTheme.TextPrimary;
                _label.raycastTarget = false;
                _label.overflowMode = TextOverflowModes.Ellipsis;
            }

            BindVisualReferences();
            return HasRequiredVisual();
        }


#endif

        public void RefreshSize()
        {
            if (_rt == null || _app == null) return;
            var size = _app.PreviewLightSize;
            _rt.sizeDelta = new Vector2(size, size);
            if (_labelBackgroundRt != null)
                _labelBackgroundRt.sizeDelta = new Vector2(Mathf.Clamp(Mathf.Max(112f, size * 1.7f), 112f, 180f), 22f);
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

        public void SetNameVisible(bool visible)
        {
            if (_labelBackgroundRt != null)
                _labelBackgroundRt.gameObject.SetActive(visible);
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
            if (_app.Document.FindUnit(_unitId) == null || parent.rect.width <= 0f || parent.rect.height <= 0f) return;
            var halfWidth = _rt.rect.width * 0.5f;
            var halfHeight = _rt.rect.height * 0.5f;
            var minX = Mathf.Min(0.5f, halfWidth / parent.rect.width);
            var minY = Mathf.Min(0.5f, halfHeight / parent.rect.height);
            var topMargin = Mathf.Min(0.5f, (halfHeight + 21f) / parent.rect.height);
            var x = Mathf.Clamp(local.x / parent.rect.width + parent.pivot.x, minX, 1f - minX);
            var y = Mathf.Clamp(local.y / parent.rect.height + parent.pivot.y, minY, 1f - topMargin);
            if (_app.Document.TrySetUnitPreviewPositionNoHistory(_unitId, x, y))
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
