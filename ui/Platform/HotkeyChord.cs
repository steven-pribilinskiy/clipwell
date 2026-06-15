using System;
using System.Collections.Generic;
using System.Text;

namespace Clipwell.Ui.Platform;

/// <summary>
/// A cross-platform hotkey chord (modifiers + a single key), parsed from / formatted
/// to a string like "Alt+Shift+V". Each <see cref="IGlobalHotkey"/> maps it to its
/// platform's native codes. Supports letters, digits, and F1–F12.
/// </summary>
public sealed record HotkeyChord(bool Alt, bool Ctrl, bool Shift, bool Win, string Key)
{
    public static readonly HotkeyChord Default = new(Alt: true, Ctrl: false, Shift: true, Win: false, Key: "V");

    public string Display
    {
        get
        {
            var sb = new StringBuilder();
            if (Ctrl) sb.Append("Ctrl+");
            if (Alt) sb.Append("Alt+");
            if (Shift) sb.Append("Shift+");
            if (Win) sb.Append("Win+");
            sb.Append(Key);
            return sb.ToString();
        }
    }

    public static HotkeyChord Parse(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return Default;
        bool alt = false, ctrl = false, shift = false, win = false;
        string key = "V";
        foreach (var raw in s.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "alt" or "option" or "opt": alt = true; break;
                case "ctrl" or "control": ctrl = true; break;
                case "shift": shift = true; break;
                case "win" or "super" or "cmd" or "meta": win = true; break;
                default: key = raw.ToUpperInvariant(); break;
            }
        }
        if (!alt && !ctrl && !shift && !win) return Default; // require a modifier
        return new HotkeyChord(alt, ctrl, shift, win, key);
    }

    // ── Windows (RegisterHotKey): MOD_* flags + virtual-key code ──────────
    public uint WinModifiers()
    {
        uint m = 0;
        if (Alt) m |= 0x1;   // MOD_ALT
        if (Ctrl) m |= 0x2;  // MOD_CONTROL
        if (Shift) m |= 0x4; // MOD_SHIFT
        if (Win) m |= 0x8;   // MOD_WIN
        return m;
    }

    public uint WinVk() => Vk(Key);

    private static uint Vk(string key)
    {
        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            if (c is >= 'A' and <= 'Z') return c;            // VK letters = ASCII upper
            if (c is >= '0' and <= '9') return c;            // VK digits = ASCII
        }
        if (key.Length >= 2 && (key[0] is 'F' or 'f') && int.TryParse(key[1..], out var n) && n is >= 1 and <= 12)
            return (uint)(0x70 + (n - 1));                   // VK_F1..VK_F12
        return 0x56; // fall back to 'V'
    }

    // ── Linux (XGrabKey): X11 modifier mask + keysym ─────────────────────
    public uint X11Modifiers()
    {
        uint m = 0;
        if (Shift) m |= 0x01;        // ShiftMask
        if (Ctrl) m |= 0x04;         // ControlMask
        if (Alt) m |= 0x08;          // Mod1Mask
        if (Win) m |= 0x40;          // Mod4Mask (Super)
        return m;
    }

    public ulong X11Keysym()
    {
        if (Key.Length == 1)
        {
            var c = char.ToLowerInvariant(Key[0]);
            if (c is >= 'a' and <= 'z') return (ulong)c;     // keysym = ASCII lower
            if (c is >= '0' and <= '9') return (ulong)c;
        }
        if (Key.Length >= 2 && (Key[0] is 'F' or 'f') && int.TryParse(Key[1..], out var n) && n is >= 1 and <= 12)
            return (ulong)(0xFFBE + (n - 1));                // XK_F1..XK_F12
        return 0x0076; // 'v'
    }

    // ── macOS (RegisterEventHotKey): Carbon modifier flags + key code ────
    public uint MacModifiers()
    {
        uint m = 0;
        if (Shift) m |= 0x0200;   // shiftKey
        if (Ctrl) m |= 0x1000;    // controlKey
        if (Alt) m |= 0x0800;     // optionKey
        if (Win) m |= 0x0100;     // cmdKey
        return m;
    }

    public uint MacKeyCode()
    {
        if (Key.Length == 1 && MacKeyCodes.TryGetValue(char.ToUpperInvariant(Key[0]), out var code))
            return code;
        return 0x09; // 'V'
    }

    // ANSI virtual key codes for the standard US layout.
    private static readonly Dictionary<char, uint> MacKeyCodes = new()
    {
        ['A']=0,['S']=1,['D']=2,['F']=3,['H']=4,['G']=5,['Z']=6,['X']=7,['C']=8,['V']=9,
        ['B']=11,['Q']=12,['W']=13,['E']=14,['R']=15,['Y']=16,['T']=17,['O']=31,['U']=32,
        ['I']=34,['P']=35,['L']=37,['J']=38,['K']=40,['N']=45,['M']=46,
        ['1']=18,['2']=19,['3']=20,['4']=21,['6']=22,['5']=23,['9']=25,['7']=26,['8']=28,['0']=29,
    };
}
