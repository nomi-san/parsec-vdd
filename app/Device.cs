﻿using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ParsecVDisplay
{
    internal static unsafe class Device
    {
        public enum Status
        {
            OK,
            INACCESSIBLE,
            UNKNOWN,
            UNKNOWN_PROBLEM,
            DISABLED,
            DRIVER_ERROR,
            RESTART_REQUIRED,
            DISABLED_SERVICE,
            NOT_INSTALLED
        }

        public static Status QueryStatus(string guid, string devId, out Version driverVersion)
        {
            var status = Status.INACCESSIBLE;
            driverVersion = new Version(0, 0, 0, 0);

            var devInfoData = new Native.SP_DEVINFO_DATA();
            devInfoData.cbSize = sizeof(Native.SP_DEVINFO_DATA);

            var classGuid = Guid.Parse(guid);
            var devInfo = Native.SetupDiGetClassDevsA(ref classGuid, null, null, Native.DIGCF_PRESENT);

            if (devInfo != Native.INVALID_HANDLE_VALUE)
            {
                bool foundProp = false;
                uint deviceIndex = 0;

                do
                {
                    if (!Native.SetupDiEnumDeviceInfo(devInfo, deviceIndex, &devInfoData))
                        break;

                    if (!Native.SetupDiBuildDriverInfoList(devInfo, &devInfoData, Native.SPDIT_COMPATDRIVER))
                        break;

                    var driverInfoData = new Native.SP_DRVINFO_DATA_V2_A();
                    driverInfoData.cbSize = Marshal.SizeOf<Native.SP_DRVINFO_DATA_V2_A>();

                    if (Native.SetupDiEnumDriverInfoA(devInfo, &devInfoData, Native.SPDIT_COMPATDRIVER, 0, &driverInfoData))
                        driverVersion = Parse64BitVersion(driverInfoData.DriverVersion);

                    int requiredSize = 0;
                    Native.SetupDiGetDeviceRegistryPropertyA(devInfo, &devInfoData,
                        Native.SPDRP_HARDWAREID, null, null, 0, &requiredSize);

                    if (requiredSize > 0)
                    {
                        uint regDataType = 0;
                        IntPtr propBuffer = Marshal.AllocHGlobal(requiredSize);

                        if (Native.SetupDiGetDeviceRegistryPropertyA(
                            devInfo,
                            &devInfoData,
                            Native.SPDRP_HARDWAREID,
                            &regDataType,
                            (void*)propBuffer,
                            requiredSize,
                            &requiredSize))
                        {
                            if (regDataType == Native.REG_SZ || regDataType == Native.REG_MULTI_SZ)
                            {
                                for (IntPtr cp = propBuffer; ; cp += Native.lstrlenA(cp) + 1)
                                {
                                    if (cp == (IntPtr)(0) || *(byte*)cp == 0 || (ulong)cp >= (ulong)(propBuffer + requiredSize))
                                    {
                                        status = Status.NOT_INSTALLED;
                                        goto except;
                                    }

                                    if (devId.Equals(Marshal.PtrToStringAnsi(cp)))
                                        break;
                                }

                                foundProp = true;
                                uint devStatus, devProblemNum;

                                if (Native.CM_Get_DevNode_Status(&devStatus, &devProblemNum, devInfoData.DevInst, 0) != Native.CR_SUCCESS)
                                {
                                    status = Status.NOT_INSTALLED;
                                    goto except;
                                }

                                if ((devStatus & (Native.DN_DRIVER_LOADED | Native.DN_STARTED)) != 0)
                                {
                                    status = Status.OK;
                                }
                                else if ((devStatus & Native.DN_HAS_PROBLEM) != 0)
                                {
                                    switch (devProblemNum)
                                    {
                                        case Native.CM_PROB_NEED_RESTART:
                                            status = Status.RESTART_REQUIRED;
                                            break;
                                        case Native.CM_PROB_DISABLED:
                                        case Native.CM_PROB_HARDWARE_DISABLED:
                                            status = Status.DISABLED;
                                            break;
                                        case Native.CM_PROB_DISABLED_SERVICE:
                                            status = Status.DISABLED_SERVICE;
                                            break;
                                        default:
                                            if (devProblemNum == Native.CM_PROB_FAILED_POST_START)
                                                status = Status.DRIVER_ERROR;
                                            else
                                                status = Status.UNKNOWN_PROBLEM;
                                            break;
                                    }
                                }
                                else
                                {
                                    status = Status.UNKNOWN;
                                }
                            }
                        }

                    except:
                        Marshal.FreeHGlobal(propBuffer);
                    }

                    ++deviceIndex;
                } while (!foundProp);

                if (!foundProp && Marshal.GetLastWin32Error() != 0)
                    status = Status.NOT_INSTALLED;

                Native.SetupDiDestroyDeviceInfoList(devInfo);
            }

            return status;
        }

        public static bool OpenHandle(string guid, out IntPtr handle)
        {
            handle = IntPtr.Zero;

            var interfaceGuid = Guid.Parse(guid);
            var devInfo = Native.SetupDiGetClassDevsA(ref interfaceGuid,
                null, null, Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);

            if (devInfo != Native.INVALID_HANDLE_VALUE)
            {
                var devInterface = new Native.SP_DEVICE_INTERFACE_DATA();
                devInterface.cbSize = sizeof(Native.SP_DEVICE_INTERFACE_DATA);

                for (uint i = 0; Native.SetupDiEnumDeviceInterfaces(devInfo, null, ref interfaceGuid, i, &devInterface); ++i)
                {
                    int detailSize = 0;
                    Native.SetupDiGetDeviceInterfaceDetailA(devInfo, &devInterface, null, 0, &detailSize, null);

                    var detail = (Native.SP_DEVICE_INTERFACE_DETAIL_DATA_A*)Marshal.AllocHGlobal(detailSize);
                    detail->cbSize = sizeof(Native.SP_DEVICE_INTERFACE_DETAIL_DATA_A);

                    if (Native.SetupDiGetDeviceInterfaceDetailA(devInfo, &devInterface, detail, detailSize, &detailSize, null))
                    {
                        handle = Native.CreateFileA(&detail->DevicePath,
                            Native.GENERIC_READ | Native.GENERIC_WRITE,
                            Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                            null,
                            Native.OPEN_EXISTING,
                            Native.FILE_ATTRIBUTE_NORMAL | Native.FILE_FLAG_NO_BUFFERING | Native.FILE_FLAG_OVERLAPPED | Native.FILE_FLAG_WRITE_THROUGH,
                            null);

                        if (handle.IsValidHandle())
                        {
                            Marshal.FreeHGlobal((IntPtr)detail);
                            break;
                        }
                    }

                    Marshal.FreeHGlobal((IntPtr)detail);
                }

                Native.SetupDiDestroyDeviceInfoList(devInfo);
            }

            return handle.IsValidHandle();
        }

        public static void CloseHandle(IntPtr handle)
        {
            if (handle.IsValidHandle())
            {
                Native.CloseHandle(handle);
            }
        }

        public static string GetDeviceDescription(uint devInst)
        {
            uint propType;
            int length = 128 * sizeof(ushort);
            var buffer = stackalloc byte[length];

            Native.CM_Get_DevNode_PropertyW(devInst,
                ref Native.DEVPROPKEY.Device_DeviceDesc, &propType, buffer, &length, 0);

            return Marshal.PtrToStringUni((IntPtr)buffer);
        }

        public static DateTime GetDeviceLastArrival(uint devInst)
        {
            uint propType;
            long lastArrival;
            int bufferLength = sizeof(long);

            Native.CM_Get_DevNode_PropertyW(devInst,
                ref Native.DEVPROPKEY.Device_LastArrivalDate, &propType, &lastArrival, &bufferLength, 0);

            return DateTime.FromFileTime(lastArrival);
        }

        public static bool GetParentDeviceInstance(uint devInst, out uint parentInst, out string parentId)
        {
            if (Native.CM_Get_Parent(out parentInst, devInst, 0) == 0)
            {
                byte[] idBuffer = new byte[Native.MAX_DEVICE_ID_LEN];
                Native.CM_Get_Device_IDA(parentInst, idBuffer, idBuffer.Length, 0);

                parentId = Encoding.ASCII.GetString(idBuffer).TrimEnd('\0');
                return true;
            }

            parentInst = 0;
            parentId = string.Empty;
            return false;
        }

        public static bool GetDeviceInstance(string deviceId, out uint devInst)
        {
            return Native.CM_Locate_DevNodeA(out devInst, deviceId, 0) == 0;
        }

        private static Version Parse64BitVersion(ulong value)
        {
            var major = (ushort)((value >> 48) & 0xFFFF);
            var minor = (ushort)((value >> 32) & 0xFFFF);
            var build = (ushort)((value >> 16) & 0xFFFF);
            var revision = (ushort)(value & 0xFFFF);

            return new Version(major, minor, build, revision);
        }

        public static bool IsValidHandle(this IntPtr handle)
        {
            return handle != IntPtr.Zero
                && handle != (IntPtr)(-1);
        }

        static class Native
        {
            public const int MAX_DEVICE_ID_LEN = 200;
            public static readonly IntPtr INVALID_HANDLE_VALUE = (IntPtr)(-1);

            public const uint GENERIC_READ = 0x80000000;
            public const uint GENERIC_WRITE = 0x40000000;

            public const uint FILE_SHARE_READ = 0x1;
            public const uint FILE_SHARE_WRITE = 0x2;

            public const uint OPEN_EXISTING = 3;

            public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
            public const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
            public const uint FILE_FLAG_OVERLAPPED = 0x40000000;
            public const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;

            public const uint DIGCF_PRESENT = 0x2;
            public const uint DIGCF_DEVICEINTERFACE = 0x10;

            public const uint SPDRP_HARDWAREID = 0x1;

            public const uint SPDIT_COMPATDRIVER = 2;

            public const uint REG_SZ = 1;
            public const uint REG_MULTI_SZ = 7;

            public const uint CR_SUCCESS = 0;

            public const uint CM_PROB_NEED_RESTART = 0x0000000E; // requires restart
            public const uint CM_PROB_DISABLED = 0x00000016; // devinst is disabled
            public const uint CM_PROB_HARDWARE_DISABLED = 0x0000001D; // device disabled
            public const uint CM_PROB_DISABLED_SERVICE = 0x00000020; // service's Start = 4
            public const uint CM_PROB_FAILED_POST_START = 0x0000002B; // The drivers set the device state to failed

            public const uint DN_DRIVER_LOADED = 0x00000002; // Has Register_Device_Driver
            public const uint DN_STARTED = 0x00000008; // Is currently configured
            public const uint DN_HAS_PROBLEM = 0x00000400; // Need device installer

            public static bool IsValidHandle(IntPtr handle)
            {
                return handle != IntPtr.Zero
                    && handle != (IntPtr)(-1);
            }

            [DllImport("kernel32.dll", CallingConvention = CallingConvention.Cdecl)]
            public static extern int lstrlenA(
                IntPtr lpString);

            [DllImport("kernel32.dll")]
            public static extern IntPtr CreateFileA(
                char* lpFileName,
                uint dwDesiredAccess,
                uint dwShareMode,
                void* lpSecurityAttributes,
                uint dwCreationDisposition,
                uint dwFlagsAndAttributes,
                void* hTemplateFile);

            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseHandle(IntPtr handle);

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

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int left;
                public int top;
                public int right;
                public int bottom;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SP_DEVICE_INTERFACE_DATA
            {
                public int cbSize;
                public Guid InterfaceClassGuid;
                public uint Flags;
                IntPtr _Reserved;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SP_DEVINFO_DATA
            {
                public int cbSize;
                public Guid ClassGuid;
                public uint DevInst;
                IntPtr _Reserved;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SP_DEVICE_INTERFACE_DETAIL_DATA_A
            {
                public int cbSize;
                public char DevicePath;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct SP_DRVINFO_DATA_V2_A
            {
                public int cbSize;
                public uint DriverType;
                private IntPtr Reserved;
                public fixed byte Description[256];
                public fixed byte MfgName[256];
                public fixed byte ProviderName[256];
                public ulong DriverDate;
                public ulong DriverVersion;
            }

            [DllImport("setupapi.dll")]
            public static extern IntPtr SetupDiGetClassDevsA(
                ref Guid ClassGuid,
                void* Enumerator,
                void* hwndParent,
                uint Flags);

            [DllImport("setupapi.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetupDiEnumDeviceInterfaces(
                IntPtr DeviceInfoSet,
                SP_DEVINFO_DATA* DeviceInfoData,
                ref Guid InterfaceClassGuid,
                uint MemberIndex,
                SP_DEVICE_INTERFACE_DATA* DeviceInterfaceData);

            [DllImport("setupapi.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetupDiGetDeviceInterfaceDetailA(
                IntPtr DeviceInfoSet,
                SP_DEVICE_INTERFACE_DATA* DeviceInterfaceData,
                void* DeviceInterfaceDetailData,
                int DeviceInterfaceDetailDataSize,
                int* RequiredSize,
                SP_DEVINFO_DATA* DeviceInfoData);

            [DllImport("setupapi.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetupDiDestroyDeviceInfoList(
                IntPtr DeviceInfoSet);

            [DllImport("setupapi.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetupDiEnumDeviceInfo(
                IntPtr DeviceInfoSet,
                uint MemberIndex,
                SP_DEVINFO_DATA* DeviceInfoData);

            [DllImport("setupapi.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetupDiGetDeviceRegistryPropertyA(
                IntPtr DeviceInfoSet,
                SP_DEVINFO_DATA* DeviceInfoData,
                uint Property,
                uint* PropertyRegDataType,
                void* PropertyBuffer,
                int PropertyBufferSize,
                int* RequiredSize);

            [DllImport("setupapi.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetupDiBuildDriverInfoList(
              IntPtr DeviceInfoSet,
              SP_DEVINFO_DATA* DeviceInfoData,
              uint DriverType);

            [DllImport("setupapi.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetupDiEnumDriverInfoA(
                IntPtr DeviceInfoSet,
                SP_DEVINFO_DATA* DeviceInfoData,
                uint DriverType,
                uint MemberIndex,
                SP_DRVINFO_DATA_V2_A* DriverInfoData);

            [DllImport("setupapi.dll")]
            public static extern uint CM_Get_DevNode_Status(
                uint* pulStatus,
                uint* pulProblemNumber,
                uint dnDevInst,
                uint ulFlags);

            [DllImport("setupapi.dll")]
            public static extern uint CM_Get_Parent(out uint pdnDevInst, uint dnDevInst, uint ulFlags);

            [DllImport("setupapi.dll")]
            public static extern uint CM_Get_Device_IDA(uint dnDevInst, byte[] Buffer, int BufferLen, uint ulFlags);

            [StructLayout(LayoutKind.Sequential)]
            public struct DEVPROPKEY
            {
                public Guid fmtid;
                public uint pid;

                public static DEVPROPKEY Device_LastArrivalDate = new DEVPROPKEY
                {
                    fmtid = Guid.Parse("{83DA6326-97A6-4088-9453-A1923F573B29}"),
                    pid = 102,
                };

                public static DEVPROPKEY Device_DeviceDesc = new DEVPROPKEY
                {
                    fmtid = Guid.Parse("{A45C254E-DF1C-4EFD-8020-67D146A850E0}"),
                    pid = 2
                };
            }

            [DllImport("CfgMgr32.dll")]
            public static extern uint CM_Get_DevNode_PropertyW(
                uint dnDevInst,
                ref DEVPROPKEY PropertyKey,
                uint* PropertyType,
                void* PropertyBuffer,
                int* PropertyBufferSize,
                uint ulFlags);

            [DllImport("CfgMgr32.dll", CharSet = CharSet.Ansi)]
            public static extern uint CM_Locate_DevNodeA(out uint devInst, string deviceId, uint flags);
        }
    }
}