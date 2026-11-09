using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Invoer die zowel met het nieuwe Input System als met de oude Input Manager werkt.
public enum K { W, A, S, D, Q, E, X, Z, C, Tab, Shift, Ctrl, Space, Esc, D1, D2, D3, D4, D5, D6, D7, D8, D9, M, N, D0, G, R, H, B, O, L, P, T, LBr, RBr, Del, F, K, U, Up, Down, Left, Right, Plus, Minus, Slash, Back, Alt, I, PgUp, PgDn, V, Backquote, J }

public static class Inp
{
    static K? fake; public static void Fake(K k) { fake = k; }   // een knop in de interface die een toets nadoet
    static bool TakeFake(K k) { if (fake == k) { fake = null; return true; } return false; }

#if ENABLE_INPUT_SYSTEM
    static Key Map(K k)
    {
        switch (k)
        {
            case K.W: return Key.W; case K.A: return Key.A; case K.S: return Key.S; case K.D: return Key.D;
            case K.Q: return Key.Q; case K.E: return Key.E; case K.X: return Key.X; case K.Z: return Key.Z; case K.C: return Key.C;
            case K.Tab: return Key.Tab; case K.Shift: return Key.LeftShift; case K.Ctrl: return Key.LeftCtrl;
            case K.Space: return Key.Space; case K.M: return Key.M; case K.N: return Key.N; case K.D0: return Key.Digit0; case K.G: return Key.G; case K.R: return Key.R; case K.H: return Key.H; case K.B: return Key.B; case K.O: return Key.O; case K.L: return Key.L; case K.P: return Key.P; case K.T: return Key.T; case K.LBr: return Key.LeftBracket; case K.RBr: return Key.RightBracket; case K.Del: return Key.Delete; case K.F: return Key.F; case K.K: return Key.K; case K.U: return Key.U; case K.V: return Key.V; case K.Backquote: return Key.Backquote; case K.J: return Key.J;
            case K.Up: return Key.UpArrow; case K.Down: return Key.DownArrow; case K.Left: return Key.LeftArrow; case K.Right: return Key.RightArrow; case K.Plus: return Key.Equals; case K.Minus: return Key.Minus; case K.Slash: return Key.Slash; case K.Back: return Key.Backspace; case K.Alt: return Key.LeftAlt; case K.I: return Key.I; case K.PgUp: return Key.PageUp; case K.PgDn: return Key.PageDown;
            case K.D1: return Key.Digit1; case K.D2: return Key.Digit2; case K.D3: return Key.Digit3; case K.D4: return Key.Digit4; case K.D5: return Key.Digit5;
            case K.D6: return Key.Digit6; case K.D7: return Key.Digit7; case K.D8: return Key.Digit8; case K.D9: return Key.Digit9; default: return Key.Escape;
        }
    }
    // een tweede toets voor dezelfde handeling: rechter Shift/Ctrl/Alt, + en − op het numerieke deel
    static Key Map2(K k) => k == K.Shift ? Key.RightShift : k == K.Ctrl ? Key.RightCtrl : k == K.Alt ? Key.RightAlt : k == K.Plus ? Key.NumpadPlus : k == K.Minus ? Key.NumpadMinus : Key.None;
    public static bool Held(K k) { var kb = Keyboard.current; if (kb == null) return false; Key k2 = Map2(k); return kb[Map(k)].isPressed || (k2 != Key.None && kb[k2].isPressed); }
    public static bool Down(K k) { if (TakeFake(k)) return true; var kb = Keyboard.current; if (kb == null) return false; Key k2 = Map2(k); return kb[Map(k)].wasPressedThisFrame || (k2 != Key.None && kb[k2].wasPressedThisFrame); }
    public static Vector2 Pos => Mouse_() != null ? Mouse_().position.ReadValue() : Vector2.zero;
    static UnityEngine.InputSystem.Mouse Mouse_() => UnityEngine.InputSystem.Mouse.current;
    public static bool LeftDown => Mouse_() != null && Mouse_().leftButton.wasPressedThisFrame;
    public static bool LeftHeld => Mouse_() != null && Mouse_().leftButton.isPressed;
    public static bool LeftUp => Mouse_() != null && Mouse_().leftButton.wasReleasedThisFrame;
    public static bool RightHeld => Mouse_() != null && Mouse_().rightButton.isPressed;
    public static bool RightDown => Mouse_() != null && Mouse_().rightButton.wasPressedThisFrame;
    public static bool RightUp => Mouse_() != null && Mouse_().rightButton.wasReleasedThisFrame;
    public static bool MiddleHeld => Mouse_() != null && Mouse_().middleButton.isPressed;
    public static bool MiddleDown => Mouse_() != null && Mouse_().middleButton.wasPressedThisFrame;
    public static Vector2 Delta => Mouse_() != null ? Mouse_().delta.ReadValue() : Vector2.zero;
    public static float Scroll { get { if (Mouse_() == null) return 0f; float y = Mouse_().scroll.ReadValue().y; return Mathf.Abs(y) > 10f ? y / 120f : y; } }   // per klikje ±1 (oude Input System-versies geven ±120)
#else
    static KeyCode Map(K k)
    {
        switch (k)
        {
            case K.W: return KeyCode.W; case K.A: return KeyCode.A; case K.S: return KeyCode.S; case K.D: return KeyCode.D;
            case K.Q: return KeyCode.Q; case K.E: return KeyCode.E; case K.X: return KeyCode.X; case K.Z: return KeyCode.Z; case K.C: return KeyCode.C;
            case K.Tab: return KeyCode.Tab; case K.Shift: return KeyCode.LeftShift; case K.Ctrl: return KeyCode.LeftControl;
            case K.Space: return KeyCode.Space; case K.M: return KeyCode.M; case K.N: return KeyCode.N; case K.D0: return KeyCode.Alpha0; case K.G: return KeyCode.G; case K.R: return KeyCode.R; case K.H: return KeyCode.H; case K.B: return KeyCode.B; case K.O: return KeyCode.O; case K.L: return KeyCode.L; case K.P: return KeyCode.P; case K.T: return KeyCode.T; case K.LBr: return KeyCode.LeftBracket; case K.RBr: return KeyCode.RightBracket; case K.Del: return KeyCode.Delete; case K.F: return KeyCode.F; case K.K: return KeyCode.K; case K.U: return KeyCode.U; case K.V: return KeyCode.V; case K.Backquote: return KeyCode.BackQuote; case K.J: return KeyCode.J;
            case K.Up: return KeyCode.UpArrow; case K.Down: return KeyCode.DownArrow; case K.Left: return KeyCode.LeftArrow; case K.Right: return KeyCode.RightArrow; case K.Plus: return KeyCode.Equals; case K.Minus: return KeyCode.Minus; case K.Slash: return KeyCode.Slash; case K.Back: return KeyCode.Backspace; case K.Alt: return KeyCode.LeftAlt; case K.I: return KeyCode.I; case K.PgUp: return KeyCode.PageUp; case K.PgDn: return KeyCode.PageDown;
            case K.D1: return KeyCode.Alpha1; case K.D2: return KeyCode.Alpha2; case K.D3: return KeyCode.Alpha3; case K.D4: return KeyCode.Alpha4; case K.D5: return KeyCode.Alpha5;
            case K.D6: return KeyCode.Alpha6; case K.D7: return KeyCode.Alpha7; case K.D8: return KeyCode.Alpha8; case K.D9: return KeyCode.Alpha9; default: return KeyCode.Escape;
        }
    }
    static KeyCode Map2(K k) => k == K.Shift ? KeyCode.RightShift : k == K.Ctrl ? KeyCode.RightControl : k == K.Alt ? KeyCode.RightAlt : k == K.Plus ? KeyCode.KeypadPlus : k == K.Minus ? KeyCode.KeypadMinus : KeyCode.None;
    public static bool Held(K k) => Input.GetKey(Map(k)) || Input.GetKey(Map2(k));
    public static bool Down(K k) => TakeFake(k) || Input.GetKeyDown(Map(k)) || Input.GetKeyDown(Map2(k));
    public static Vector2 Pos => Input.mousePosition;
    public static bool LeftDown => Input.GetMouseButtonDown(0);
    public static bool LeftHeld => Input.GetMouseButton(0);
    public static bool LeftUp => Input.GetMouseButtonUp(0);
    public static bool RightHeld => Input.GetMouseButton(1);
    public static bool RightDown => Input.GetMouseButtonDown(1);
    public static bool RightUp => Input.GetMouseButtonUp(1);
    public static bool MiddleHeld => Input.GetMouseButton(2);
    public static bool MiddleDown => Input.GetMouseButtonDown(2);
    public static Vector2 Delta => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
    public static float Scroll => Input.mouseScrollDelta.y;
#endif
}
