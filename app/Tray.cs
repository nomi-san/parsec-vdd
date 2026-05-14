using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ParsecVDisplay
{
    internal class Tray : ApplicationContext
    {
        public static Tray Instance { get; private set; }
        static IWin32Window Owner => new Helper.ArbitraryWindow(MainWindow.Handle);

        NotifyIcon TrayIcon;
        Thread GuiThread;

        ToolStripMenuItem MI_Language;

        ToolStripMenuItem MI_RunOnStartup;
        ToolStripMenuItem MI_RestoreDisplays;
        ToolStripMenuItem MI_FallbackDisplay;
        ToolStripMenuItem MI_KeepScreenOn;

        // Snapshot captured at PBT_APMSUSPEND so we can restore on wake.
        // Lives only for the duration of one suspend/resume cycle.
        List<Display.State> SuspendSnapshot;

        // Windows fires multiple resume events (RESUMESUSPEND, RESUMEAUTOMATIC,
        // possibly RESUMESTANDBY) within ~100ms. We only want to run the
        // restore path once per cycle. Reset by the next suspend.
        int ResumeHandled;

        // Guards against re-entering AddDisplay before the previous attempt
        // has fully unwound (the MainWindow.DisplayChanged fallback path can
        // re-trigger us while we're still showing the error MessageBox).
        int InAddDisplay;

        // Driver index of the fallback display we auto-added because the host
        // had no physical display. -1 = not active. Reset on suspend.
        int FallbackDriverIndex = -1;

        // 1-second debounce — Windows fires multiple DisplaySettingsChanged
        // events in a storm during display changes; we only act on the trailing
        // edge so we don't flap displays during transitions.
        System.Threading.Timer FallbackTimer;
        const int FallbackEvaluateDelayMs = 1000;

        //  ParsecVDisplay v{version}
        //  ______________
        //  Add display
        //  Remove last display
        //  --------------
        //  Options        >   Run on startup
        //                 |   Restore displays
        //                 |   --------------
        //                 |   Fallback display
        //                 |   Keep screen on
        //  Language       >   {lang_1}
        //                 |   {lang_2}
        //                 |   ...
        //  Check update
        //  --------------
        //  Exit

        public Tray()
        {
            Instance = this;
            Vdd.Controller.Start();

            GuiThread = new Thread(App.Main);
            GuiThread.IsBackground = true;
            GuiThread.SetApartmentState(ApartmentState.STA);
            GuiThread.Start();

            var appName = $"{Program.AppName} v{Program.AppVersion}";
            var appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            var translateIcon = (Image)Properties.Resources.ResourceManager.GetObject("translate_icon");

            TrayIcon = new NotifyIcon()
            {
                Text = appName,
                Icon = appIcon,
                Visible = true,
                ContextMenuStrip = new ContextMenuStrip()
                {
                    Items =
                    {
                        new ToolStripMenuItem(appName, appIcon.ToBitmap(), QueryDriver),
                        new ToolStripSeparator(),
                        new ToolStripMenuItem("t_add_display", null, AddDisplay),
                        new ToolStripMenuItem("t_remove_last_display", null, RemoveLastDisplay),
                        new ToolStripSeparator(),
                        new ToolStripMenuItem("t_options")
                        {
                            DropDownItems =
                            {
                                (MI_RunOnStartup = new ToolStripMenuItem("t_run_on_startup",
                                    null, OptionsCheck) { CheckOnClick = true, Checked = Config.RunOnStartup }),
                                (MI_RestoreDisplays = new ToolStripMenuItem("t_restore_displays",
                                    null, OptionsCheck) { CheckOnClick = true, Checked = Config.RestoreDisplays }),
                                new ToolStripSeparator(),
                                (MI_FallbackDisplay = new ToolStripMenuItem("t_fallback_display",
                                    null, OptionsCheck) { CheckOnClick = true, Checked = Config.FallbackDisplay }),
                                (MI_KeepScreenOn = new ToolStripMenuItem("t_keep_screen_on",
                                    null, OptionsCheck) { CheckOnClick = true, Checked = Config.KeepScreenOn }),
                            }
                        },
                        (MI_Language = new ToolStripMenuItem("t_language", translateIcon)),
                        new ToolStripMenuItem("t_check_for_update", null, CheckUpdate),
                        new ToolStripSeparator(),
                        new ToolStripMenuItem("t_exit", null, Exit),
                    }
                }
            };

            var selectedLanguage = Config.Language;
            foreach (var lang in App.Languages)
            {
                var item = new ToolStripMenuItem(lang, null, SetLanguage);
                if (selectedLanguage == lang)
                    item.Checked = true;
                MI_Language.DropDownItems.Add(item);
            }

            UpdateContent();

            TrayIcon.DoubleClick += delegate { ShowApp(); };
            TrayIcon.Visible = true;

            FallbackTimer = new System.Threading.Timer(EvaluateFallback,
                null, Timeout.Infinite, Timeout.Infinite);

            SystemEvents.SessionEnding += SaveDisplayState;
            SystemEvents.SessionSwitch += SaveDisplayState;
            SystemEvents.DisplaySettingsChanged += SaveDisplayState;
            SystemEvents.DisplaySettingsChanged += ScheduleFallbackEvaluation;
            PowerEvents.PowerModeChanged += OnPowerModeChanged;

            // Restore previously-saved displays as soon as the driver handle is ready.
            // Runs on a background thread so the tray construction doesn't block.
            Task.Run(() =>
            {
                if (Vdd.Controller.WaitForReady(10000))
                    RestoreDisplays();
            });

            Invoke(async () =>
            {
                await Task.Delay(1000);
                CheckUpdate(null, null);
            });
        }

        private void WarnVddStatus(Device.Status status)
        {
            if (status == Device.Status.OK)
                return;

            string error = null;
            switch (status)
            {
                case Device.Status.RESTART_REQUIRED:
                    error = App.GetTranslation("t_msg_must_restart_pc");
                    break;
                case Device.Status.DISABLED:
                    error = App.GetTranslation("t_msg_driver_is_disabled", Vdd.Core.ADAPTER);
                    break;
                case Device.Status.NOT_INSTALLED:
                    error = App.GetTranslation("t_msg_please_install_driver");
                    break;
                default:
                    error = App.GetTranslation("t_msg_driver_status_not_ok", status);
                    break;
            }

            if (error != null)
                MessageBox.Show(Owner, error, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public void HandleVddError(Exception ex)
        {
            string message = null;

            if (ex is Vdd.ErrorDriverStatus errStatus)
            {
                switch (errStatus.Status)
                {
                    case Device.Status.RESTART_REQUIRED:
                        message = App.GetTranslation("t_msg_must_restart_pc");
                        break;
                    case Device.Status.DISABLED:
                        message = App.GetTranslation("t_msg_driver_is_disabled", Vdd.Core.ADAPTER);
                        break;
                    case Device.Status.NOT_INSTALLED:
                        message = App.GetTranslation("t_msg_please_install_driver");
                        break;
                    default:
                        message = App.GetTranslation("t_msg_driver_status_not_ok", errStatus.Status);
                        break;
                }
            }
            else if (ex is Vdd.ErrorDeviceHandle)
            {
                message = App.GetTranslation("t_msg_failed_to_obtain_handle");
            }
            else if (ex is Vdd.ErrorExceededLimit errLimit)
            {
                message = App.GetTranslation("t_msg_exceeded_display_limit", errLimit.Limit);
            }
            else if (ex is Vdd.ErrorOperationFailed errOperation)
            {
                switch (errOperation.Type)
                {
                    case Vdd.ErrorOperationFailed.Operation.AddDisplay:
                        message = App.GetTranslation("t_msg_failed_to_add_display");
                        break;
                    case Vdd.ErrorOperationFailed.Operation.RemoveDisplay:
                        message = App.GetTranslation("t_msg_failed_to_remove_display");
                        break;
                }
            }
            else
            {
                message = ex.ToString();
            }

            if (message != null)
            {
                MessageBox.Show(Owner, message,
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Restore previously-saved displays from Config (called once at startup).
        /// </summary>
        void RestoreDisplays()
        {
            if (!Config.RestoreDisplays)
                return;

            var states = Display.UnpackStates(Config.SavedDisplays);
            if (states.Count == 0)
                return;

            RestoreFromStates(states);
        }

        /// <summary>
        /// Add N virtual displays and apply the saved per-display modes
        /// atomically (CDS_NORESET on each, single Commit at the end).
        /// </summary>
        void RestoreFromStates(List<Display.State> states)
        {
            int wanted = Math.Min(states.Count, Vdd.Core.MAX_DISPLAYS);

            int existing = Vdd.Core.GetDisplays().Count;
            int toAdd = Math.Max(0, wanted - existing);

            for (int i = 0; i < toAdd; i++)
            {
                try
                {
                    Vdd.Controller.AddDisplay();

                    // Settle: let DisplaySettingsChanged callbacks finish on
                    // other threads before issuing the next IOCTL.
                    if (i + 1 < toAdd)
                        Thread.Sleep(500);
                }
                catch
                {
                    break;
                }
            }

            List<Display> displays = null;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                displays = Vdd.Core.GetDisplays();
                if (displays.Count >= wanted)
                    break;
                Thread.Sleep(100);
            }
            if (displays == null || displays.Count == 0)
                return;

            int n = Math.Min(displays.Count, states.Count);
            bool anyDeferred = false;
            for (int i = 0; i < n; i++)
            {
                if (!displays[i].Active)
                    continue;

                var s = states[i];
                if (s.Width <= 0 || s.Height <= 0 || s.Hz <= 0)
                    continue;

                if (displays[i].ChangeMode(s.Width, s.Height, s.Hz, s.Orientation, defer: true))
                    anyDeferred = true;
            }

            if (anyDeferred)
                Display.CommitChanges();
        }

        void ScheduleFallbackEvaluation(object sender, EventArgs e)
        {
            FallbackTimer?.Change(FallbackEvaluateDelayMs, Timeout.Infinite);
        }

        void EvaluateFallback(object state)
        {
            if (!Config.FallbackDisplay) return;
            if (Vdd.Controller.IsSuspended) return;

            int physical = Vdd.Core.CountPhysicalDisplays();

            // Drop a stale tracked index if the user removed the display manually
            if (FallbackDriverIndex >= 0)
            {
                var parsecs = Vdd.Core.GetDisplays();
                bool stillThere = false;
                foreach (var d in parsecs)
                    if (d.DisplayIndex == FallbackDriverIndex) { stillThere = true; break; }
                if (!stillThere)
                    FallbackDriverIndex = -1;
            }

            if (physical == 0 && FallbackDriverIndex < 0)
            {
                try
                {
                    Vdd.Controller.AddDisplay(out int idx);
                    FallbackDriverIndex = idx;
                }
                catch { }
            }
            else if (physical > 0 && FallbackDriverIndex >= 0)
            {
                try { Vdd.Controller.RemoveDisplay(FallbackDriverIndex); }
                catch { }
                FallbackDriverIndex = -1;
            }
        }

        void OnPowerModeChanged(object sender, PowerEvents.PowerBroadcastType type)
        {
            switch (type)
            {
                case PowerEvents.PowerBroadcastType.PBT_APMSUSPEND:
                case PowerEvents.PowerBroadcastType.PBT_APMSTANDBY:
                    Interlocked.Exchange(ref ResumeHandled, 0);
                    // The display we tracked is about to be unplugged; the
                    // resume path will re-evaluate fallback from scratch.
                    FallbackDriverIndex = -1;
                    try { SuspendSnapshot = Vdd.Controller.Suspend(); }
                    catch { }
                    break;

                case PowerEvents.PowerBroadcastType.PBT_APMRESUMEAUTOMATIC:
                case PowerEvents.PowerBroadcastType.PBT_APMRESUMESUSPEND:
                case PowerEvents.PowerBroadcastType.PBT_APMRESUMESTANDBY:
                case PowerEvents.PowerBroadcastType.PBT_APMRESUMECRITICAL:
                    // Coalesce: Windows fires several resume events back-to-back.
                    if (Interlocked.Exchange(ref ResumeHandled, 1) == 0)
                        Task.Run(OnResume);
                    break;
            }
        }

        void OnResume()
        {
            try
            {
                Vdd.Controller.Resume();
                if (!Vdd.Controller.WaitForReady(10000))
                    return;

                var snap = Interlocked.Exchange(ref SuspendSnapshot, null);
                if (snap == null || snap.Count == 0)
                    return;

                RestoreFromStates(snap);
            }
            catch { }
        }

        public void AddDisplay(object sender, EventArgs e)
        {
            if (Interlocked.Exchange(ref InAddDisplay, 1) != 0)
                return;

            try
            {
                Vdd.Controller.AddDisplay();
            }
            catch (Exception ex)
            {
                HandleVddError(ex);
            }
            finally
            {
                Interlocked.Exchange(ref InAddDisplay, 0);
            }
        }

        public void RemoveDisplay(int index)
        {
            try
            {
                Vdd.Controller.RemoveDisplay(index);
            }
            catch (Vdd.ErrorOperationFailed)
            {
                MessageBox.Show(Owner, App.GetTranslation("t_msg_failed_to_remove_display"),
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void RemoveLastDisplay(object sender, EventArgs e)
        {
            try
            {
                Vdd.Controller.RemoveLastDisplay();
            }
            catch (Vdd.ErrorOperationFailed)
            {
                MessageBox.Show(Owner, App.GetTranslation("t_msg_failed_to_remove_display"),
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        public void QueryDriver(object sender, EventArgs e)
        {
            ShowApp();

            var status = Vdd.Core.QueryStatus(out var version);
            var caption = $"{Program.AppName} v{Program.AppVersion}";

            MessageBox.Show(Owner,
                $"{Vdd.Core.ADAPTER}\n\n" +
                $"- Version: {version}\n" +
                $"- {App.GetTranslation("t_msg_driver_status")}: {status}",
                caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        async void CheckUpdate(object sender, EventArgs e)
        {
            ToolStripMenuItem menuItem = null;
            if (sender is ToolStripMenuItem)
            {
                menuItem = (ToolStripMenuItem)sender;
                menuItem.Enabled = false;
            }

            var newVersion = await Updater.CheckUpdate()
                .ConfigureAwait(false);

            if (!string.IsNullOrEmpty(newVersion))
            {
                var ret = MessageBox.Show(Owner, App.GetTranslation("t_msg_update_available", newVersion),
                    Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (ret == DialogResult.Yes)
                {
                    Helper.OpenLink(Updater.DOWNLOAD_URL);
                }
            }
            else if (sender != null)
            {
                MessageBox.Show(Owner, App.GetTranslation("t_msg_up_to_date"),
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            if (menuItem != null)
            {
                menuItem.Enabled = true;
            }
        }

        void OptionsCheck(object sender, EventArgs e)
        {
            if (sender == MI_RunOnStartup)
                Config.RunOnStartup = MI_RunOnStartup.Checked;
            else if (sender == MI_RestoreDisplays)
            {
                Config.RestoreDisplays = MI_RestoreDisplays.Checked;
                if (MI_RestoreDisplays.Checked)
                {
                    // Enabling → snapshot the current state right away so a quick
                    // restart restores what the user has on screen.
                    SaveDisplayState(null, EventArgs.Empty);
                }
                else
                {
                    Config.SavedDisplays = string.Empty;
                }
            }
            else if (sender == MI_FallbackDisplay)
            {
                Config.FallbackDisplay = MI_FallbackDisplay.Checked;
                if (MI_FallbackDisplay.Checked)
                {
                    // Evaluate now in case the host is currently headless
                    FallbackTimer?.Change(0, Timeout.Infinite);
                }
                else if (FallbackDriverIndex >= 0)
                {
                    // Disabling — drop our auto-added display, leave user displays alone
                    try { Vdd.Controller.RemoveDisplay(FallbackDriverIndex); }
                    catch { }
                    FallbackDriverIndex = -1;
                }
            }
            else if (sender == MI_KeepScreenOn)
                Config.KeepScreenOn = MI_KeepScreenOn.Checked;
        }

        void SaveDisplayState(object sender, EventArgs e)
        {
            // Skip while suspended — displays are intentionally unplugged.
            if (Vdd.Controller.IsSuspended)
                return;

            if (!Config.RestoreDisplays)
                return;

            var displays = Vdd.Core.GetDisplays();
            var states = new List<Display.State>(displays.Count);
            foreach (var d in displays)
            {
                if (d.Active && d.CurrentMode != null)
                    states.Add(d.Snapshot());
            }
            Config.SavedDisplays = Display.PackStates(states);
        }

        public void ShowApp()
        {
            MainWindow.Instance?.Dispatcher
                .Invoke(MainWindow.Instance.ShowMe);
        }

        public void UpdateContent()
        {
            void UpdateItem(ToolStripItem item, bool submenu)
            {
                if (item is ToolStripMenuItem mi)
                {
                    if (mi.Tag is string t) { }
                    else
                    {
                        t = mi.Text;
                        mi.Tag = t;
                    }

                    if (!string.IsNullOrEmpty(t) && t.StartsWith("t_"))
                    {
                        mi.Text = App.GetTranslation(t);

                        if (submenu && mi.HasDropDownItems)
                        {
                            foreach (ToolStripItem sub in mi.DropDownItems)
                            {
                                UpdateItem(sub, false);
                            }
                        }
                    }
                }
            }

            var items = TrayIcon.ContextMenuStrip.Items;
            for (int i = 1; i < items.Count; i++)
            {
                UpdateItem(items[i], true);
            }
        }

        void Exit(object sender, EventArgs e)
        {
            var displays = Vdd.Core.GetDisplays();
            // Skip the "remove all displays?" prompt when restore is enabled —
            // the next launch will bring them right back.
            if (displays.Count > 0 && !Config.RestoreDisplays)
            {
                if (MessageBox.Show(Owner, App.GetTranslation("t_msg_prompt_leave_all"),
                    Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            SystemEvents.SessionEnding -= SaveDisplayState;
            SystemEvents.SessionSwitch -= SaveDisplayState;
            SystemEvents.DisplaySettingsChanged -= SaveDisplayState;
            SystemEvents.DisplaySettingsChanged -= ScheduleFallbackEvaluation;
            PowerEvents.PowerModeChanged -= OnPowerModeChanged;
            FallbackTimer?.Dispose();

            // Snapshot one last time so next launch's restore matches the
            // displays the user is exiting with.
            SaveDisplayState(null, EventArgs.Empty);

            // Best-effort explicit removal in reverse order (preserves Windows 10
            // Connectivity registry config). Per-display failures are swallowed —
            // closing the device handle in Controller.Stop triggers the driver's
            // keep-alive watchdog to auto-remove any stragglers within ~1 s.
            for (int i = displays.Count - 1; i >= 0; i--)
            {
                try { Vdd.Controller.RemoveDisplay(displays[i].DisplayIndex); }
                catch { }
            }

            App.Current?.Dispatcher.Invoke(App.Current.Shutdown);
            GuiThread.Join();

            Vdd.Controller.Stop();

            TrayIcon.Visible = false;
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TrayIcon.Dispose();
            }

            base.Dispose(disposing);
        }

        public void Invoke(Action action)
        {
            TrayIcon.ContextMenuStrip.BeginInvoke(action);
        }

        private void SetLanguage(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem mi)
            {
                // Recheck options
                foreach (ToolStripMenuItem item in MI_Language.DropDownItems)
                    item.Checked = mi == item;

                // Update language
                var lang = mi.Text;
                App.SetLanguage(lang);
                UpdateContent();
            }
        }
    }
}