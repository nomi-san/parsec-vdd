using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

using Timer = System.Windows.Forms.Timer;

namespace ParsecVDisplay
{
    internal static class ParsecVDD
    {
        public const string DISPLAY_ID = "PSCCDD0";
        public const string DISPLAY_NAME = "ParsecVDA";

        public const string ADAPTER = "Parsec Virtual Display Adapter";
        public const string ADAPTER_GUID = "{00b41627-04c4-429e-a26e-0265cf50c8fa}";

        public const string HARDWARE_ID = @"Root\Parsec\VDA";
        public const string CLASS_GUID = "{4d36e968-e325-11ce-bfc1-08002be10318}";

        static IntPtr VddHandle;
        static Timer UpdateTimer;
        static Thread UpdateThread;

        // actually 16 devices could be created per adapter
        // so just use a half to avoid plugging lag
        public static int MAX_DISPLAYS => 8;

        public static int DisplayCount { get; private set; } = 0;

        public delegate void DisplayChangedCallback(List<Display> displays, bool noMonitors);
        public static event DisplayChangedCallback DisplayChanged;

        public static bool Init()
        {
            if (Device.OpenHandle(ADAPTER_GUID, out VddHandle))
            {
                UpdateThread = new Thread(() =>
                {
                    Control.CheckForIllegalCrossThreadCalls = false;

                    UpdateTimer = new Timer();
                    UpdateTimer.Tick += delegate { Ping(); };
                    UpdateTimer.Interval = 50;
                    UpdateTimer.Start();

                    Application.Run();
                });

                UpdateThread.SetApartmentState(ApartmentState.STA);
                UpdateThread.Start();

                SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
                SystemEvents.SessionEnding += SessionEnding;
                return true;
            }

            return false;
        }

        public static void Uninit()
        {
            //Config.DisplayCount = DisplayCount;
            SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;

            //UpdateTimer.Tick -= UpdateRoutine;
            UpdateTimer.Stop();
            UpdateThread.Abort();

            Device.CloseHandle(VddHandle);
        }

        static void UpdateRoutine(object s, EventArgs e)
        {
            // TODO: restore added displays

            //int initialCount = Config.DisplayCount;
            //if (initialCount > 0)
            //{
            //    for (int i = 0; i < initialCount; i++)
            //        AddDisplay();
            //}

            //var sw = Stopwatch.StartNew();
            //Core.Update(VddHandle);
        }

        public static void Invalidate()
        {
            DisplaySettingsChanged(null, EventArgs.Empty);
        }

        static void DisplaySettingsChanged(object sender, EventArgs e)
        {
            var displays = Display.GetAllDisplays();
            bool noMonitors = displays.Count == 0;

            displays = displays.FindAll(d => d.DisplayName
                .Equals(DISPLAY_ID, StringComparison.OrdinalIgnoreCase));

            DisplayCount = displays.Count;
            noMonitors = DisplayCount == 0 && noMonitors;

            DisplayChanged?.Invoke(displays, noMonitors);
        }

        static void SessionEnding(object sender, SessionEndingEventArgs e)
        {
            Config.DisplayCount = DisplayCount;
        }

        public static Device.Status QueryStatus()
        {
            return Device.QueryStatus(CLASS_GUID, HARDWARE_ID);
        }

        public static bool QueryVersion(out string version)
        {
            if (Core.IoControl(VddHandle, Core.IOCTL_VERSION, null, out int vernum, 100))
            {
                int major = 0;
                int minor = vernum & 0xFFFF;
                version = $"{major}.{minor}";
                return true;
            }
            else
            {
                version = "0.???";
                return false;
            }     
        }

        public static bool AddDisplay(out int index)
        {
            if (Core.IoControl(VddHandle, Core.IOCTL_ADD, null, out index, 5000))
            {
                Ping();
                return true;
            }

            return false;
        }

        public static bool RemoveDisplay(int index)
        {
            var input = new byte[2];
            input[1] = (byte)(index & 0xFF);

            if (Core.IoControl(VddHandle, Core.IOCTL_REMOVE, input, out var _, 1000))
            {
                Ping();
                return true;
            }

            return false;
        }

        public static void RemoveLastDisplay()
        {
            if (DisplayCount > 0)
            {
                int index = DisplayCount - 1;
                RemoveDisplay(index);
            }
        }

        public static void Ping()
        {
            Core.IoControl(VddHandle, Core.IOCTL_UPDATE, null, out var _, 1000);
        }

        static unsafe class Core
        {
            public const uint IOCTL_ADD = 0x22E004;
            public const uint IOCTL_REMOVE = 0x22A008;
            public const uint IOCTL_UPDATE = 0x22A00C;
            public const uint IOCTL_VERSION = 0x22E010;

