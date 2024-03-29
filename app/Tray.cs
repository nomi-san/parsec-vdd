using System;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Threading;

namespace ParsecVDisplay
{
    internal static class Tray
    {
        static NotifyIcon Icon;
        static Window Window;
        static System.Windows.Controls.ContextMenu Menu;

        public static void Init(Window window, System.Windows.Controls.ContextMenu menu)
        {
            Window = window;
            Menu = menu;

            Icon = new NotifyIcon();

            var uri = new Uri(window.Icon.ToString());
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            Icon.Icon = new System.Drawing.Icon(streamInfo.Stream);

            Icon.MouseClick += Icon_MouseClick;
            Icon.DoubleClick += Icon_DoubleClick;

            Icon.Visible = true;
        }

        public static void Uninit()
        {
            if (Icon != null)
            {
                Icon.Visible = false;
                Icon.Dispose();
            }
        }

        private static void Icon_DoubleClick(object sender, EventArgs e)
        {
            ShowApp();
        }

        private static void Icon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                Menu.IsOpen = true;

                if (PresentationSource.FromVisual(Menu) is HwndSource hwndSource)
                {
                    SetForegroundWindow(hwndSource.Handle);
                }
            }
        }

        public static void ShowApp()
        {
            if (Window == null) return;

            if (Window.Dispatcher.Thread.ManagedThreadId != Thread.CurrentThread.ManagedThreadId)
            {
                Window.Dispatcher.BeginInvoke(new Action(ShowApp));
                return;
            }

            if (Window.Visibility == Visibility.Hidden)
            {
                Window.Show();
            }

            if (PresentationSource.FromVisual(Window) is HwndSource hwndSource)
            {
                SetForegroundWindow(hwndSource.Handle);
            }
        }

        [DllImport("user32.dll")]
        static extern IntPtr SetForegroundWindow(IntPtr hwnd);
    }
}