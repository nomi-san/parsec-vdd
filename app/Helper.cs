using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Interop;

namespace ParsecVDisplay
{
    internal static class Helper
    {
        public static bool ShellExec(string file, string args = "", string cwd = null, bool admin = false)
        {
            try
            {
                var a = Assembly.GetAssembly(typeof(Process));
                var _p = a.GetType("System.Diagnostics.Process");
                var _psi = a.GetType("System.Diagnostics.ProcessStartInfo");

                var psi = Activator.CreateInstance(_psi);
                _psi.GetProperty("FileName").SetValue(psi, file);
                _psi.GetProperty("UseShellExecute").SetValue(psi, true);

                if (!string.IsNullOrEmpty(args))
                    _psi.GetProperty("Arguments").SetValue(psi, args);
                if (!string.IsNullOrEmpty(cwd))
                    _psi.GetProperty("WorkingDirectory").SetValue(psi, cwd);
                if (admin)
                    _psi.GetProperty("Verb").SetValue(psi, "runas");

                var s = _p.GetMethod("Start", new Type[] { _psi });
                s.Invoke(null, new object[] { psi });

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void OpenLink(string url)
        {
            if (!string.IsNullOrEmpty(url) && url.StartsWith("https://"))
                ShellExec(url);
        }

        public static bool IsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public static bool RunAdminTask(string args)
        {
            var exe = Assembly.GetExecutingAssembly().Location;
            var cwd = Path.GetDirectoryName(exe);

            return ShellExec(exe, args, cwd, true);
        }

        public static void StayAwake(bool enable)
        {
            const uint ES_CONTINUOUS = 0x80000000;
            const uint ES_DISPLAY_REQUIRED = 0x00000002;

            uint flags = ES_CONTINUOUS;
            if (enable) flags |= ES_DISPLAY_REQUIRED;

            SetThreadExecutionState(flags);
        }

        [DllImport("kernel32.dll")]
        static extern uint SetThreadExecutionState(uint esFlags);

        public static void EnableDropShadow(IntPtr hwnd)
        {
            var v = 2;
            DwmSetWindowAttribute(hwnd, 2, ref v, 4);

            var margins = new MARGINS
            {
                bottomHeight = 0,
                leftWidth = 0,
                rightWidth = 0,
                topHeight = 1
            };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [StructLayout(LayoutKind.Sequential)]
        struct MARGINS
        {
            public int leftWidth;
            public int rightWidth;
            public int topHeight;
            public int bottomHeight;
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        public static void ShowMe(this Window window)
        {
            if (window.Visibility != Visibility.Visible)
            {
                window.Show();
            }

            if (PresentationSource.FromVisual(window) is HwndSource hwndSource)
            {
                ShowWindow(hwndSource.Handle, 5);
                SetForegroundWindow(hwndSource.Handle);
            }
        }

        [DllImport("user32.dll")]
        static extern IntPtr SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern int ShowWindow(IntPtr hwnd, int flag);

        public class ArbitraryWindow : System.Windows.Forms.IWin32Window
        {
            public ArbitraryWindow(IntPtr handle) { Handle = handle; }
            public IntPtr Handle { get; private set; }
        }
    }
}