using System.Runtime.InteropServices;

namespace TinyBongo.Services;

/// <summary>
/// Win32 P/Invoke declarations used for global hooks and window styles.
/// </summary>
internal static class NativeMethods
{
    internal const int WhKeyboardLl = 13;
    internal const int WhMouseLl = 14;

    internal const int WmKeydown = 0x0100;
    internal const int WmSyskeydown = 0x0104;
    internal const int WmKeyup = 0x0101;
    internal const int WmSyskeyup = 0x0105;
    internal const int WmLbuttondown = 0x0201;
    internal const int WmRbuttondown = 0x0204;
    internal const int WmMbuttondown = 0x0207;
    internal const int WmLbuttonup = 0x0202;
    internal const int WmRbuttonup = 0x0205;
    internal const int WmMbuttonup = 0x0208;
    internal const int WmXbuttondown = 0x020B;
    internal const int WmXbuttonup = 0x020C;

    internal const int GwlExstyle = -20;
    internal const int WsExLayered = 0x00080000;
    internal const int WsExTransparent = 0x00000020;
    internal const int WsExToolwindow = 0x00000080;
    internal const int WsExAppwindow = 0x00040000;

    internal delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    internal struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MsLlHookStruct
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr DwExtraInfo;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
