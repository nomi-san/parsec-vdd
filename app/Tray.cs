using System;
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
                                    null, OptionsCheck) { CheckOnClick = true, Checked = Config.DisplayCount >= 0 }),
                                new ToolStripSeparator(),
                                (MI_FallbackDisplay = new ToolStripMenuItem("t_fallback_display",
                                    null, OptionsCheck) { CheckOnClick = true, Checked = Config.FallbackDisplay }),
                                (MI_KeepScreenOn = new ToolStripMenuItem("t_keep_screen_on",
                                    null, OptionsCheck) { CheckOnClick = true, Checked = Config.KeepScreenOn }),
                            }
                        },
                        (MI_Language = new ToolStripMenuItem("t_language")),
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

            SystemEvents.SessionEnding += SaveDisplayCount;
            SystemEvents.SessionSwitch += SaveDisplayCount;
            SystemEvents.DisplaySettingsChanged += SaveDisplayCount;

            Invoke(async () =>
            {
                await Task.Delay(1000);

                RestoreDisplays();
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

        void RestoreDisplays()
        {
            //var savedCount = Config.DisplayCount;

            //if (savedCount > 0)
            //{
            //    var displays = Vdd.Core.GetDisplays();
            //    var amount = savedCount - displays.Count;

            //    for (int i = 0; i < amount; i++)
            //        Controller.AddDisplay(out var _);
            //}
        }

        public void AddDisplay(object sender, EventArgs e)
        {
            try
            {
                Vdd.Controller.AddDisplay();
            }
            catch (Exception ex)
            {
                HandleVddError(ex);
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
                $"{Vdd.Core.ADAPTER}\n" +
                $"Version: {version}\n" +
                $"{App.GetTranslation("t_msg_driver_status")}: {status}",
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
                Config.DisplayCount = MI_RestoreDisplays.Checked ? Vdd.Core.GetDisplays().Count : -1;
            else if (sender == MI_FallbackDisplay)
                Config.FallbackDisplay = MI_FallbackDisplay.Checked;
            else if (sender == MI_KeepScreenOn)
                Config.KeepScreenOn = MI_KeepScreenOn.Checked;
        }

        void SaveDisplayCount(object sender, EventArgs e)
        {
            if (Config.DisplayCount >= 0)
            {
                var displays = Vdd.Core.GetDisplays();
                Config.DisplayCount = displays.Count;
            }
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
            if (displays.Count > 0)
            {
                if (MessageBox.Show(Owner, App.GetTranslation("t_msg_prompt_leave_all"),
                    Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;
            }

            SystemEvents.SessionEnding -= SaveDisplayCount;
            SystemEvents.SessionSwitch -= SaveDisplayCount;
            SystemEvents.DisplaySettingsChanged -= SaveDisplayCount;

            if (displays.Count > 0)
            {
                for (int i = displays.Count - 1; i >= 0; i--)
                {
                    var index = displays[i].DisplayIndex;
                    Vdd.Controller.RemoveDisplay(index);
                }

                if (Config.DisplayCount >= 0)
                    Config.DisplayCount = displays.Count;
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
            TrayIcon.ContextMenuStrip.Invoke(action);
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