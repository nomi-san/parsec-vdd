using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Navigation;

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
            xAppName.Content += $" v{Program.AppVersion}";

            // prevent frame history
            xFrame.Navigating += (_, e) => e.Cancel = e.NavigationMode != NavigationMode.New;
            xFrame.Navigated += (_, e) => xFrame.NavigationService.RemoveBackEntry();

            xDisplays.Children.Clear();
            xNoDisplay.Visibility = Visibility.Hidden;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            Handle = new WindowInteropHelper(this).EnsureHandle();
            Helper.EnableDropShadow(Handle);
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

            var defaultLang = Config.Language;
            foreach (var item in App.Languages)
            {
                var mi = new MenuItem
                {
                    Header = item,
                    IsCheckable = true,
                    IsChecked = item == defaultLang
                };

                mi.Click += delegate
                {
                    foreach (MenuItem item2 in xLanguageMenu.Items)
                        item2.IsChecked = false;

                    mi.IsChecked = true;
                    App.SetLanguage(mi.Header.ToString());
                    Tray.Instance?.Invoke(Tray.Instance.UpdateContent);
                };

                xLanguageMenu.Items.Add(mi);
            }

            SystemEvents.DisplaySettingsChanged += DisplayChanged;
            DisplayChanged(null, EventArgs.Empty);
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        }

        private void DisplayChanged(object sender, EventArgs  e)
        {
            Dispatcher.Invoke(() =>
            {
                var displays = ParsecVDD.GetDisplays(out bool noMonitors);

                xDisplays.Children.Clear();
                xNoDisplay.Visibility = displays.Count <= 0 ? Visibility.Visible : Visibility.Hidden;

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
        }

        private void QueryStatus(object sender, EventArgs e)
        {
            if (e is MouseEventArgs mbe)
                mbe.Handled = true;

            Tray.Instance.Invoke(() => Tray.Instance.QueryDriver(null, null));
        }

        private void OpenRepoLink(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Helper.OpenLink($"https://github.com/{Program.GitHubRepo}");
        }

        private void LanguageText_MouseEvent(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (e.LeftButton == MouseButtonState.Released)
                (sender as TextBlock).ContextMenu.IsOpen = true;
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
    }
}