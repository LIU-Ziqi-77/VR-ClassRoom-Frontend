using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

/// <summary>
/// Small compatibility layer for editor/demo keyboard and mouse controls.
/// It uses the new Input System when the legacy Input Manager is disabled.
/// </summary>
public static class DesktopInputBridge
{
    public static bool GetKey(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (TryGetInputSystemKey(key, out Key inputKey) && Keyboard.current != null)
        {
            return Keyboard.current[inputKey].isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(key);
#else
        return false;
#endif
    }

    public static bool GetKeyDown(KeyCode key)
    {
#if ENABLE_INPUT_SYSTEM
        if (TryGetInputSystemKey(key, out Key inputKey) && Keyboard.current != null)
        {
            return Keyboard.current[inputKey].wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(key);
#else
        return false;
#endif
    }

    public static bool GetMouseButton(int button)
    {
#if ENABLE_INPUT_SYSTEM
        ButtonControl mouseButton = GetMouseButtonControl(button);
        if (mouseButton != null)
        {
            return mouseButton.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(button);
#else
        return false;
#endif
    }

    public static bool GetMouseButtonDown(int button)
    {
#if ENABLE_INPUT_SYSTEM
        ButtonControl mouseButton = GetMouseButtonControl(button);
        if (mouseButton != null)
        {
            return mouseButton.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(button);
#else
        return false;
#endif
    }

    public static bool GetMouseButtonUp(int button)
    {
#if ENABLE_INPUT_SYSTEM
        ButtonControl mouseButton = GetMouseButtonControl(button);
        if (mouseButton != null)
        {
            return mouseButton.wasReleasedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonUp(button);
#else
        return false;
#endif
    }

    public static Vector2 GetMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.delta.ReadValue();
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
        return Vector2.zero;
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static ButtonControl GetMouseButtonControl(int button)
    {
        if (Mouse.current == null) return null;

        switch (button)
        {
            case 0:
                return Mouse.current.leftButton;
            case 1:
                return Mouse.current.rightButton;
            case 2:
                return Mouse.current.middleButton;
            default:
                return null;
        }
    }

    private static bool TryGetInputSystemKey(KeyCode key, out Key inputKey)
    {
        switch (key)
        {
            case KeyCode.A:
                inputKey = Key.A;
                return true;
            case KeyCode.B:
                inputKey = Key.B;
                return true;
            case KeyCode.C:
                inputKey = Key.C;
                return true;
            case KeyCode.D:
                inputKey = Key.D;
                return true;
            case KeyCode.E:
                inputKey = Key.E;
                return true;
            case KeyCode.F9:
                inputKey = Key.F9;
                return true;
            case KeyCode.H:
                inputKey = Key.H;
                return true;
            case KeyCode.J:
                inputKey = Key.J;
                return true;
            case KeyCode.K:
                inputKey = Key.K;
                return true;
            case KeyCode.L:
                inputKey = Key.L;
                return true;
            case KeyCode.N:
                inputKey = Key.N;
                return true;
            case KeyCode.P:
                inputKey = Key.P;
                return true;
            case KeyCode.Q:
                inputKey = Key.Q;
                return true;
            case KeyCode.R:
                inputKey = Key.R;
                return true;
            case KeyCode.S:
                inputKey = Key.S;
                return true;
            case KeyCode.T:
                inputKey = Key.T;
                return true;
            case KeyCode.U:
                inputKey = Key.U;
                return true;
            case KeyCode.W:
                inputKey = Key.W;
                return true;
            case KeyCode.X:
                inputKey = Key.X;
                return true;
            case KeyCode.Y:
                inputKey = Key.Y;
                return true;
            case KeyCode.Tab:
                inputKey = Key.Tab;
                return true;
            case KeyCode.LeftShift:
                inputKey = Key.LeftShift;
                return true;
            case KeyCode.Alpha0:
                inputKey = Key.Digit0;
                return true;
            case KeyCode.Alpha1:
                inputKey = Key.Digit1;
                return true;
            case KeyCode.Alpha2:
                inputKey = Key.Digit2;
                return true;
            case KeyCode.Alpha3:
                inputKey = Key.Digit3;
                return true;
            case KeyCode.Alpha4:
                inputKey = Key.Digit4;
                return true;
            case KeyCode.Alpha5:
                inputKey = Key.Digit5;
                return true;
            case KeyCode.Alpha6:
                inputKey = Key.Digit6;
                return true;
            case KeyCode.Alpha7:
                inputKey = Key.Digit7;
                return true;
            case KeyCode.Alpha8:
                inputKey = Key.Digit8;
                return true;
            case KeyCode.Alpha9:
                inputKey = Key.Digit9;
                return true;
            case KeyCode.Keypad0:
                inputKey = Key.Numpad0;
                return true;
            case KeyCode.Keypad1:
                inputKey = Key.Numpad1;
                return true;
            case KeyCode.Keypad2:
                inputKey = Key.Numpad2;
                return true;
            case KeyCode.Keypad3:
                inputKey = Key.Numpad3;
                return true;
            case KeyCode.Keypad4:
                inputKey = Key.Numpad4;
                return true;
            case KeyCode.Keypad5:
                inputKey = Key.Numpad5;
                return true;
            case KeyCode.Keypad6:
                inputKey = Key.Numpad6;
                return true;
            case KeyCode.Keypad7:
                inputKey = Key.Numpad7;
                return true;
            case KeyCode.Keypad8:
                inputKey = Key.Numpad8;
                return true;
            case KeyCode.Keypad9:
                inputKey = Key.Numpad9;
                return true;
            default:
                inputKey = Key.None;
                return false;
        }
    }
#endif
}
