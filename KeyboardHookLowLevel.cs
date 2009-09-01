using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Reflection;
using System.ComponentModel;
using System.Windows.Forms;

namespace KeyboardRedirector
{
    class KeyboardHookLowLevel
    {
        private int _keyboardHookHandle;
        private Keys _modifiers;

        public event KeyEventHandler KeyDown;
        public event KeyEventHandler KeyUp;

        private delegate int HookProc(int nCode, int wParam, IntPtr lParam);

        /// <summary>
        /// This field isn't really needed except to maintain a reference to the hook so
        /// that the GC doesn't clean it up while the unmanaged code is still using it.
        /// </summary>
        private HookProc _keyboardDelegate;


        #region Win32 API

        private const int WH_KEYBOARD_LL = 13;

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true)]
        private static extern int UnhookWindowsHookEx(int idHook);

        [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
        private static extern int CallNextHookEx(int idHook, int nCode, int wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        #endregion

        public void SetHook()
        {
            if (_keyboardHookHandle == 0)
            {
                _modifiers = Keys.None;
                _keyboardDelegate = KeyboardHookProc;
                _keyboardHookHandle = KeyboardHookLowLevel.SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardDelegate, Marshal.GetHINSTANCE(Assembly.GetExecutingAssembly().GetModules()[0]), 0);

                if (_keyboardHookHandle == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        public void ClearHook()
        {
            if (_keyboardHookHandle != 0)
            {
                int result = KeyboardHookLowLevel.UnhookWindowsHookEx(_keyboardHookHandle);
                _keyboardHookHandle = 0;
                _keyboardDelegate = null;
                if (result == 0)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        private int KeyboardHookProc(int nCode, Int32 wParam, IntPtr lParam)
        {
            bool handled = false;

            if (nCode >= 0)
            {
                KBDLLHOOKSTRUCT hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                Keys key = (Keys)hookStruct.vkCode;

                Keys modifier = Keys.None;
                if ((key == Keys.ShiftKey) || (key == Keys.LShiftKey) || (key == Keys.RShiftKey))
                    modifier = Keys.Shift;
                else if ((key == Keys.ControlKey) || (key == Keys.LControlKey) || (key == Keys.RControlKey))
                    modifier = Keys.Control;
                if ((key == Keys.Menu) || (key == Keys.LMenu) || (key == Keys.RMenu))
                    modifier = Keys.Alt;

                if ((wParam == (int)Win32.WM.KEYDOWN) || (wParam == (int)Win32.WM.SYSKEYDOWN))
                {
                    _modifiers |= modifier;

                    // Only trigger the event if this isn't a modifier
                    if ((modifier == Keys.None) && (KeyDown != null))
                    {
                        key |= _modifiers;
                        KeyEventArgs e = new KeyEventArgs(key);
                        KeyDown(this, e);
                        handled |= e.Handled;
                    }
                }

                if ((wParam == (int)Win32.WM.KEYUP) || (wParam == (int)Win32.WM.SYSKEYUP))
                {
                    _modifiers &= ~modifier;

                    // Only trigger the event if this isn't a modifier
                    if ((modifier == Keys.None) && (KeyUp != null))
                    {
                        key |= _modifiers;
                        KeyEventArgs e = new KeyEventArgs(key);
                        KeyUp(this, e);
                        handled |= e.Handled;
                    }
                }
            }

            if (handled)
                return -1;

            return CallNextHookEx(_keyboardHookHandle, nCode, wParam, lParam);
        }

    }
}
