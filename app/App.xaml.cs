using System;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Linq;
using System.Windows.Interop;
using System.Windows.Media;

namespace ParsecVDisplay
{
    public partial class App : Application
    {
        const string ID = "QpHOX8IBUHBznGtqk9xm1";
        public const string NAME = "ParsecVDisplay";
        public const string VERSION = "0.45.0";

        public static bool Silent { get; private set; }

        static App()
        {
            var displays = Display.GetAllDisplays();
            return;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Length >= 2 && e.Args[0] == "-custom")
            {
                var modes = Display.ParseModes(e.Args[1]);
                ParsecVDD.SetCustomDisplayModes(modes);

                Shutdown();
                return;
            }

            Silent = e.Args.Contains("-silent");

            var signal = new EventWaitHandle(false,
                EventResetMode.AutoReset, ID, out var isOwned);

            if (!isOwned)
            {
                signal.Set();
                Shutdown();
                return;
            }

            var status = ParsecVDD.QueryStatus();
            if (status != Device.Status.OK)
            {
                if (status == Device.Status.RESTART_REQUIRED)
                {
                    MessageBox.Show("You must restart your PC to complete the driver setup.",
                        NAME, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else if (status == Device.Status.DISABLED)
                {
                    MessageBox.Show($"{ParsecVDD.ADAPTER} is disabled, please enable it.",
                        NAME, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else if (status == Device.Status.NOT_INSTALLED)
                {
                    MessageBox.Show("Please install the driver first.",
                        NAME, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show($"The driver is not OK, please check again. Current status: {status}.",
                        NAME, MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                Shutdown();
                return;
            }

            if (ParsecVDD.Init() == false)
            {
                MessageBox.Show("Failed to obtain the device handle, please check the driver installation again.",
                    NAME, MessageBoxButton.OK, MessageBoxImage.Warning);

                Shutdown();
                return;
            }

            Task.Run(() =>
            {
                while (signal.WaitOne())
                {
                    Tray.ShowApp();
                }
            });

            base.OnStartup(e);

            // Disable GPU to prevent flickering when adding display
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ParsecVDD.Uninit();
            base.OnExit(e);
        }
    }
}