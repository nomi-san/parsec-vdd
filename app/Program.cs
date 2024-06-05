using System;
using System.Threading;
using System.Threading.Tasks;

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

            bool isOwned = false;
            var signal = new EventWaitHandle(false,
                EventResetMode.AutoReset, AppId, out isOwned);

            if (isOwned)
            {
                Task.Run(() =>
                {
                    while (signal.WaitOne())
                    {
                        Tray.ShowApp();
                    }
                });

                App.Main();
            }
            else
            {
                signal.Set();
                signal.Dispose();
            }

            return 0;
        }
    }
}