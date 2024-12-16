using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ParsecVDisplay.Vdd
{
    internal static unsafe class Core
    {
        public const string NAME = "Parsec Virtual Display";

        public const string DISPLAY_ID = "PSCCDD0";
        public const string DISPLAY_NAME = "ParsecVDA";

        public const string ADAPTER = "Parsec Virtual Display Adapter";
        public const string ADAPTER_GUID = "{00b41627-04c4-429e-a26e-0265cf50c8fa}";

        public const string HARDWARE_ID = @"Root\Parsec\VDA";
        public const string CLASS_GUID = "{4d36e968-e325-11ce-bfc1-08002be10318}";

        // actually 16 devices could be created per adapter
        // so just use a half to avoid plugging lag
        public static int MAX_DISPLAYS => 8;

        public static bool OpenHandle(out IntPtr vdd)
        {
            if (Device.OpenHandle(ADAPTER_GUID, out vdd))
            {
                Update(vdd);
                return true;
            }

            return false;
        }

        public static void CloseHandle(IntPtr vdd)
        {
            Device.CloseHandle(vdd);
        }

        public static List<Display> GetDisplays(out bool noMonitors)
        {
            var displays = Display.GetAllDisplays();
            noMonitors = displays.Count == 0;

            displays = displays.FindAll(d => d.DisplayName
                .Equals(DISPLAY_ID, StringComparison.OrdinalIgnoreCase));

            noMonitors = displays.Count == 0 && noMonitors;
            return displays;
        }

        public static List<Display> GetDisplays()
        {
            return GetDisplays(out var _);
        }

        /// <summary>
        /// Query the driver device status.
        /// </summary>
        public static Device.Status QueryStatus(out Version version)
        {
            return Device.QueryStatus(CLASS_GUID, HARDWARE_ID, out version);
        }

        /// <summary>
        /// Get driver version from the device handle.
        /// </summary>
        public static bool GetVersion(IntPtr vdd, out string version)
        {
            if (IoControl(vdd, IoCtlCode.IOCTL_VERSION, null, out int vernum, 100))
            {
                int major = (vernum >> 16) & 0xFFFF;
                int minor = vernum & 0xFFFF;
                version = $"{major}.{minor}";
                return true;
            }
            else
            {
                version = "(unknown)";
                return false;
            }
        }

        /// <summary>
        /// Add a virtual display and retrieve the index.
        /// </summary>
        public static bool AddDisplay(IntPtr vdd, out int index)
        {
            if (IoControl(vdd, IoCtlCode.IOCTL_ADD, null, out index, 5000))
            {
                Update(vdd);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Remove an added display by index.
        /// </summary>
        public static bool RemoveDisplay(IntPtr vdd, int index)
        {
            var input = new byte[2];
            input[1] = (byte)(index & 0xFF);

            if (IoControl(vdd, IoCtlCode.IOCTL_REMOVE, input, 1000))
            {
                Update(vdd);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Update driver session to keep added displays alive.
        /// </summary>
        public static void Update(IntPtr vdd)
        {
            IoControl(vdd, IoCtlCode.IOCTL_UPDATE, null, 1000);
        }

        private enum IoCtlCode
        {
            IOCTL_ADD       = 0x22E004,
            IOCTL_REMOVE    = 0x22A008,
            IOCTL_UPDATE    = 0x22A00C,
            IOCTL_VERSION   = 0x22E010,

            // new code in driver v0.45
            // relates to IOCTL_UPDATE and per display state
            // but unused in Parsec app
            IOCTL_UNKNOWN1  = 0x22A014,
        }

        /// <summary>
        /// Send IO control code to the driver device handle.
        /// </summary>
        private static bool IoControl(IntPtr handle, IoCtlCode code, byte[] input, int* result, int timeout)
        {
            var InBuffer = new byte[32];
            var Overlapped = new Native.OVERLAPPED();

            if (input != null && input.Length > 0)
            {
                Array.Copy(input, InBuffer, Math.Min(input.Length, InBuffer.Length));
            }

            fixed (byte* buffer = InBuffer)
            {
                int outputLength = result != null ? sizeof(int) : 0;
                Overlapped.hEvent = Native.CreateEvent(null, false, false, null);

                Native.DeviceIoControl(handle, (uint)code,
                    buffer, InBuffer.Length,
                    result, outputLength,
                    null, ref Overlapped);

                bool success = Native.GetOverlappedResultEx(handle, ref Overlapped,
                    out var NumberOfBytesTransferred, timeout, false);

                if (Overlapped.hEvent != IntPtr.Zero)
                    Native.CloseHandle(Overlapped.hEvent);

                return success;
            }
        }

        private static bool IoControl(IntPtr handle, IoCtlCode code, byte[] input, int timeout)
        {
            return IoControl(handle, code, input, null, timeout);
        }

        private static bool IoControl(IntPtr handle, IoCtlCode code, byte[] input, out int result, int timeout)
        {
            int output;
            bool success = IoControl(handle, code, input, &output, timeout);
            result = output;
            return success;
        }
        
        private static class Native
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