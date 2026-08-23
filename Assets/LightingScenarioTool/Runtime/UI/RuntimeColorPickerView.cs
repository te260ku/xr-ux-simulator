using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LightingScenarioTool
{
    internal sealed class RuntimeColorPickerView : MonoBehaviour
    {
        [SerializeField] private RectTransform _pickerRoot;
        [SerializeField] private RawImage _hueRing;
        [SerializeField] private RawImage _svSquare;
        [SerializeField] private RectTransform _hueMarker;
        [SerializeField] private RectTransform _svMarker;
        [SerializeField] private Image _preview;
        [SerializeField] private TMP_InputField _r;
        [SerializeField] private TMP_InputField _g;
        [SerializeField] private TMP_InputField _b;
        [SerializeField] private TMP_InputField _h;
        [SerializeField] private TMP_InputField _s;
        [SerializeField] private TMP_InputField _v;
        [SerializeField] private Button _savePresetButton;
        [SerializeField] private Transform[] _presetRows;
        [SerializeField] private HsvColorPickerControl _control;
        [SerializeField] private HsvHueRingInput _hueInput;
        [SerializeField] private HsvSvSquareInput _svInput;

        internal RectTransform RectTransform => transform as RectTransform;
        internal RectTransform PickerRoot => _pickerRoot;
        internal RawImage HueRing => _hueRing;
        internal RawImage SvSquare => _svSquare;
        internal RectTransform HueMarker => _hueMarker;
        internal RectTransform SvMarker => _svMarker;
        internal Image Preview => _preview;
        internal TMP_InputField R => _r;
        internal TMP_InputField G => _g;
        internal TMP_InputField B => _b;
        internal TMP_InputField H => _h;
        internal TMP_InputField S => _s;
        internal TMP_InputField V => _v;
        internal Button SavePresetButton => _savePresetButton;
        internal Transform[] PresetRows => _presetRows;
        internal HsvColorPickerControl Control => _control;
        internal HsvHueRingInput HueInput => _hueInput;
        internal HsvSvSquareInput SvInput => _svInput;

#if UNITY_EDITOR
        internal void EditorConfigure(
            RectTransform pickerRoot,
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
            Button savePresetButton,
            Transform[] presetRows,
            HsvColorPickerControl control,
            HsvHueRingInput hueInput,
            HsvSvSquareInput svInput)
        {
            _pickerRoot = pickerRoot;
            _hueRing = hueRing;
            _svSquare = svSquare;
            _hueMarker = hueMarker;
            _svMarker = svMarker;
            _preview = preview;
            _r = r;
            _g = g;
            _b = b;
            _h = h;
            _s = s;
            _v = v;
            _savePresetButton = savePresetButton;
            _presetRows = presetRows;
            _control = control;
            _hueInput = hueInput;
            _svInput = svInput;
        }
#endif
    }
}