            public static bool IoControl(IntPtr handle, uint code, byte[] input, out int result, int timeout)
            {
                var InBuffer = new byte[32];
                var Overlapped = new Native.OVERLAPPED();
                int OutBuffer = 0;

                if (input != null && input.Length > 0)
                {
                    Array.Copy(input, InBuffer, Math.Min(input.Length, InBuffer.Length));
                }

                fixed (byte* buffer = InBuffer)
                {
                    Overlapped.hEvent = Native.CreateEvent(null, false, false, null);

                    Native.DeviceIoControl(handle, code,
                        buffer, InBuffer.Length,
                        &OutBuffer, sizeof(uint),
                        null, ref Overlapped);

                    bool success = Native.GetOverlappedResultEx(handle, ref Overlapped,
                        out var NumberOfBytesTransferred, timeout, false);

                    if (Overlapped.hEvent != IntPtr.Zero)
                        Native.CloseHandle(Overlapped.hEvent);

                    result = OutBuffer;
                    return success;
                }
            }
        }

        public static IList<Display.Mode> GetCustomDisplayModes()
        {
            var list = new List<Display.Mode>();

            using (var vdd = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Parsec\\vdd", RegistryKeyPermissionCheck.ReadSubTree))
            {
                if (vdd != null)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        using (var index = vdd.OpenSubKey($"{i}", RegistryKeyPermissionCheck.ReadSubTree))
                        {
                            if (index != null)
                            {
                                var width = index.GetValue("width");
                                var height = index.GetValue("height");
                                var hz = index.GetValue("hz");

                                if (width != null && height != null && hz != null)
                                {
                                    list.Add(new Display.Mode
                                    {
                                        Width = Convert.ToUInt16(width),
                                        Height = Convert.ToUInt16(height),
                                        Hz = Convert.ToUInt16(hz),
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return list;
        }

        // Requires admin perm
        public static void SetCustomDisplayModes(List<Display.Mode> modes)
        {
            using (var vdd = Registry.LocalMachine.CreateSubKey("SOFTWARE\\Parsec\\vdd", RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (vdd != null)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        using (var index = vdd.CreateSubKey($"{i}", RegistryKeyPermissionCheck.ReadWriteSubTree))
                        {
                            if (i >= modes.Count && index != null)
                            {
                                index.Dispose();
                                vdd.DeleteSubKey($"{i}");
                            }
                            else if (index != null)
                            {
                                index.SetValue("width", modes[i].Width, RegistryValueKind.DWord);
                                index.SetValue("height", modes[i].Height, RegistryValueKind.DWord);
                                index.SetValue("hz", modes[i].Hz, RegistryValueKind.DWord);
                            }
                        }
                    }
                }
            }
        }

        public enum ParentGPU
        {
            Auto = 0,
            NVIDIA = 0x10DE,
            AMD = 0x1002,
        }

        public static ParentGPU GetParentGPU()
        {
            using (var parameters = Registry.LocalMachine.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\WUDF\\Services\\ParsecVDA\\Parameters",
                RegistryKeyPermissionCheck.ReadSubTree))
            {
                if (parameters != null)
                {
                    object value = parameters.GetValue("PreferredRenderAdapterVendorId");
                    if (value != null)
                    {
                        return (ParentGPU)Convert.ToInt32(value);
                    }
                }
            }

            return ParentGPU.Auto;
        }

        // Requires admin perm
        public static void SetParentGPU(ParentGPU kind)
        {
            using (var parameters = Registry.LocalMachine.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\WUDF\\Services\\ParsecVDA\\Parameters",
                RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (parameters != null)
                {
                    if (kind == ParentGPU.Auto)
                    {
                        parameters.DeleteValue("PreferredRenderAdapterVendorId", false);
                    }
                    else
                    {
                        parameters.SetValue("PreferredRenderAdapterVendorId",
                            (uint)kind, RegistryValueKind.DWord);
                    }
                }
            }
        }

        static unsafe class Native
        {
            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeviceIoControl(
                IntPtr device, uint code,
                void* lpInBuffer, int nInBufferSize,
                void* lpOutBuffer, int nOutBufferSize,
                void* lpBytesReturned,
                ref OVERLAPPED lpOverlapped
            );

            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetOverlappedResult(
                IntPtr handle,
                ref OVERLAPPED lpOverlapped,
                out uint lpNumberOfBytesTransferred,
                [MarshalAs(UnmanagedType.Bool)] bool bWait
            );

            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool GetOverlappedResultEx(
                IntPtr handle,
                ref OVERLAPPED lpOverlapped,
                out uint lpNumberOfBytesTransferred,
                int dwMilliseconds,
                [MarshalAs(UnmanagedType.Bool)] bool bAlertable
            );

            [StructLayout(LayoutKind.Sequential)]
            public struct OVERLAPPED
            {
                public IntPtr Internal;
                public IntPtr InternalHigh;
                public IntPtr Pointer;
                public IntPtr hEvent;
            }

            [DllImport("kernel32.dll", EntryPoint = "CreateEventW", CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateEvent(
                void* lpEventAttributes,
                [MarshalAs(UnmanagedType.Bool)] bool bManualReset,
                [MarshalAs(UnmanagedType.Bool)] bool bInitialState,
                string lpName
            );

            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseHandle(IntPtr handle);
        }
    }
}