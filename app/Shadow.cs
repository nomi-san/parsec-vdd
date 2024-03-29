using System;
using System.Runtime.InteropServices;

namespace ParsecVDisplay
{
    internal static class Shadow
    {
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

        public static void ApplyShadow(IntPtr hwnd)
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
    }
}