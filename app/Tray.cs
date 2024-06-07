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
        static System.Windows.Controls.ContextMenu Menu;

        Thread AppThread;
        System.Windows.Forms.Timer VddTimer;

        ToolStripMenuItem MI_RunOnStartup;
        ToolStripMenuItem MI_RestoreDisplays;
        ToolStripMenuItem MI_FallbackDisplay;
        ToolStripMenuItem MI_KeepScreenOn;

        //  ParsecVDisplay
        //  ______________
        //  Add display
        //  Remove last display
        //  --------------
        //  Options        >   Run on startup
        //                 |   Restore displays
        //                 |   --------------
        //                 |   Fallback display
        //                 |   Keep screen on
        //  Check update
        //  --------------
        //  Exit

        public Tray()
        {
            Instance = this;

            AppThread = new Thread(App.Main);
            AppThread.IsBackground = true;
            AppThread.SetApartmentState(ApartmentState.STA);
            AppThread.Start();

            VddTimer = new System.Windows.Forms.Timer();
            VddTimer.Interval = 50;
            VddTimer.Tick += VddTimer_Tick;
            VddTimer.Start();

            var appName = Program.AppName;
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
                        new ToolStripMenuItem("t_check_for_update", null, CheckUpdate),
                        new ToolStripSeparator(),
                        new ToolStripMenuItem("t_exit", null, Exit),
                    }
                }
            };

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

        void VddTimer_Tick(object sender, EventArgs e)
        {
            ParsecVDD.Ping();
        }

        void RestoreDisplays()
        {
            var savedCount = Config.DisplayCount;

            if (savedCount > 0)
            {
                var displays = ParsecVDD.GetDisplays();
                var amount = savedCount - displays.Count;

                for (int i = 0; i < amount; i++)
                    ParsecVDD.AddDisplay(out var _);
            }
        }

        public void AddDisplay(object sender, EventArgs e)
        {
            if (ParsecVDD.GetDisplays().Count >= ParsecVDD.MAX_DISPLAYS)
            {
                MessageBox.Show(Owner, App.GetTranslation("t_msg_exceeded_display_limit", ParsecVDD.MAX_DISPLAYS),
                    Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                ParsecVDD.AddDisplay(out var _);
            }
        }

        void RemoveLastDisplay(object sender, EventArgs e)
        {
            var displays = ParsecVDD.GetDisplays();
            if (displays.Count > 0)
            {
                var last = displays[displays.Count - 1];
                ParsecVDD.RemoveDisplay(last.DisplayIndex);
            }
        }

        public void QueryDriver(object sender, EventArgs e)
        {
            ShowApp();

            var status = ParsecVDD.QueryStatus();
            ParsecVDD.QueryVersion(out string version);

            MessageBox.Show(Owner, $"Parsec Virtual Display v{version}\n" +
                $"{App.GetTranslation("t_msg_driver_status")}: {status}",
                Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                Config.DisplayCount = MI_RestoreDisplays.Checked ? ParsecVDD.GetDisplays().Count : -1;
            else if (sender == MI_FallbackDisplay)
                Config.FallbackDisplay = MI_FallbackDisplay.Checked;
            else if (sender == MI_KeepScreenOn)
                Config.KeepScreenOn = MI_KeepScreenOn.Checked;
        }

        void SaveDisplayCount(object sender, EventArgs e)
        {
            if (Config.DisplayCount >= 0)
            {
                var displays = ParsecVDD.GetDisplays();
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

            var items = TrayIcon.ContextMenuStrip.Items;
            for (int i = 1; i < items.Count; i++)
            {
                UpdateItem(items[i], true);
            }
        }

        void Exit(object sender, EventArgs e)
        {
            SystemEvents.SessionEnding -= SaveDisplayCount;
            SystemEvents.SessionSwitch -= SaveDisplayCount;
            SystemEvents.DisplaySettingsChanged -= SaveDisplayCount;

            var displays = ParsecVDD.GetDisplays();
            if (displays.Count > 0)
            {
                if (MessageBox.Show(Owner, App.GetTranslation("t_msg_prompt_leave_all"),
                    Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                    return;

                for (int i = displays.Count - 1; i >= 0; i--)
                {
                    var index = displays[i].DisplayIndex;
                    ParsecVDD.RemoveDisplay(index);
                }

                if (Config.DisplayCount >= 0)
                    Config.DisplayCount = displays.Count;
            }

            VddTimer.Tick -= VddTimer_Tick;
            VddTimer.Stop();

            App.Current?.Dispatcher.Invoke(App.Current.Shutdown);
            AppThread.Join();

            TrayIcon.Visible = false;
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                VddTimer.Dispose();
                TrayIcon.Dispose();
            }

            base.Dispose(disposing);
        }

        public void Invoke(Action action)
        {
            TrayIcon.ContextMenuStrip.Invoke(action);
        }
    }
}