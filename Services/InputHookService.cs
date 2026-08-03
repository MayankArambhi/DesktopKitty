using System.Runtime.InteropServices;
using TinyBongo.Models;

namespace TinyBongo.Services;

/// <summary>
/// Installs low-level global keyboard and mouse hooks (WH_KEYBOARD_LL / WH_MOUSE_LL).
/// Events fire only when input occurs — no polling loop.
/// </summary>
public sealed class InputHookService : IDisposable
{
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private NativeMethods.HookProc? _keyboardProc;
    private NativeMethods.HookProc? _mouseProc;
    private bool _disposed;

    /// <summary>Raised when a key is pressed globally. Argument is the virtual-key code.</summary>
    public event Action<int>? KeyDown;
    /// <summary>Raised when a key is released globally. Argument is the virtual-key code.</summary>
    public event Action<int>? KeyUp;

    /// <summary>Raised when any mouse button is pressed globally.</summary>
    public event Action? MouseDown;
    /// <summary>Raised when any mouse button is released globally.</summary>
    public event Action? MouseUp;
    /// <summary>Raised when a valid input (new key press or new mouse button press) is counted.
    /// Use this to increment global input counters without double-counting repeats.</summary>
    public event Action<InputSource>? InputCounted;

    // Track pressed keys & mouse buttons locally to detect transitions and avoid counting repeats.
    private readonly HashSet<int> _pressedKeys = new();
    private readonly HashSet<int> _pressedMouseButtons = new();

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Keep delegate references alive so the GC cannot collect the hook callbacks.
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;

        var moduleHandle = NativeMethods.GetModuleHandle(null);

        _keyboardHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhKeyboardLl, _keyboardProc, moduleHandle, 0);

        _mouseHook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WhMouseLl, _mouseProc, moduleHandle, 0);

        if (_keyboardHook == IntPtr.Zero || _mouseHook == IntPtr.Zero)
        {
            Dispose();
            throw new InvalidOperationException("Failed to install global input hooks.");
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var message = wParam.ToInt32();
                var hookStruct = Marshal.PtrToStructure<NativeMethods.KbdLlHookStruct>(lParam);
                var vk = (int)hookStruct.VkCode;
                if (message is NativeMethods.WmKeydown or NativeMethods.WmSyskeydown)
                {
                    KeyDown?.Invoke(vk);

                    // Count only when transitioning from released -> pressed (avoid repeats)
                    if (_pressedKeys.Add(vk))
                    {
                        InputCounted?.Invoke(InputSource.Keyboard);
                    }
                }
                else if (message is NativeMethods.WmKeyup or NativeMethods.WmSyskeyup)
                {
                    KeyUp?.Invoke(vk);
                    _pressedKeys.Remove(vk);
                }
            }
            catch
            {
                // Never throw from a hook proc — it would destabilize the hook chain.
            }
        }

        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var message = wParam.ToInt32();
                // Read mouse hook struct for extra button data when needed.
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MsLlHookStruct>(lParam);

                if (message is NativeMethods.WmLbuttondown)
                {
                    MouseDown?.Invoke();
                    if (_pressedMouseButtons.Add(1)) InputCounted?.Invoke(InputSource.Mouse);
                }
                else if (message is NativeMethods.WmLbuttonup)
                {
                    MouseUp?.Invoke();
                    _pressedMouseButtons.Remove(1);
                }
                else if (message is NativeMethods.WmRbuttondown)
                {
                    MouseDown?.Invoke();
                    if (_pressedMouseButtons.Add(2)) InputCounted?.Invoke(InputSource.Mouse);
                }
                else if (message is NativeMethods.WmRbuttonup)
                {
                    MouseUp?.Invoke();
                    _pressedMouseButtons.Remove(2);
                }
                else if (message is NativeMethods.WmMbuttondown)
                {
                    MouseDown?.Invoke();
                    if (_pressedMouseButtons.Add(3)) InputCounted?.Invoke(InputSource.Mouse);
                }
                else if (message is NativeMethods.WmMbuttonup)
                {
                    MouseUp?.Invoke();
                    _pressedMouseButtons.Remove(3);
                }
                else if (message is NativeMethods.WmXbuttondown)
                {
                    MouseDown?.Invoke();
                    // high word of mouseData indicates X button: 1 or 2
                    var xButton = (int)((hookStruct.MouseData >> 16) & 0xFFFF);
                    var id = xButton == 1 ? 4 : 5;
                    if (_pressedMouseButtons.Add(id)) InputCounted?.Invoke(InputSource.Mouse);
                }
                else if (message is NativeMethods.WmXbuttonup)
                {
                    MouseUp?.Invoke();
                    var xButton = (int)((hookStruct.MouseData >> 16) & 0xFFFF);
                    var id = xButton == 1 ? 4 : 5;
                    _pressedMouseButtons.Remove(id);
                }
            }
            catch
            {
                // Never throw from a hook proc.
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        _keyboardProc = null;
        _mouseProc = null;
    }
}
