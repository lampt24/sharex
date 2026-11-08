#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using ShareX.HelpersLib;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ShareX.ScreenCaptureLib
{
    // Low level keyboard hook used by scrolling capture to observe keys while another window has focus.
    internal sealed class KeyboardHook : IDisposable
    {
        public event KeyEventHandler KeyDown, KeyUp;

        private readonly HookProc keyboardHookProc;
        private IntPtr keyboardHookHandle = IntPtr.Zero;

        public KeyboardHook()
        {
            keyboardHookProc = KeyboardHookProc;
            keyboardHookHandle = SetHook(NativeConstants.WH_KEYBOARD_LL, keyboardHookProc);
        }

        ~KeyboardHook()
        {
            Unhook();
        }

        private static IntPtr SetHook(int hookType, HookProc hookProc)
        {
            using (Process currentProcess = Process.GetCurrentProcess())
            using (ProcessModule currentModule = currentProcess.MainModule)
            {
                IntPtr moduleHandle = NativeMethods.GetModuleHandle(currentModule.ModuleName);
                return NativeMethods.SetWindowsHookEx(hookType, hookProc, moduleHandle, 0);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                bool handled = false;

                switch ((KeyEvent)wParam)
                {
                    case KeyEvent.WM_KEYDOWN:
                    case KeyEvent.WM_SYSKEYDOWN:
                        handled = Raise(KeyDown, lParam);
                        break;
                    case KeyEvent.WM_KEYUP:
                    case KeyEvent.WM_SYSKEYUP:
                        handled = Raise(KeyUp, lParam);
                        break;
                }

                if (handled)
                {
                    return (IntPtr)1;
                }
            }

            return NativeMethods.CallNextHookEx(keyboardHookHandle, nCode, wParam, lParam);
        }

        private bool Raise(KeyEventHandler handler, IntPtr key)
        {
            if (handler == null)
            {
                return false;
            }

            Keys keyData = (Keys)Marshal.ReadInt32(key) | Control.ModifierKeys;
            KeyEventArgs keyEventArgs = new(keyData);
            handler(this, keyEventArgs);
            return keyEventArgs.Handled || keyEventArgs.SuppressKeyPress;
        }

        private void Unhook()
        {
            if (keyboardHookHandle != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(keyboardHookHandle);
                keyboardHookHandle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            Unhook();
            GC.SuppressFinalize(this);
        }
    }
}
