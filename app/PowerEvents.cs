using System;
using System.Runtime.InteropServices;

namespace ParsecVDisplay
{
    internal static class PowerEvents
    {
        static EventHandler<PowerBroadcastType> _powerModeChanged;
        public static event EventHandler<PowerBroadcastType> PowerModeChanged
        {
            add
            {
                _powerModeChanged += value;
                if (_powerEventHandler == IntPtr.Zero)
                {
                    var result = Native.PowerRegisterSuspendResumeNotification(2, _dnsp, out _powerEventHandler);
                    if (result != 0)
                        throw new Exception("Failed To Register PowerSuspendResumeNotification");
                }

            }
            remove
            {
                _powerModeChanged -= value;
                if (_powerModeChanged == null)
                {
                    if (Native.PowerUnregisterSuspendResumeNotification(_powerEventHandler) != 0)
                        throw new Exception("Failed To Unregister PowerSuspendResumeNotification");
                    _powerEventHandler = IntPtr.Zero;
                }
            }
        }

        static IntPtr _powerEventHandler;
        static Native.DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS _dnsp = new Native.DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS
        {
            Callback = OnDeviceNotify,
            Context = IntPtr.Zero
        };

        static uint OnDeviceNotify(IntPtr context, uint type, IntPtr setting)
        {
            _powerModeChanged?.Invoke(null, (PowerBroadcastType)type);
            return 0;
        }

