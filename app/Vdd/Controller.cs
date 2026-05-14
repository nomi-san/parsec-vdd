using System;
using System.Collections.Generic;
using System.Threading;

namespace ParsecVDisplay.Vdd
{
    internal static class Controller
    {
        static Thread UpdateThread;
        static Thread StatusThread;
        static CancellationTokenSource Cancellation;
        static IntPtr VddHandle = IntPtr.Zero;

        // Stored as int so Interlocked can be used safely; cast on read
        static int LastStatusValue;
        static Device.Status LastStatus => (Device.Status)Volatile.Read(ref LastStatusValue);

        // Wakes StatusLoop on demand (e.g. after Resume) instead of waiting up to 2s
        static ManualResetEventSlim StatusKick;
        // Signals that the device handle is open and ready to receive IOCTLs
        static ManualResetEventSlim HandleReady;
        // True while the controller is suspended (sleep / hibernation)
        static volatile bool Suspended;

        public static bool IsSuspended => Suspended;
        public static bool IsHandleReady => VddHandle.IsValidHandle();

        public static void Start()
        {
            Cancellation = new CancellationTokenSource();
            StatusKick = new ManualResetEventSlim(false);
            HandleReady = new ManualResetEventSlim(false);
            Suspended = false;

            UpdateThread = new Thread(() => UpdateLoop(Cancellation.Token));
            UpdateThread.IsBackground = true;
            UpdateThread.Priority = ThreadPriority.Highest;

            StatusThread = new Thread(() => StatusLoop(Cancellation.Token));
            StatusThread.IsBackground = true;
            StatusThread.Priority = ThreadPriority.BelowNormal;

            UpdateThread.Start();
            StatusThread.Start();
        }

        public static void Stop()
        {
            Cancellation?.Cancel();
            StatusKick?.Set();
            UpdateThread?.Join();
            StatusThread?.Join();

            var handle = Interlocked.Exchange(ref VddHandle, IntPtr.Zero);
            Device.CloseHandle(handle);

            StatusKick?.Dispose();
            HandleReady?.Dispose();
        }

        /// <summary>
        /// Block until the device handle is open or the timeout elapses.
        /// Returns true if the handle is ready.
        /// </summary>
        public static bool WaitForReady(int timeoutMs)
        {
            return HandleReady != null && HandleReady.Wait(timeoutMs);
        }

        /// <summary>
        /// Force StatusLoop to re-check the driver status now instead of
        /// waiting up to 2 seconds for the next tick. Cheap, no allocation.
        /// </summary>
        public static void KickStatusCheck()
        {
            StatusKick?.Set();
        }

        /// <summary>
        /// Suspend driver activity: snapshot current displays, unplug them in
        /// reverse order (preserves Windows 10 Connectivity registry config),
        /// then close the device handle so no IOCTLs are sent while the system
        /// sleeps. The keep-alive thread becomes a no-op while suspended.
        /// </summary>
        public static List<Display.State> Suspend()
        {
            if (Suspended)
                return new List<Display.State>();

            var displays = Core.GetDisplays();
            var snapshot = displays.ConvertAll(d => d.Snapshot());

            Suspended = true;

            // Unplug in reverse order to keep Windows 10 from inventing a
            // new Connectivity registry entry for the remaining subset.
            if (VddHandle.IsValidHandle())
            {
                for (int i = displays.Count - 1; i >= 0; i--)
                {
                    try { Core.RemoveDisplay(VddHandle, displays[i].DisplayIndex); }
                    catch { /* best effort */ }
                }
            }

            var handle = Interlocked.Exchange(ref VddHandle, IntPtr.Zero);
            HandleReady.Reset();
            Device.CloseHandle(handle);

            return snapshot;
        }

        /// <summary>
        /// Mark the controller as resumed and wake StatusLoop so the handle
        /// is reopened as soon as the driver reports OK. Callers should then
        /// WaitForReady before adding displays.
        /// </summary>
        public static void Resume()
        {
            Suspended = false;
            StatusKick?.Set();
        }

        static void UpdateLoop(CancellationToken cancellation)
        {
            while (!cancellation.IsCancellationRequested)
            {
                if (!Suspended
                    && VddHandle.IsValidHandle()
                    && LastStatus == Device.Status.OK)
                {
                    Core.Update(VddHandle);
                }

                Thread.Sleep(100);
            }
        }

        static void StatusLoop(CancellationToken cancellation)
        {
            while (!cancellation.IsCancellationRequested)
            {
                if (Suspended)
                {
                    // Sleep until Resume() (or Stop) signals us
                    try { StatusKick.Wait(Timeout.Infinite, cancellation); }
                    catch (OperationCanceledException) { break; }
                    StatusKick.Reset();
                    continue;
                }

                var status = QueryStatus(out var _);
                Volatile.Write(ref LastStatusValue, (int)status);

                if (status == Device.Status.OK)
                {
                    if (!VddHandle.IsValidHandle())
                    {
                        Device.OpenHandle(Core.ADAPTER_GUID, out var handle);
                        Interlocked.Exchange(ref VddHandle, handle);
                        if (handle.IsValidHandle())
                            HandleReady.Set();
                    }
                }
                else
                {
                    var handle = Interlocked.Exchange(ref VddHandle, IntPtr.Zero);
                    if (handle != IntPtr.Zero)
                    {
                        HandleReady.Reset();
                        Device.CloseHandle(handle);
                    }
                }

                // Wait up to 2s — KickStatusCheck() / Resume() shortcut this.
                try { StatusKick.Wait(2000, cancellation); }
                catch (OperationCanceledException) { break; }
                StatusKick.Reset();
            }
        }

        public static Device.Status QueryStatus(out Version version)
        {
            return Device.QueryStatus(Core.CLASS_GUID, Core.HARDWARE_ID, out version);
        }

        public static Device.Status QueryStatus()
        {
            return QueryStatus(out var _);
        }

        public static void AddDisplay()
        {
            AddDisplay(out var _);
        }

        public static void AddDisplay(out int driverIndex)
        {
            driverIndex = -1;

            var status = QueryStatus();
            if (status != Device.Status.OK)
                throw new ErrorDriverStatus(status);

            int limit = Core.MAX_DISPLAYS;
            var displays = Core.GetDisplays();

            if (displays.Count >= limit)
                throw new ErrorExceededLimit(limit);

            // Snapshot the handle ONCE so StatusLoop closing it after this
            // check doesn't leave us calling DeviceIoControl on a stale value.
            var handle = VddHandle;
            if (!handle.IsValidHandle())
                throw new ErrorDeviceHandle();

            if (!Core.AddDisplay(handle, out driverIndex))
                throw new ErrorOperationFailed(ErrorOperationFailed.Operation.AddDisplay);
        }

        public static void RemoveDisplay(int index)
        {
            var status = QueryStatus();
            if (status != Device.Status.OK)
                throw new ErrorDriverStatus(status);

            if (index < 0)
                return;

            if (!VddHandle.IsValidHandle())
                throw new ErrorDeviceHandle();

            if (!Core.RemoveDisplay(VddHandle, index))
                throw new ErrorOperationFailed(ErrorOperationFailed.Operation.RemoveDisplay);
        }

        public static void RemoveLastDisplay()
        {
            var displays = Core.GetDisplays();
            if (displays.Count > 0)
            {
                var last = displays[displays.Count - 1];
                RemoveDisplay(last.DisplayIndex);
            }
        }
    }
}
