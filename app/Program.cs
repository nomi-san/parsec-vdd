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
        public const string AppVersion = "1.0.2";
        public const string GitHubRepo = "nomi-san/parsec-vdd";

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "-custom")
            {
                var modes = Display.ParseModes(args[1]);
                Vdd.Utils.SetCustomDisplayModes(modes);

                if (args.Length >= 3)
                {
                    if (Enum.TryParse<Vdd.Utils.ParentGPU>(args[2], true, out var kind))
                    {
                        Vdd.Utils.SetParentGPU(kind);
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
                Helper.StayAwake(false);

                Application.Run(new Tray());
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
    }
}