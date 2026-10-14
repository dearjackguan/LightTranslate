using System;
using System.Runtime.InteropServices;

namespace LightTranslate
{
    internal static class NativeMethods
    {
        internal const int HotkeyId = 0x4C54;
        internal const int HotkeyMessage = 0x0312;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnregisterHotKey(IntPtr window, int id);
    }
}
