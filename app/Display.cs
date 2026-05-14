using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;

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

        /// <summary>
        /// Serializable snapshot of a display's mode + orientation. Used for
        /// restoring displays across suspend/resume and across app sessions.
        /// </summary>
        public class State
        {
            public int Width;
            public int Height;
            public int Hz;
            public Orientation Orientation;

            public string Pack() => $"{Width}x{Height}@{Hz}/{(int)Orientation}";

            public static bool TryUnpack(string s, out State state)
            {
                state = null;
                if (string.IsNullOrWhiteSpace(s))
                    return false;

                var m = System.Text.RegularExpressions.Regex.Match(s.Trim(),
                    @"^(\d+)x(\d+)@(\d+)/([0-3])$");
                if (!m.Success)
                    return false;

                state = new State
                {
                    Width       = int.Parse(m.Groups[1].Value),
                    Height      = int.Parse(m.Groups[2].Value),
                    Hz          = int.Parse(m.Groups[3].Value),
                    Orientation = (Orientation)int.Parse(m.Groups[4].Value),
                };
                return true;
            }
        }

        public State Snapshot()
        {
            return new State
            {
                Width       = CurrentMode?.Width  ?? 0,
                Height      = CurrentMode?.Height ?? 0,
                Hz          = CurrentMode?.Hz     ?? 0,
                Orientation = CurrentOrientation,
            };
        }

        public static string PackStates(List<State> states)
        {
            return string.Join(",", states.ConvertAll(s => s.Pack()));
        }

        public static List<State> UnpackStates(string packed)
        {
            var list = new List<State>();
            if (string.IsNullOrWhiteSpace(packed))
                return list;

            foreach (var tok in packed.Split(','))
            {
                if (State.TryUnpack(tok, out var s))
                    list.Add(s);
            }
            return list;
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
            return ChangeMode(width, height, hz, orientation, defer: false);
        }

        /// <summary>
        /// Apply a mode change. When <paramref name="defer"/> is true the change is
        /// staged with CDS_NORESET — the caller must invoke <see cref="CommitChanges"/>
        /// to apply all staged changes atomically. Returns true on DISP_CHANGE_SUCCESSFUL.
        /// </summary>
        public bool ChangeMode(int? width, int? height, int? hz, Orientation? orientation, bool defer)
        {
            var mode = new Native.DEVMODE();
            mode.dmSize = (short)Marshal.SizeOf(typeof(Native.DEVMODE));

            if (!Native.EnumDisplaySettings(DeviceName, -1, ref mode))
                return false;

            // dmFields must explicitly enumerate the fields we are changing
            mode.dmFields = 0;

            if (width.HasValue)
            {
                mode.dmPelsWidth = width.Value;
                mode.dmFields |= /*DM_PELSWIDTH*/ 0x80000;
            }

            if (height.HasValue)
            {
                mode.dmPelsHeight = height.Value;
                mode.dmFields |= /*DM_PELSHEIGHT*/ 0x100000;
            }

            if (hz.HasValue)
            {
                mode.dmDisplayFrequency = hz.Value;
                mode.dmFields |= /*DM_DISPLAYFREQUENCY*/ 0x400000;
            }

            if (orientation.HasValue)
            {
                var newDO = orientation.Value;
                mode.dmDisplayOrientation = newDO;
                mode.dmFields |= /*DM_DISPLAYORIENTATION*/ 0x80;

                if (((int)newDO + (int)CurrentOrientation) % 2 != 0)
                {
                    int t = mode.dmPelsWidth;
                    mode.dmPelsWidth = mode.dmPelsHeight;
                    mode.dmPelsHeight = t;
                    mode.dmFields |= 0x80000 | 0x100000;
                }
            }

            uint flags = /*CDS_UPDATEREGISTRY*/ 0x1;
            if (defer)
                flags |= /*CDS_NORESET*/ 0x10000000;

            int rc = Native.ChangeDisplaySettingsEx(DeviceName, ref mode, IntPtr.Zero, flags, IntPtr.Zero);
            if (rc != 0 /* DISP_CHANGE_SUCCESSFUL */)
                return false;

            // Refresh local cache so subsequent ChangeMode calls see the new state
            if (width.HasValue)  CurrentMode.Width  = width.Value;
            if (height.HasValue) CurrentMode.Height = height.Value;
            if (hz.HasValue)     CurrentMode.Hz     = hz.Value;
            if (orientation.HasValue) CurrentOrientation = orientation.Value;

            return true;
        }

        /// <summary>
        /// Apply all pending CDS_NORESET changes atomically.
        /// Returns true if the commit was accepted (DISP_CHANGE_SUCCESSFUL).
        /// </summary>
        public static bool CommitChanges()
        {
            return Native.ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero) == 0;
        }

        public void TakeScreenshot(string saveFile)
        {
            var devMode = new Native.DEVMODE();
            devMode.dmSize = (short)Marshal.SizeOf(typeof(Native.DEVMODE));
            Native.EnumDisplaySettings(DeviceName, -1, ref devMode);

            int x = devMode.dmPositionX;
            int y = devMode.dmPositionY;
            int width = devMode.dmPelsWidth;
            int height = devMode.dmPelsHeight;

            using (var bmp = new Bitmap(width, height))
            using (var gfx = Graphics.FromImage(bmp))
            {
                var hdcSrc = Native.GetDC(IntPtr.Zero);
                var hdcDest = gfx.GetHdc();

                Native.BitBlt(hdcDest, 0, 0, width, height, hdcSrc, x, y, 0x00CC0020);
                gfx.ReleaseHdc(hdcDest);

                bmp.Save(saveFile, System.Drawing.Imaging.ImageFormat.Png);
                Native.ReleaseDC(IntPtr.Zero, hdcSrc);
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

                    // Derive device instance ID directly from monitor.DeviceID
                    // (interface path) — avoids reading HKLM\...\monitor\Enum.
                    if (!TryParseInstanceId(dd2.DeviceID, out var instanceId))
                        continue;

                    if (displayMap.ContainsKey(instanceId))
                        continue;

                    var display = new Display
                    {
                        Active = (dd2.StateFlags & Native.DISPLAY_DEVICE_ACTIVE) != 0,
                        Address = ParseDisplayAddress(dd2.DeviceID),
                        DeviceName = dd.DeviceName,
                        DisplayName = ParseDisplayCode(dd2.DeviceID),
                    };

                    if (display.Active)
                    {
                        if (prevActiveDisplay == null)
                            prevActiveDisplay = display;
                        else
                            cloneGroups.Add(new Tuple<Display, Display>(prevActiveDisplay, display));

                        display.FetchAllModes();
                    }

                    if (Device.GetDeviceInstance(instanceId, out uint devInst))
                    {
                        Device.GetParentDeviceInstance(devInst, out uint parentInst, out display.AdapterInstance);
                        display.AdapterArrival = Device.GetDeviceLastArrival(parentInst);
                    }

                    displayMap.Add(instanceId, display);
                }
            }

            var displays = displayMap.Values.ToList();

            // Sort displays by adapter arrival (older adapter → lower number),
            // then by monitor Address (UID) within the same adapter — DeviceName
            // would sort lexicographically (DISPLAY10 < DISPLAY2), which breaks
            // numbering once the GDI ordinal crosses 9.
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

        /// <summary>
        /// Convert a device interface path returned by EnumDisplayDevices with
        /// EDD_GET_DEVICE_INTERFACE_NAME to a device instance ID accepted by
        /// CM_Locate_DevNodeA. Example transform:
        /// <code>
        /// \\?\DISPLAY#PSCCDD0#5&amp;abc&amp;UID256#{e6f07b5f-...}
        /// → DISPLAY\PSCCDD0\5&amp;abc&amp;UID256
        /// </code>
        /// </summary>
        static bool TryParseInstanceId(string interfacePath, out string instanceId)
        {
            instanceId = null;
            if (string.IsNullOrEmpty(interfacePath))
                return false;

            int start = interfacePath.StartsWith(@"\\?\") ? 4 : 0;
            int end = interfacePath.IndexOf("#{", start, StringComparison.Ordinal);
            if (end < 0) end = interfacePath.Length;
            if (end <= start) return false;

            instanceId = interfacePath.Substring(start, end - start).Replace('#', '\\');
            return true;
        }

        /// <summary>
        /// Parse the UID number out of a device interface path / device ID.
        /// Walks contiguous digits after "UID" rather than relying on the
        /// substring being numeric-to-end (handles trailing "#{guid}").
        /// </summary>
        static int ParseDisplayAddress(string path)
        {
            if (string.IsNullOrEmpty(path))
                return 0;

            int i = path.IndexOf("UID", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return 0;
            i += 3;

            int end = i;
            while (end < path.Length && path[end] >= '0' && path[end] <= '9')
                end++;

            int address;
            int.TryParse(path.Substring(i, end - i), out address);
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

            [DllImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExA", CharSet = CharSet.Ansi)]
            public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, IntPtr lpDevMode,
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

            [DllImport("user32.dll")]
            public static extern IntPtr GetDC(IntPtr hwnd);

            [DllImport("user32.dll")]
            public static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        }
    }
}