using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Navigation;
using Microsoft.Win32;

namespace ParsecVDisplay
{
    public partial class MainWindow : Window
    {
        public static IntPtr Handle { get; private set; }
        public static MainWindow Instance { get; private set; }

        public static bool IsMenuOpen;

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();

            // prevent frame history
            xFrame.Navigating += (_, e) => e.Cancel = e.NavigationMode != NavigationMode.New;
            xFrame.Navigated += (_, e) => xFrame.NavigationService.RemoveBackEntry();

            xDisplays.Children.Clear();
            xNoDisplay.Visibility = Visibility.Hidden;

            this.Title = Program.AppName;
            this.IsVisibleChanged += delegate { UpdateDriverLabel(); };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            Handle = new WindowInteropHelper(this).EnsureHandle();
            Helper.EnableDropShadow(Handle);

            var source = HwndSource.FromHwnd(Handle);
            source.AddHook(new HwndSourceHook(WndProc));
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;

            if (xFrame.Content != null)
            {
                xFrame.Visibility = Visibility.Hidden;
                xFrame.Content = null;
                xDisplays.Visibility = Visibility.Visible;
                xButtons.Visibility = Visibility.Visible;
            }
            else
            {
                this.Hide();
            }
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= Window_Loaded;

            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            DisplayChanged(null, EventArgs.Empty);

            UpdateDriverLabel();
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        }

        private void UpdateDriverLabel()
        {
            Vdd.Controller.QueryStatus(out var ver);
            xDriver.Content = $"{Vdd.Core.NAME} v{ver.Major}.{ver.Minor}";
        }

        private void DisplayChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var displays = Vdd.Core.GetDisplays(out bool noMonitors);

                xDisplays.Children.Clear();
                xNoDisplay.Visibility = displays.Count > 0
                    ? Visibility.Hidden : Visibility.Visible;

                foreach (var display in displays)
                {
                    var item = new Components.DisplayItem(display);
                    xDisplays.Children.Add(item);
                }

                xAdd.IsEnabled = true;

                if (noMonitors && Config.FallbackDisplay)
                {
                    AddDisplay(null, EventArgs.Empty);
                }
            });
        }

        private void AddDisplay(object sender, EventArgs e)
        {
            Tray.Instance.Invoke(() => Tray.Instance.AddDisplay(null, null));
        }

        private void OpenCustom(object sender, EventArgs e)
        {
            xDisplays.Visibility = Visibility.Hidden;
            xButtons.Visibility = Visibility.Hidden;
            xFrame.Content = new Components.CustomPage();
            xFrame.Visibility = Visibility.Visible;
        }

        private void OpenDisplaySettings(object sender, EventArgs e)
        {
            Helper.ShellExec("ms-settings:display");
        }

        private void SyncSettings(object sender, EventArgs e)
        {
            xAdd.IsEnabled = false;
            xDisplays.Children.Clear();

            DisplayChanged(null, null);
            UpdateDriverLabel();
        }

        private void QueryStatus(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            UpdateDriverLabel();
            Tray.Instance.Invoke(() => Tray.Instance.QueryDriver(null, null));
        }

        private void OpenRepoLink(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Helper.OpenLink($"https://github.com/{Program.GitHubRepo}");
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.IsRepeat &&
                (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
            {
                var screen = System.Windows.Forms.Screen.FromHandle(Handle);
                if (screen != null)
                {
                    var screens = System.Windows.Forms.Screen.AllScreens;

                    int index = -1, nextIndex;
                    for (int i = 0; i < screens.Length; i++)
                        if (screens[i].Bounds.Contains(screen.Bounds))
                            index = i;

                    if (index != -1)
                    {
                        if (e.Key == Key.Left)
                            nextIndex = index - 1;
                        else if (e.Key == Key.Right)
                            nextIndex = index + 1;
                        else return;

                        if (nextIndex >= screens.Length) nextIndex = 0;
                        else if (nextIndex < 0) nextIndex = screens.Length - 1;

                        if (index != nextIndex)
                        {
                            var wa = screens[nextIndex].WorkingArea;
                            Left = wa.Location.X + (wa.Width - RenderSize.Width) / 2;
                            Top = wa.Location.Y + (wa.Height - RenderSize.Height) / 2;
                        }
                    }
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0219 && unchecked((int)wParam) == 0x7)
            {
                DisplayChanged(this, EventArgs.Empty);
            }

            return IntPtr.Zero;
        }
    }
}