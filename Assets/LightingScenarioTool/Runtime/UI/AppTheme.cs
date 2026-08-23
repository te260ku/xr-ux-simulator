using UnityEngine;
using UnityEngine.UI;

namespace LightingScenarioTool
{
    public enum AppButtonStyle
    {
        Secondary,
        Primary,
        Icon,
        Menu
    }

    /// <summary>
    /// Central visual theme for Lighting Scenario Tool.
    /// Attach this component to the same GameObject as LightingScenarioApp and edit colors in the Inspector.
    /// Runtime UI and dynamically-created UI read colors through the static accessors below.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AppTheme : MonoBehaviour
    {
        public const float SpacingXs = 4f;
        public const float SpacingS = 8f;
        public const float SpacingM = 12f;
        public const float SpacingL = 16f;
        public const float SpacingXl = 24f;
        public const float ControlHeight = 32f;
        public const float SmallControlHeight = 28f;

        public const int SectionTitleSize = 15;
        public const int BodySize = 13;
        public const int SecondarySize = 12;
        public const int TimelineScaleSize = 11;

        [Header("Surfaces")]
        [SerializeField] private Color background = new Color32(24, 24, 24, 255);
        [SerializeField] private Color panel = new Color32(32, 32, 32, 255);
        [SerializeField] private Color elevated = new Color32(41, 41, 41, 255);
        [SerializeField] private Color elevatedHover = new Color32(52, 52, 52, 255);
        [SerializeField] private Color elevatedPressed = new Color32(29, 29, 29, 255);
        [SerializeField] private Color inputBackground = new Color32(36, 36, 36, 255);
        [SerializeField] private Color previewCanvas = new Color32(17, 18, 20, 255);
        [SerializeField] private Color divider = new Color32(72, 72, 78, 255);

        [Header("Timeline")]
        [SerializeField] private Color trackEven = new Color32(29, 29, 31, 255);
        [SerializeField] private Color trackOdd = new Color32(32, 32, 35, 255);
        [SerializeField] private Color trackHeaderEven = new Color32(36, 36, 38, 255);
        [SerializeField] private Color trackHeaderOdd = new Color32(39, 39, 41, 255);
        [SerializeField] private Color grid = new Color(0.36f, 0.37f, 0.40f, 0.56f);
        [SerializeField] private Color playhead = new Color32(255, 107, 95, 255);

        [Header("Text")]
        [SerializeField] private Color textPrimary = new Color32(238, 238, 240, 255);
        [SerializeField] private Color textSecondary = new Color32(169, 171, 176, 255);
        [SerializeField] private Color textDisabled = new Color32(102, 104, 109, 255);

        [Header("Accent / Selection")]
        [SerializeField] private Color accent = new Color32(76, 141, 255, 255);
        [SerializeField] private Color accentHover = new Color32(99, 160, 255, 255);
        [SerializeField] private Color accentPressed = new Color32(53, 111, 221, 255);
        [SerializeField] private Color accentTint = new Color(0.17f, 0.25f, 0.38f, 1f);
        [SerializeField] private Color accentTintSoft = new Color(0.14f, 0.19f, 0.27f, 1f);

        [Header("Button State")]
        [SerializeField] private Color buttonDisabled = new Color(0.17f, 0.17f, 0.18f, 0.72f);

        [Header("Input State")]
        [SerializeField] private Color inputHighlighted = new Color32(43, 43, 45, 255);
        [SerializeField] private Color inputPressed = new Color32(43, 43, 45, 255);
        [SerializeField] private Color inputSelected = new Color32(48, 54, 64, 255);
        [SerializeField] private Color inputDisabled = new Color32(30, 30, 32, 255);

        [Header("Semantic")]
        [SerializeField] private Color error = new Color32(240, 106, 106, 255);
        [SerializeField] private Color warning = new Color32(228, 184, 92, 255);
        [SerializeField] private Color success = new Color32(109, 187, 134, 255);

        private static AppTheme _active;

        public static Color Background => Resolve().background;
        public static Color Panel => Resolve().panel;
        public static Color Elevated => Resolve().elevated;
        public static Color ElevatedHover => Resolve().elevatedHover;
        public static Color ElevatedPressed => Resolve().elevatedPressed;
        public static Color InputBackground => Resolve().inputBackground;
        public static Color PreviewCanvas => Resolve().previewCanvas;
        public static Color Divider => Resolve().divider;

        public static Color TrackEven => Resolve().trackEven;
        public static Color TrackOdd => Resolve().trackOdd;
        public static Color TrackHeaderEven => Resolve().trackHeaderEven;
        public static Color TrackHeaderOdd => Resolve().trackHeaderOdd;
        public static Color Grid => Resolve().grid;
        public static Color Playhead => Resolve().playhead;

        public static Color TextPrimary => Resolve().textPrimary;
        public static Color TextSecondary => Resolve().textSecondary;
        public static Color TextDisabled => Resolve().textDisabled;

        public static Color Accent => Resolve().accent;
        public static Color AccentHover => Resolve().accentHover;
        public static Color AccentPressed => Resolve().accentPressed;
        public static Color AccentTint => Resolve().accentTint;
        public static Color AccentTintSoft => Resolve().accentTintSoft;

        public static Color Error => Resolve().error;
        public static Color Warning => Resolve().warning;
        public static Color Success => Resolve().success;

        internal static void SetActive(AppTheme theme)
        {
            if (theme != null) _active = theme;
        }

        private void OnEnable()
        {
            SetActive(this);
        }

        private void OnDisable()
        {
            if (_active == this)
                _active = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Make Inspector edits immediately become the active source for editor-time UI generation.
            SetActive(this);
        }
#endif

        public static ColorBlock ButtonColors(AppButtonStyle style)
        {
            var theme = Resolve();
            var primary = style == AppButtonStyle.Primary;
            var menu = style == AppButtonStyle.Menu;
            return new ColorBlock
            {
                normalColor = primary ? theme.accent : menu ? theme.panel : theme.elevated,
                highlightedColor = primary ? theme.accentHover : theme.elevatedHover,
                pressedColor = primary ? theme.accentPressed : theme.elevatedPressed,
                selectedColor = primary ? theme.accentHover : theme.accentTint,
                disabledColor = theme.buttonDisabled,
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }

        public static ColorBlock InputColors()
        {
            var theme = Resolve();
            return new ColorBlock
            {
                normalColor = theme.inputBackground,
                highlightedColor = theme.inputHighlighted,
                pressedColor = theme.inputPressed,
                selectedColor = theme.inputSelected,
                disabledColor = theme.inputDisabled,
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
        }

        private static AppTheme Resolve()
        {
            if (_active != null) return _active;

            _active = Object.FindFirstObjectByType<AppTheme>(FindObjectsInactive.Exclude);
            if (_active != null) return _active;

            // UI generation can run while the authored root is inactive.
            _active = Object.FindFirstObjectByType<AppTheme>(FindObjectsInactive.Include);
            if (_active != null) return _active;

            // Defensive fallback for utility calls made before a scene AppTheme is available.
            var fallbackObject = new GameObject("LightingScenarioAppTheme_Fallback")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _active = fallbackObject.AddComponent<AppTheme>();
            return _active;
        }
    }
}
