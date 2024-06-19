using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ParsecVDisplay
{
    internal class Display
    {
        public enum Orientation
        {
            Landscape = 0,      // Angle0
            Portrait,           // Angle90
            Landscape_Flipped,  // Angle180
            Portrait_Flipped    // Angle270
        }

        public class Mode
        {
            public int Width;
            public int Height;
            public int Hz;

            public Mode()
            {
            }

            public Mode(int width, int height, int hz)
            {
                Width = width;
                Height = height;
                Hz = hz;
            }

            public Mode(ulong bits)
            {
                Width = unchecked((ushort)bits);
                Height = unchecked((ushort)(bits >> 16));
                Hz = unchecked((ushort)(bits >> 32));
            }

            public ulong Bits => (uint)(Width & 0xFFFF)
                | ((ulong)(Height & 0xFFFF) << 16)
                | ((ulong)(Hz & 0xFFFF) << 32);

            public string Resolution => $"{Width} × {Height}";
            public string RefreshRate => $"{Hz} Hz";
            public override string ToString() => $"{Resolution} @ {RefreshRate}";
        }

        public class ModeSet
        {
            public int Width;
            public int Height;
            public List<int> RefreshRates;
        }

        public bool Active;
        public int Identifier;
        public int CloneOf;
        public int Address;
        public DateTime LastArrival;

        public string Adapter;
        public string AdapterInstance;
        public DateTime AdapterArrival;

        public string DeviceName;
        public string DisplayName;

        public Mode CurrentMode;
        public Orientation CurrentOrientation;
        public List<Mode> ModeList;
        public List<ModeSet> SupportedResolutions;

        public int DisplayIndex => Address - 0x100;

        Display()
        {
            ModeList = new List<Mode>();
            CurrentOrientation = Orientation.Landscape;
            SupportedResolutions = new List<ModeSet>();
        }

        public override string ToString()
        {
            var str = $"[{Identifier}] {DeviceName} ({DisplayName}#{Address})";
            if (CloneOf > 0 && CloneOf < Identifier) str += $" (clone of [{CloneOf}])";
            return str;
        }

        void FetchAllModes()
        {
            var devMode = new Native.DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(Native.DEVMODE));

            var set = new Dictionary<ulong, HashSet<int>>();

            for (int num = -1; Native.EnumDisplaySettings(DeviceName, num, ref devMode); num++)
            {
                var mode = new Mode
                {
                    Width = devMode.dmPelsWidth,
                    Height = devMode.dmPelsHeight,
                    Hz = devMode.dmDisplayFrequency,
                };

                if (num == -1)
                {
                    CurrentMode = mode;
                    CurrentOrientation = devMode.dmDisplayOrientation;
                }
                else
                {
                    ModeList.Add(mode);

                    mode.Hz = 0;
                    if (set.TryGetValue(mode.Bits, out var rrs))
                    {
                        rrs.Add(devMode.dmDisplayFrequency);
                    }
                    else
                    {
                        set[mode.Bits] = new HashSet<int> { devMode.dmDisplayFrequency };
                    }
                }
            }

            foreach (var kv in set)
            {
                var mode = new Mode(kv.Key);
                var rrs = kv.Value.ToList();
                rrs.Sort((a, b) => a - b);

                SupportedResolutions.Add(new ModeSet
                {
                    Width = mode.Width,
                    Height = mode.Height,
                    RefreshRates = rrs,
                });
            }

            SupportedResolutions.Sort((a, b)
                => b.Width == a.Width ? (b.Height - a.Height) : (b.Width - a.Width));
        }

        public bool ChangeMode(int? width, int? height, int? hz, Orientation? orientation)
        {
            var mode = new Native.DEVMODE();
            mode.dmSize = (short)Marshal.SizeOf(typeof(Native.DEVMODE));
            
            if (Native.EnumDisplaySettings(DeviceName, -1, ref mode))
            {
                if (width.HasValue) mode.dmPelsWidth = width.Value;
                if (height.HasValue) mode.dmPelsHeight = height.Value;
                if (hz.HasValue) mode.dmDisplayFrequency = hz.Value;

                if (orientation.HasValue)
                {
                    var newDO = orientation.Value;
                    mode.dmDisplayOrientation = newDO;

                    if (((int)newDO + (int)CurrentOrientation) % 2 != 0)
                    {
                        int t = mode.dmPelsWidth;
                        mode.dmPelsWidth = mode.dmPelsHeight;
                        mode.dmPelsHeight = t;
                    }
                }

                return Native.ChangeDisplaySettingsEx(DeviceName,
                    ref mode, IntPtr.Zero, 1, IntPtr.Zero) == 0;
            }

            return false;
        }

        public void TakeScreenshot(string saveFile)
        {
            int width = CurrentMode.Width;
            int height = CurrentMode.Height;

            using (var bmp = new Bitmap(width, height))
            using (var gfx = Graphics.FromImage(bmp))
            {
                var hdc = Native.CreateDC(IntPtr.Zero, DeviceName, IntPtr.Zero, IntPtr.Zero);

                var dstHdc = gfx.GetHdc();
                Native.BitBlt(dstHdc, 0, 0, width, height, hdc, 0, 0, 0x00CC0020);
                gfx.ReleaseHdc(dstHdc);

                bmp.Save(saveFile, System.Drawing.Imaging.ImageFormat.Png);
                Native.DeleteDC(hdc);
            }
        }

        public static string DumpModes(List<Mode> modes)
        {
            return string.Join(",", modes.Select(m => m.Bits.ToString("x")));
        }

        public static List<Mode> ParseModes(string modes)
        {
            var list = new List<Mode>();
            var tokens = modes.Trim().Split(',');
            foreach (var mode in tokens)
                if (ulong.TryParse(mode, NumberStyles.HexNumber, null, out var bits))
                    list.Add(new Mode(bits));

            return list;
        }

        public static List<Display> GetAllDisplays()
        {
            var displayMap = new Dictionary<string, Display>(StringComparer.OrdinalIgnoreCase);
            var cloneGroups = new List<Tuple<Display, Display>>();

            var paths = GetDisplayPaths();

            var dd = new Native.DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));

            for (int i = 0; Native.EnumDisplayDevices(null, i, ref dd, 0); i++)
            {
                Display prevActiveDisplay = null;

                var dd2 = new Native.DISPLAY_DEVICE();
                dd2.cb = Marshal.SizeOf(typeof(Native.DISPLAY_DEVICE));

                for (int j = 0; Native.EnumDisplayDevices(dd.DeviceName, j, ref dd2, Native.EDD_GET_DEVICE_INTERFACE_NAME); j++)
                {
                    if ((dd2.StateFlags & Native.DISPLAY_DEVICE_ATTACHED) == 0)
                        continue;

                    var pathIdx = paths.FindIndex(p => dd2.DeviceID.Contains(p.Replace('\\', '#')));
                    if (pathIdx < 0) continue;

                    if (!displayMap.ContainsKey(paths[pathIdx]))
                    {
                        var display = new Display
                        {
                            Active = (dd2.StateFlags & Native.DISPLAY_DEVICE_ACTIVE) != 0,
                            Address = ParseDisplayAddress(paths[pathIdx]),
                            DeviceName = dd.DeviceName,
                            DisplayName = ParseDisplayCode(dd2.DeviceID),
                        };

                        if (display.Active)
                        {
                            if (prevActiveDisplay == null)
                            {
                                prevActiveDisplay = display;
                            }
                            else
                            {
                                cloneGroups.Add(new Tuple<Display, Display>(prevActiveDisplay, display));
                            }

                            display.FetchAllModes();
                        }

                        Device.GetDeviceInstance(paths[pathIdx], out uint devInst);
                        display.LastArrival = Device.GetDeviceLastArrival(devInst);

                        Device.GetParentDeviceInstance(devInst, out uint parentInst, out display.AdapterInstance);
                        display.Adapter = Device.GetDeviceDescription(parentInst);
                        display.AdapterArrival = Device.GetDeviceLastArrival(parentInst);

                        displayMap.Add(paths[pathIdx], display);
                        paths.RemoveAt(pathIdx);
                    }
                }
            }

            var displays = displayMap.Values.ToList();

            // Sort displays by adapter arrival time
            displays.Sort((a, b) =>
            {
                if (a.AdapterInstance == b.AdapterInstance)
                    return a.DeviceName.CompareTo(b.DeviceName);
                return a.AdapterArrival.CompareTo(b.AdapterArrival);
            });

            // Fill display identifier
            for (int i = 0; i < displays.Count; i++)
                displays[i].Identifier = i + 1;

            // Fill clone of identifier
            foreach (var group in cloneGroups)
            {
                group.Item1.CloneOf = group.Item2.Identifier;
                group.Item2.CloneOf = group.Item1.Identifier;
            }

            cloneGroups.Clear();
            return displays;
        }

        static List<string> GetDisplayPaths()
        {
            var paths = new List<string>();

            using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\monitor\Enum", false))
            {
                if (key != null)
                {
                    int count = Convert.ToInt32(key.GetValue("Count", 0));

                    for (int i = 0; i < count; ++i)
                    {
                        var path = key.GetValue($"{i}");
                        paths.Add(Convert.ToString(path));
                    }
                }
            }

            return paths;
        }

        static int ParseDisplayAddress(string path)
        {
            var index = path.LastIndexOf("uid", StringComparison.OrdinalIgnoreCase);
            int.TryParse(path.Substring(index + 3), out var address);
            return address;
        }

        static string ParseDisplayCode(string id)
        {
            var tokens = id.Split('#');
            return tokens.Length >= 2 ? tokens[1] : tokens[0];
        }

        static class Native
        {
            public const uint EDD_GET_DEVICE_INTERFACE_NAME = 0x1;
            public const uint DISPLAY_DEVICE_ACTIVE = 0x1;
            public const uint DISPLAY_DEVICE_ATTACHED = 0x2;

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool EnumDisplayDevices(string lpDevice, int iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
            public struct DISPLAY_DEVICE
            {
                public int cb;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
                public string DeviceName;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
                public string DeviceString;
                public uint StateFlags;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
                public string DeviceID;
                [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
                public string DeviceKey;
            }

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

            [DllImport("user32.dll")]
            public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode,
                IntPtr hwnd, uint dwflags, IntPtr lParam);

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
                public Orientation dmDisplayOrientation;
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

            [DllImport("gdi32.dll")]
            public static extern IntPtr CreateDC(IntPtr pwszDriver, string pwszDevice, IntPtr pszPort, IntPtr devmode);

            [DllImport("gdi32.dll")]
            public static extern int BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int rasterOperation);

            [DllImport("gdi32.dll")]
            public static extern int DeleteDC(IntPtr hdc);
        }
    }
}