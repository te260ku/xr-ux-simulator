using UnityEngine;
using TMPro;

namespace LightingScenarioTool
{
    public sealed class LightingScenarioUiPrefabCatalog : ScriptableObject
    {
        [Header("Timeline Widgets")]
        [SerializeField] private GameObject _trackLabelPrefab;
        [SerializeField] private GameObject _trackTimePrefab;
        [SerializeField] private ColorKeyframeView _colorKeyframePrefab;
        [SerializeField] private TMP_Text _rulerLabelPrefab;
        [SerializeField] private GameObject _rulerInteractionPrefab;
        [SerializeField] private GameObject _timePlayheadPrefab;
        [SerializeField] private GameObject _rulerPlayheadPrefab;

        [Header("Preview Widgets")]
        [SerializeField] private PreviewLightView _previewLightPrefab;

        [Header("Popup Widgets")]
        [SerializeField] private RuntimeMenuPopupView _menuPopupPrefab;
        [SerializeField] private RuntimeMenuItemView _menuItemPrefab;
        [SerializeField] private RuntimeColorPickerView _colorPickerPrefab;
        [SerializeField] private RuntimePresetSwatchView _presetSwatchPrefab;
        [SerializeField] private RuntimeDialogView _dialogPrefab;

        internal GameObject TrackLabelPrefab => _trackLabelPrefab;
        internal GameObject TrackTimePrefab => _trackTimePrefab;
        internal ColorKeyframeView ColorKeyframePrefab => _colorKeyframePrefab;
        internal TMP_Text RulerLabelPrefab => _rulerLabelPrefab;
        internal GameObject RulerInteractionPrefab => _rulerInteractionPrefab;
        internal GameObject TimePlayheadPrefab => _timePlayheadPrefab;
        internal GameObject RulerPlayheadPrefab => _rulerPlayheadPrefab;
        internal PreviewLightView PreviewLightPrefab => _previewLightPrefab;
        internal RuntimeMenuPopupView MenuPopupPrefab => _menuPopupPrefab;
        internal RuntimeMenuItemView MenuItemPrefab => _menuItemPrefab;
        internal RuntimeColorPickerView ColorPickerPrefab => _colorPickerPrefab;
        internal RuntimePresetSwatchView PresetSwatchPrefab => _presetSwatchPrefab;
        internal RuntimeDialogView DialogPrefab => _dialogPrefab;

        internal bool IsComplete =>
            _trackLabelPrefab != null && _trackTimePrefab != null &&
            _colorKeyframePrefab != null && _rulerLabelPrefab != null &&
            _rulerInteractionPrefab != null && _timePlayheadPrefab != null && _rulerPlayheadPrefab != null &&
            _previewLightPrefab != null && _menuPopupPrefab != null &&
            _menuItemPrefab != null &&
            _colorPickerPrefab != null && _presetSwatchPrefab != null &&
            _dialogPrefab != null;

#if UNITY_EDITOR
        internal void EditorAssign(
            GameObject trackLabelPrefab,
            GameObject trackTimePrefab,
            ColorKeyframeView colorKeyframePrefab,
            TMP_Text rulerLabelPrefab,
            GameObject rulerInteractionPrefab,
            GameObject timePlayheadPrefab,
            GameObject rulerPlayheadPrefab,
            PreviewLightView previewLightPrefab,
            RuntimeMenuPopupView menuPopupPrefab,
            RuntimeMenuItemView menuItemPrefab,
            RuntimeColorPickerView colorPickerPrefab,
            RuntimePresetSwatchView presetSwatchPrefab,
            RuntimeDialogView dialogPrefab)
        {
            _trackLabelPrefab = trackLabelPrefab;
            _trackTimePrefab = trackTimePrefab;
            _colorKeyframePrefab = colorKeyframePrefab;
            _rulerLabelPrefab = rulerLabelPrefab;
            _rulerInteractionPrefab = rulerInteractionPrefab;
            _timePlayheadPrefab = timePlayheadPrefab;
            _rulerPlayheadPrefab = rulerPlayheadPrefab;
            _previewLightPrefab = previewLightPrefab;
            _menuPopupPrefab = menuPopupPrefab;
            _menuItemPrefab = menuItemPrefab;
            _colorPickerPrefab = colorPickerPrefab;
            _presetSwatchPrefab = presetSwatchPrefab;
            _dialogPrefab = dialogPrefab;
        }
#endif
    }
}
