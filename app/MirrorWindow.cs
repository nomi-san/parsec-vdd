using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ParsecVDisplay
{
    public class MirrorWindow : Form
    {
        const double FPS = 60.0;
        const double FRAME_TIME = 1000.0 / FPS;

        private bool IsMirroring;
        private Thread MirrorThread;
        private TaskCompletionSource<IntPtr> WhenHwnd;

        public MirrorWindow()
        {
            IsMirroring = false;
            WhenHwnd = new TaskCompletionSource<IntPtr>();

            ClientSize = new Size(960, 540);
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            IsMirroring = false;
            MirrorThread?.Join();

            base.OnClosing(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            WhenHwnd.SetResult(Handle);
        }

        public void MirrorScreen(string displayDevice)
        {
            if (!IsMirroring)
            {
                IsMirroring = true;
                Text = $"Mirror - {displayDevice}";

                MirrorThread = new Thread(() => MirrorWorker(displayDevice));
                MirrorThread.IsBackground = true;
                MirrorThread.Start();
            }
        }

        private void MirrorWorker(string displayDevice)
        {
            var hwnd = WhenHwnd.Task.Result;

            var dcDest = Native.GetDC(hwnd);
            var bgBrush = Native.GetStockObject(/*BLACK_BRUSH*/ 4);

            var devmode = default(Native.DEVMODE);
            short devmodeSize = (short)Marshal.SizeOf<Native.DEVMODE>();

            try
            {
                var stopwatch = Stopwatch.StartNew();
                double previousTime = stopwatch.Elapsed.TotalMilliseconds;

                while (IsMirroring)
                {
                    double currentTime = stopwatch.Elapsed.TotalMilliseconds;
                    double elapsedTime = currentTime - previousTime;

                    if (elapsedTime >= FRAME_TIME)
                    {
                        devmode.dmSize = devmodeSize;

                        if (Native.EnumDisplaySettings(displayDevice, -1, ref devmode))
                        {
                            var dcScreens = Native.GetDC(IntPtr.Zero);

                            var client = GetClientSize(hwnd);
                            var screen = new Rectangle(devmode.dmPositionX, devmode.dmPositionY, devmode.dmPelsWidth, devmode.dmPelsHeight);
                            var vp = GetViewport(client.Width, client.Height, screen.Width, screen.Height);

                            DrawBackground(dcDest, bgBrush, ref client, ref vp);
                            DrawScreen(dcDest, dcScreens, ref vp, ref screen);
                            DrawCursor(dcDest, ref vp, ref screen);

                            Native.ReleaseDC(IntPtr.Zero, dcScreens);
                        }

                        previousTime = currentTime;
                    }
                    else
                    {
                        int sleepTime = (int)(FRAME_TIME - elapsedTime);
                        if (sleepTime > 0)
                            Thread.Sleep(sleepTime);
                    }
                }
            }
            finally
            {
                Native.ReleaseDC(hwnd, dcDest);
                Native.DeleteObject(bgBrush);
            }
        }

        private struct Viewport
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
        }

        private void DrawBackground(IntPtr dc, IntPtr brush, ref Size client, ref Viewport vp)
        {
            var rect = default(Rectangle);

            // fill the excluded rectangles (areas outside the viewport)
            // this is the simplest way to avoid flickering without WM_PAINT

            // top excluded rect
            if (vp.Y > 0)
            {
                rect.X = 0;
                rect.Y = 0;
                rect.Width = client.Width;
                rect.Height = vp.Y;

                Native.FillRect(dc, ref rect, brush);
            }

            // bottom excluded rect
            if (vp.Y + vp.Height < client.Height)
            {
                rect.X = 0;
                rect.Y = vp.Y + vp.Height;
                rect.Width = client.Width;
                rect.Height = client.Height;

                Native.FillRect(dc, ref rect, brush);
            }

            // left excluded rect
            if (vp.X > 0)
            {
                rect.X = 0;
                rect.Y = vp.Y;
                rect.Width = vp.X;
                rect.Height = vp.Height + vp.Y;

                Native.FillRect(dc, ref rect, brush);
            }

            // right excluded rect
            if (vp.X + vp.Width < client.Width)
            {
                rect.X = vp.X + vp.Width;
                rect.Y = vp.Y;
                rect.Width = client.Width;
                rect.Height = vp.Height + vp.Y;

                Native.FillRect(dc, ref rect, brush);
            }
        }

        private void DrawScreen(IntPtr dc, IntPtr dcSrc, ref Viewport vp, ref Rectangle screen)
        {
            // set scaling mode
            Native.SetStretchBltMode(dc, /*HALFTONE*/ 4);

            // draw the screen
            Native.StretchBlt(
                dc,
                vp.X, vp.Y, vp.Width, vp.Height,
                dcSrc,
                screen.X, screen.Y, screen.Width, screen.Height,
                Native.SRCCOPY
            );
        }

        private void DrawCursor(IntPtr dc, ref Viewport vp, ref Rectangle screen)
        {
            var cursor = default(Native.CURSORINFO);
            cursor.cbSize = Marshal.SizeOf<Native.CURSORINFO>();

            if (Native.GetCursorInfo(ref cursor)
                // cursor must be inside the screen
                && screen.Contains(cursor.screenPosX, cursor.screenPosY)
                // and visible
                && cursor.flags == /*CURSOR_SHOWING*/ 0x1)
            {
                var iconInfo = default(Native.ICONINFO);
                Native.GetIconInfo(cursor.hCursor, ref iconInfo);

                var bmpCursor = default(Native.BITMAP);
                Native.GetObject(iconInfo.hbmColor, Marshal.SizeOf<Native.BITMAP>(), ref bmpCursor);

                int x = cursor.screenPosX - iconInfo.xHotspot - screen.X;
                int y = cursor.screenPosY - iconInfo.yHotspot - screen.Y;
                int width = bmpCursor.bmWidth;
                int height = bmpCursor.bmHeight;
                ScaleCursor(ref vp, screen.Size, ref x, ref y, ref width, ref height);

                Native.DrawIconEx(dc, x, y, cursor.hCursor, width, height, 0, IntPtr.Zero, /*DI_NORMAL*/ 0x3);
            }
        }

        private static Size GetClientSize(IntPtr hwnd)
        {
            var rect = new Rectangle();
            Native.GetClientRect(hwnd, ref rect);
            return new Size(rect.Width, rect.Height);
        }

        private static Viewport GetViewport(int clientWidth, int clientHeight, int rectWidth, int rectHeight)
        {
            float clientAspect = (float)clientWidth / clientHeight;
            float rectAspect = (float)rectWidth / rectHeight;

            int viewportX, viewportY;
            int viewportWidth, viewportHeight;

            // compare aspect ratios to determine scaling
            if (clientAspect > rectAspect)
            {
                // client is wider than rect, so scale to fit height
                viewportHeight = clientHeight;
                viewportWidth = (int)(rectAspect * viewportHeight);
            }
            else
            {
                // client is taller than rect, so scale to fit width
                viewportWidth = clientWidth;
                viewportHeight = (int)(viewportWidth / rectAspect);
            }

            // center the viewport
            viewportX = (clientWidth - viewportWidth) / 2;
            viewportY = (clientHeight - viewportHeight) / 2;

            return new Viewport
            {
                X = viewportX,
                Y = viewportY,
                Width = viewportWidth,
                Height = viewportHeight,
            };
        }

        private static void ScaleCursor(ref Viewport viewport, Size screen, ref int cursorX, ref int cursorY, ref int cursorWidth, ref int cursorHeight)
        {
            float scaleX = (float)viewport.Width / screen.Width;
            float scaleY = (float)viewport.Height / screen.Height;

            cursorWidth = (int)(cursorWidth * scaleX);
            cursorHeight = (int)(cursorHeight * scaleY);

            cursorX = viewport.X + (int)(cursorX * scaleX);
            cursorY = viewport.Y + (int)(cursorY * scaleY);
        }

        private static class Native
        {
            public const int ENUM_CURRENT_SETTINGS = -1;
            public const int SRCCOPY = 0x00CC0020;

            [StructLayout(LayoutKind.Sequential)]
            public struct DEVMODE
            {
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
                public string dmDeviceName;
                public short dmSpecVersion;
                public short dmDriverVersion;
                public short dmSize;
                public short dmDriverExtra;
                public int dmFields;
                public int dmPositionX;
                public int dmPositionY;
                public int dmDisplayOrientation;
                public int dmDisplayFixedOutput;
                public short dmColor;
                public short dmDuplex;
                public short dmYResolution;
                public short dmTTOption;
                public short dmCollate;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
                public string dmFormName;
                public short dmLogPixels;
                public int dmBitsPerPel;
                public int dmPelsWidth;
                public int dmPelsHeight;
                public int dmDisplayFlags;
                public int dmDisplayFrequency;
                public int dmICMMethod;
                public int dmICMIntent;
                public int dmMediaType;
                public int dmDitherType;
                public int dmReserved1;
                public int dmReserved2;
                public int dmPanningWidth;
                public int dmPanningHeight;
            }

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetClientRect(IntPtr hwnd, ref Rectangle rect);

            [DllImport("user32.dll")]
            public static extern IntPtr GetDC(IntPtr hwnd);

            [DllImport("user32.dll")]
            public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

            [DllImport("gdi32.dll", SetLastError = true)]
            public static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

            [DllImport("gdi32.dll", SetLastError = true)]
            public static extern bool DeleteDC(IntPtr hdc);

            [DllImport("gdi32.dll", SetLastError = true)]
            public static extern bool DeleteObject(IntPtr hObject);

            [DllImport("gdi32.dll")]
            public static extern int SetStretchBltMode(IntPtr hdc, int mode);

            [DllImport("gdi32.dll")]
            public static extern bool StretchBlt(IntPtr hdcDest, int nXOriginDest, int nYOriginDest, int nWidthDest, int nHeightDest,
                IntPtr hdcSrc, int nXOriginSrc, int nYOriginSrc, int nWidthSrc, int nHeightSrc, uint dwRop);

            [StructLayout(LayoutKind.Sequential)]
            public struct BITMAP
            {
                public uint bmType;
                public int bmWidth;
                public int bmHeight;
                public int bmWidthBytes;
                public short bmPlanes;
                public short bmBitsPixel;
                public IntPtr bmBits;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct ICONINFO
            {
                public int fIcon;
                public int xHotspot;
                public int yHotspot;
                public IntPtr hbmMask;
                public IntPtr hbmColor;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct CURSORINFO
            {
                public int cbSize;
                public uint flags;
                public IntPtr hCursor;
                public int screenPosX;
                public int screenPosY;
            }

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetIconInfo(IntPtr hIcon, ref ICONINFO piconinfo);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetCursorInfo(ref CURSORINFO pci);

            [DllImport("gdi32.dll")]
            public static extern int GetObject(IntPtr h, int c, ref BITMAP pv);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DrawIconEx(IntPtr hdc,
                int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth,
                uint istepIfAniCur, IntPtr hbrFlickerFreeDraw, uint diFlags);

            [DllImport("user32.dll")]
            public static extern int FillRect(IntPtr hDC, ref Rectangle lprc, IntPtr hbr);

            [DllImport("gdi32.dll")]
            public static extern IntPtr GetStockObject(int i);
        }
    }
}