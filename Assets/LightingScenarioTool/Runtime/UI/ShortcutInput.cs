using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LightingScenarioTool
{
    /// <summary>
    /// Small compatibility layer for keyboard shortcuts. It works with both
    /// Unity's new Input System and the legacy Input Manager depending on the
    /// project's Active Input Handling setting.
    /// </summary>
    internal static class ShortcutInput
    {
        public static bool CtrlPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var keyboard = Keyboard.current;
                return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#else
                return false;
#endif
            }
        }

        public static bool ShiftPressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var keyboard = Keyboard.current;
                return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
                return false;
#endif
            }
        }

        public static bool ZPressedThisFrame => WasPressed(KeyCode.Z);
        public static bool NPressedThisFrame => WasPressed(KeyCode.N);
        public static bool OPressedThisFrame => WasPressed(KeyCode.O);
        public static bool SPressedThisFrame => WasPressed(KeyCode.S);
        public static bool CPressedThisFrame => WasPressed(KeyCode.C);
        public static bool VPressedThisFrame => WasPressed(KeyCode.V);
        public static bool DPressedThisFrame => WasPressed(KeyCode.D);
        public static bool DeletePressedThisFrame => WasPressed(KeyCode.Delete);
        public static bool HomePressedThisFrame => WasPressed(KeyCode.Home);
        public static bool EndPressedThisFrame => WasPressed(KeyCode.End);
        public static bool SpacePressedThisFrame => WasPressed(KeyCode.Space);
        public static bool EscapePressedThisFrame => WasPressed(KeyCode.Escape);

        public static Vector2 PointerPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePosition;
#else
                return Vector2.zero;
#endif
            }
        }

        public static bool MiddleMousePressed
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null && mouse.middleButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetMouseButton(2);
#else
                return false;
#endif
            }
        }

        public static bool MiddleMousePressedThisFrame
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                var mouse = Mouse.current;
                return mouse != null && mouse.middleButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetMouseButtonDown(2);
#else
                return false;
#endif
            }
        }

        private static bool WasPressed(KeyCode keyCode)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;

            switch (keyCode)
            {
                case KeyCode.Z: return keyboard.zKey.wasPressedThisFrame;
                case KeyCode.N: return keyboard.nKey.wasPressedThisFrame;
                case KeyCode.O: return keyboard.oKey.wasPressedThisFrame;
                case KeyCode.S: return keyboard.sKey.wasPressedThisFrame;
                case KeyCode.C: return keyboard.cKey.wasPressedThisFrame;
                case KeyCode.V: return keyboard.vKey.wasPressedThisFrame;
                case KeyCode.D: return keyboard.dKey.wasPressedThisFrame;
                case KeyCode.Delete: return keyboard.deleteKey.wasPressedThisFrame;
                case KeyCode.Home: return keyboard.homeKey.wasPressedThisFrame;
                case KeyCode.End: return keyboard.endKey.wasPressedThisFrame;
                case KeyCode.Space: return keyboard.spaceKey.wasPressedThisFrame;
                case KeyCode.Escape: return keyboard.escapeKey.wasPressedThisFrame;
                default: return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#else
            return false;
#endif
        }
    }
}
