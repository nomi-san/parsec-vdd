using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

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
        static Task UpdateTask;
        static CancellationTokenSource Cancellation;

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
                Cancellation = new CancellationTokenSource();
                UpdateTask = Task.Run(() => UpdateRoutine(Cancellation.Token), Cancellation.Token);

                SystemEvents.DisplaySettingsChanged += DisplaySettingsChanged;
                SystemEvents.SessionEnding += SessionEnding;

                DisplayCount = Config.DisplayCount;
                for(int i = 0; i < DisplayCount; ++i)
                {
                    AddDisplay();
                }

                return true;
            }

            return false;
        }

        public static void Uninit()
        {
            //Config.DisplayCount = DisplayCount;
            SystemEvents.DisplaySettingsChanged -= DisplaySettingsChanged;

            Cancellation?.Cancel();
            UpdateTask?.Wait();

            OnAppClose();
            Device.CloseHandle(VddHandle);

        }

        static async void UpdateRoutine(CancellationToken token)
        {
            // TODO: restore added displays

            //int initialCount = Config.DisplayCount;
            //if (initialCount > 0)
            //{
            //    for (int i = 0; i < initialCount; i++)
            //        AddDisplay();
            //}

            var sw = Stopwatch.StartNew();

            while (!token.IsCancellationRequested)
            {
                long start = sw.ElapsedMilliseconds;

                Core.Update(VddHandle);

                if (token.IsCancellationRequested)
                    break;

                if ((sw.ElapsedMilliseconds - start) < 100)
                {
                    await Task.Delay(80);
                }
            }
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
            OnAppClose();
        }

        static void OnAppClose()
        {
            Config.DisplayCount = DisplayCount;
        }

        public static Device.Status QueryStatus()
        {
            return Device.QueryStatus(CLASS_GUID, HARDWARE_ID);
        }

        public static string QueryVersion()
        {
            Core.Update(VddHandle);
            Core.Version(VddHandle, out int minor);

            return $"0.{minor}";
        }

        public static int AddDisplay()
        {
            Core.Add(VddHandle, out int index);
            Core.Update(VddHandle);

            return index;
        }

        public static void RemoveDisplay(int index)
        {
            Core.Remove(VddHandle, index);
            Core.Update(VddHandle);
        }

        public static void RemoveLastDisplay()
        {
            if (DisplayCount > 0)
            {
                int index = DisplayCount - 1;
                RemoveDisplay(index);
            }
        }

        static unsafe class Core
        {
            public const uint IOCTL_ADD = 0x0022e004;
            public const uint IOCTL_REMOVE = 0x0022a008;
            public const uint IOCTL_UPDATE = 0x0022a00c;
            public const uint IOCTL_VERSION = 0x0022e010;

            static int IoControl(IntPtr handle, uint code, byte[] data)
            {
                var InBuffer = new byte[32];
                var Overlapped = new Native.OVERLAPPED();
                int OutBuffer = 0;

                if (data != null)
                    Array.Copy(data, InBuffer, Math.Min(data.Length, InBuffer.Length));

                fixed (byte* input = InBuffer)
                {
                    Overlapped.hEvent = Native.CreateEventA(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    Native.DeviceIoControl(handle, code, input, InBuffer.Length, &OutBuffer, sizeof(int), IntPtr.Zero, ref Overlapped);
                    Native.GetOverlappedResult(handle, ref Overlapped, out var NumberOfBytesTransferred, true);

                    if (Overlapped.hEvent != IntPtr.Zero)
                        Native.CloseHandle(Overlapped.hEvent);
                }

                return OutBuffer;
            }

            public static void Version(IntPtr handle, out int minor)
            {
                // Remove() takes only 2 bytes for index
                //   so this 4 bytes return could be a combination ((major << 16) | minor)
                //   it should be clear when Parsec VDD comes to v1.0
                minor = IoControl(handle, IOCTL_VERSION, null);
            }

            public static void Update(IntPtr handle)
            {
                IoControl(handle, IOCTL_UPDATE, null);
            }

            public static void Add(IntPtr handle, out int index)
            {
                index = IoControl(handle, IOCTL_ADD, null);
            }

            public static void Remove(IntPtr handle, int index)
            {
                // 16-bit BE index
                var indexData = BitConverter.GetBytes((ushort)unchecked(index & 0xFFFF));
                Array.Reverse(indexData);

                IoControl(handle, IOCTL_REMOVE, indexData);
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

        static unsafe class Native
        {
            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DeviceIoControl(
                IntPtr device, uint code,
                void* lpInBuffer, int nInBufferSize,
                void* lpOutBuffer, int nOutBufferSize,
                IntPtr lpBytesReturned,
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

            [StructLayout(LayoutKind.Sequential)]
            public struct OVERLAPPED
            {
                public IntPtr Internal;
                public IntPtr InternalHigh;
                public IntPtr Pointer;
                public IntPtr hEvent;
            }

            [DllImport("kernel32.dll")]
            public static extern IntPtr CreateEventA(IntPtr a, IntPtr b, IntPtr c, IntPtr d);

            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseHandle(IntPtr handle);
        }
    }
}