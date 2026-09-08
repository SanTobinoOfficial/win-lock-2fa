using System.Runtime.InteropServices;

namespace WinLock2FA;

/// <summary>
/// P/Invoke declarations for the low-level keyboard hook used to discourage
/// switching away from the lock screen (Alt+Tab, Win key, Alt+F4, Ctrl+Esc).
///
/// NOTE: This intentionally cannot and does not touch Ctrl+Alt+Del. That key
/// combination is a protected "Secure Attention Sequence" handled directly by
/// the Windows kernel/Winlogon and is not interceptable by any user-mode hook.
/// It is left untouched on purpose so the user always has a way out via
/// Task Manager -> End Task.
/// </summary>
internal static class NativeMethods
{
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_SYSKEYDOWN = 0x0104;

    public const int VK_TAB = 0x09;
    public const int VK_ESCAPE = 0x1B;
    public const int VK_F4 = 0x73;
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;
    public const int VK_MENU = 0x12; // Alt
    public const int VK_LMENU = 0xA4;
    public const int VK_RMENU = 0xA5;
    public const int VK_CONTROL = 0x11;

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public nint dwExtraInfo;
    }

    public delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll")]
    public static extern nint GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);
}