        public enum PowerBroadcastType
        {
            PBT_APMQUERYSUSPEND = 0,
            //
            // Summary:
            //     The PBT_APMQUERYSUSPEND message is sent to request permission to suspend the
            //     computer. An application that grants permission should carry out preparations
            //     for the suspension before returning. Return TRUE to grant the request to suspend.
            //     To deny the request, return BROADCAST_QUERY_DENY.
            PBT_APMQUERYSTANDBY = 1,
            //
            // Summary:
            //     [PBT_APMQUERYSUSPENDFAILED is available for use in the operating systems specified
            //     in the Requirements section. Support for this event was removed in Windows Vista.
            //     Use SetThreadExecutionState instead.]
            //     Notifies applications that permission to suspend the computer was denied. This
            //     event is broadcast if any application or driver returned BROADCAST_QUERY_DENY
            //     to a previous PBT_APMQUERYSUSPEND event.
            //     A window receives this event through the WM_POWERBROADCAST message. The wParam
            //     and lParam parameters are set as described following.
            //
            // Remarks:
            //     lParam: Reserved; must be zero.
            //     No return value.
            //     Applications typically respond to this event by resuming normal operation.
            PBT_APMQUERYSUSPENDFAILED = 2,
            //
            // Summary:
            //     The PBT_APMQUERYSUSPENDFAILED message is sent to notify the application that
            //     suspension was denied by some other application. However, this message is only
            //     sent when we receive PBT_APMQUERY* before.
            PBT_APMQUERYSTANDBYFAILED = 3,
            //
            // Summary:
            //     Notifies applications that the computer is about to enter a suspended state.
            //     This event is typically broadcast when all applications and installable drivers
            //     have returned TRUE to a previous PBT_APMQUERYSUSPEND event.
            //     A window receives this event through the WM_POWERBROADCAST message. The wParam
            //     and lParam parameters are set as described following.
            //
            // Remarks:
            //     lParam: Reserved; must be zero.
            //     No return value.
            //     An application should process this event by completing all tasks necessary to
            //     save data.
            //     The system allows approximately two seconds for an application to handle this
            //     notification. If an application is still performing operations after its time
            //     allotment has expired, the system may interrupt the application.
            PBT_APMSUSPEND = 4,
            //
            // Summary:
            //     Undocumented.
            PBT_APMSTANDBY = 5,
            //
            // Summary:
            //     [PBT_APMRESUMECRITICAL is available for use in the operating systems specified
            //     in the Requirements section. Support for this event was removed in Windows Vista.
            //     Use PBT_APMRESUMEAUTOMATIC instead.]
            //     Notifies applications that the system has resumed operation. This event can indicate
            //     that some or all applications did not receive a PBT_APMSUSPEND event. For example,
            //     this event can be broadcast after a critical suspension caused by a failing battery.
            //     A window receives this event through the WM_POWERBROADCAST message. The wParam
            //     and lParam parameters are set as described following.
            //
            // Remarks:
            //     lParam: Reserved; must be zero.
            //     No return value.
            //     Because a critical suspension occurs without prior notification, resources and
            //     data previously available may not be present when the application receives this
            //     event. The application should attempt to restore its state to the best of its
            //     ability. While in a critical suspension, the system maintains the state of the
            //     DRAM and local hard disks, but may not maintain net connections. An application
            //     may need to take action with respect to files that were open on the network before
            //     critical suspension.
            PBT_APMRESUMECRITICAL = 6,
            //
            // Summary:
            //     Notifies applications that the system has resumed operation after being suspended.
            //     A window receives this event through the WM_POWERBROADCAST message. The wParam
            //     and lParam parameters are set as described following.
            //
            // Remarks:
            //     lParam: Reserved; must be zero.
            //     No return value.
            //     An application can receive this event only if it received the PBT_APMSUSPEND
            //     event before the computer was suspended. Otherwise, the application will receive
            //     a PBT_APMRESUMECRITICAL event.
            //     If the system wakes due to user activity (such as pressing the power button)
            //     or if the system detects user interaction at the physical console (such as mouse
            //     or keyboard input) after waking unattended, the system first broadcasts the PBT_APMRESUMEAUTOMATIC
            //     event, then it broadcasts the PBT_APMRESUMESUSPEND event. In addition, the system
            //     turns on the display. Your application should reopen files that it closed when
            //     the system entered sleep and prepare for user input.
            //     If the system wakes due to an external wake signal (remote wake), the system
            //     broadcasts only the PBT_APMRESUMEAUTOMATIC event. The PBT_APMRESUMESUSPEND event
            //     is not sent.
            PBT_APMRESUMESUSPEND = 7,
            //
            // Summary:
            //     The PBT_APMRESUMESTANDBY event is broadcast as a notification that the system
            //     has resumed operation after being standby.
            PBT_APMRESUMESTANDBY = 8,
            //
            // Summary:
            //     [PBT_APMBATTERYLOW is available for use in the operating systems specified in
            //     the Requirements section. Support for this event was removed in Windows Vista.
            //     Use PBT_APMPOWERSTATUSCHANGE instead.]
            //     Notifies applications that the battery power is low.
            //     A window receives this event through the WM_POWERBROADCAST message. The wParam
            //     and lParam parameters are set as described following.
            //
            // Remarks:
            //     lParam: Reserved, must be zero.
            //     No return value.
            //     This event is broadcast when a system's APM BIOS signals an APM battery low notification.
            //     Because some APM BIOS implementations do not provide notifications when batteries
            //     are low, this event may never be broadcast on some computers.
            PBT_APMBATTERYLOW = 9,
            //
            // Summary:
            //     Notifies applications of a change in the power status of the computer, such as
            //     a switch from battery power to A/C. The system also broadcasts this event when
            //     remaining battery power slips below the threshold specified by the user or if
            //     the battery power changes by a specified percentage.
            //     A window receives this event through the WM_POWERBROADCAST message. The wParam
            //     and lParam parameters are set as described following.
            //
            // Remarks:
            //     lParam: Reserved; must be zero.
            //     No return value.
            //     An application should process this event by calling the GetSystemPowerStatus
            //     function to retrieve the current power status of the computer. In particular,
            //     the application should check the ACLineStatus, BatteryFlag, BatteryLifeTime,
            //     and BatteryLifePercent members of the SYSTEM_POWER_STATUS structure for any changes.
            //     This event can occur when battery life drops to less than 5 minutes, or when
            //     the percentage of battery life drops below 10 percent, or if the battery life
            //     changes by 3 percent.
            PBT_APMPOWERSTATUSCHANGE = 10,
            //
            // Summary:
            //     [PBT_APMOEMEVENT is available for use in the operating systems specified in the
            //     Requirements section. Support for this event was removed in Windows Vista.]
            //     Notifies applications that the APM BIOS has signaled an APM OEM event.
            //     A window receives this event through the WM_POWERBROADCAST message. The wParam
            //     and lParam parameters are set as described following.
            //
            // Remarks:
            //     lParam: The OEM-defined event code that was signaled by the system's APM BIOS.
            //     OEM event codes are in the range 0200h - 02FFh.
            //     No return value.
            //     Because not all APM BIOS implementations provide OEM event notifications, this
            //     event may never be broadcast on some computers.
            PBT_APMOEMEVENT = 11,
            //
            // Summary:
            //     Notifies applications that the system is resuming from sleep or hibernation.
            //     This event is delivered every time the system resumes and does not indicate whether
            //     a user is present.
            //     A window receives this event through the WM_POWERBROADCAST message. The wParam
            //     and lParam parameters are set as described following.
            //
            // Remarks:
            //     lParam: Reserved; must be zero.
            //     No return value.
            //     If the system detects any user activity after broadcasting PBT_APMRESUMEAUTOMATIC,
            //     it will broadcast a PBT_APMRESUMESUSPEND event to let applications know they
            //     can resume full interaction with the user.
            PBT_APMRESUMEAUTOMATIC = 18,
            //
            // Summary:
            //     Power setting change event sent with a WM_POWERBROADCAST window message or in
            //     a HandlerEx notification callback for services.
            //
            // Remarks:
            //     lParam: Pointer to a POWERBROADCAST_SETTING structure.
            //     No return value.
            PBT_POWERSETTINGCHANGE = 32787,
            ERROR_ERROR = 10101
        }

        static class Native
        {
            [UnmanagedFunctionPointer(CallingConvention.Winapi)]
            public delegate uint DeviceNotifyCallbackRoutine(IntPtr Context, uint Type, IntPtr Setting);

            [StructLayout(LayoutKind.Sequential)]
            public struct DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS
            {
                [MarshalAs(UnmanagedType.FunctionPtr)]
                public DeviceNotifyCallbackRoutine Callback;
                public IntPtr Context;
            }

            [DllImport("powrprof.dll", SetLastError = false, ExactSpelling = true)]
            public static extern uint PowerRegisterSuspendResumeNotification(int Flags, in DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS Recipient, out IntPtr RegistrationHandle);

            [DllImport("powrprof.dll", SetLastError = false, ExactSpelling = true)]
            public static extern uint PowerUnregisterSuspendResumeNotification(IntPtr RegistrationHandle);
        }
    }
}