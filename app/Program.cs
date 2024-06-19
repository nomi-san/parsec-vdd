using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ParsecVDisplay
{
    public static class Program
    {
        public const string AppId = "QpHOX8IBUHBznGtqk9xm1";
        public const string AppName = "ParsecVDisplay";
        public const string AppVersion = "0.45.2";
        public const string GitHubRepo = "nomi-san/parsec-vdd";

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "-custom")
            {
                var modes = Display.ParseModes(args[1]);
                ParsecVDD.SetCustomDisplayModes(modes);

                if (args.Length >= 3)
                {
                    if (Enum.TryParse<ParsecVDD.ParentGPU>(args[2], true, out var kind))
                    {
                        ParsecVDD.SetParentGPU(kind);
                    }
                }

                return 0;
            }

            if (args.Length > 0 && args[0] == "-cli")
            {
                args = args.Skip(1).ToArray();
                return CLI.Execute(args);
            }

            if (SingleInstance())
            {
                App.LoadTranslations();

                if (InitDriver())
                {
                    Helper.StayAwake(false);
                    Application.Run(new Tray());
                    ParsecVDD.Uninit();
                }
            }

            return 0;
        }

        static bool SingleInstance()
        {
            bool isOwned = false;
            var signal = new EventWaitHandle(false,
                EventResetMode.AutoReset, AppId, out isOwned);

            if (isOwned)
            {
                Task.Run(() =>
                {
                    while (signal.WaitOne())
                    {
                        Tray.Instance?.Invoke(Tray.Instance.ShowApp);
                    }
                });
            }
            else
            {
                signal.Set();
                signal.Dispose();
            }

            return isOwned;
        }

        static bool InitDriver()
        {
            string error = null;
            var status = ParsecVDD.QueryStatus();

            if (!(status == Device.Status.OK || Config.SkipDriverCheck))
            {
                switch (status)
                {
                    case Device.Status.RESTART_REQUIRED:
                        error = App.GetTranslation("t_msg_must_restart_pc");
                        break;
                    case Device.Status.DISABLED:
                        error = App.GetTranslation("t_msg_driver_is_disabled", ParsecVDD.ADAPTER);
                        break;
                    case Device.Status.NOT_INSTALLED:
                        error = App.GetTranslation("t_msg_please_install_driver");
                        break;
                    default:
                        error = App.GetTranslation("t_msg_driver_status_not_ok", status);
                        break;
                }
            }
            else if (ParsecVDD.Init() != true)
            {
                error = App.GetTranslation("t_msg_failed_to_obtain_handle");
            }

            if (error != null)
            {
                MessageBox.Show(error, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return error == null;
        }
    }
}