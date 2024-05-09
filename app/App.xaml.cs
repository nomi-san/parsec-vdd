using System;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Linq;
using System.Windows.Interop;
using System.Windows.Media;
using System.Reflection;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.Resources;

namespace ParsecVDisplay
{
    public partial class App : Application
    {
        const string ID = "QpHOX8IBUHBznGtqk9xm1";
        public const string NAME = "ParsecVDisplay";
        public const string VERSION = "0.45.2";
        public const string GITHUB_REPO = "nomi-san/parsec-vdd";

        public static bool Silent { get; private set; }
        public static string[] Languages => LanguageDicts.Keys.ToArray();
        static Dictionary<string, ResourceDictionary> LanguageDicts;

        protected override void OnStartup(StartupEventArgs e)
        {
            if (e.Args.Length >= 2 && e.Args[0] == "-custom")
            {
                var modes = Display.ParseModes(e.Args[1]);
                ParsecVDD.SetCustomDisplayModes(modes);

                if (e.Args.Length >= 3)
                {
                    if (Enum.TryParse<ParsecVDD.ParentGPU>(e.Args[2], true, out var kind))
                    {
                        ParsecVDD.SetParentGPU(kind);
                    }
                }

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

            base.OnStartup(e);
            LoadLanguages();

            var status = ParsecVDD.QueryStatus();
            if (status != Device.Status.OK)
            {
                if (status == Device.Status.RESTART_REQUIRED)
                {
                    MessageBox.Show(GetTranslation("t_msg_must_restart_pc"),
                        NAME, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else if (status == Device.Status.DISABLED)
                {
                    MessageBox.Show(GetTranslation("t_msg_driver_is_disabled", ParsecVDD.ADAPTER),
                        NAME, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else if (status == Device.Status.NOT_INSTALLED)
                {
                    MessageBox.Show(GetTranslation("t_msg_please_install_driver"),
                        NAME, MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(GetTranslation("t_msg_driver_status_not_ok", status),
                        NAME, MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                Shutdown();
                return;
            }

            if (ParsecVDD.Init() == false)
            {
                MessageBox.Show(GetTranslation("t_msg_failed_to_obtain_handle"),
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

            // Disable GPU to prevent flickering when adding display
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ParsecVDD.Uninit();
            base.OnExit(e);
        }

        static void LoadLanguages()
        {
            LanguageDicts = new Dictionary<string, ResourceDictionary>();

            var assembly = Assembly.GetExecutingAssembly();
            var rm = new ResourceManager(assembly.GetName().Name + ".g", assembly);
            try
            {
                var list = rm.GetResourceSet(CultureInfo.CurrentCulture, true, true);
                foreach (DictionaryEntry item in list)
                {
                    if (item.Key is string key
                        && key.StartsWith("languages/"))
                    {
                        var source = key
                            .Replace("languages/", "Languages/")
                            .Replace(".baml", ".xaml");

                        try
                        {
                            var dict = new ResourceDictionary();
                            dict.Source = new Uri(source, UriKind.Relative);

                            var name = dict["lang_name"].ToString();
                            LanguageDicts.Add(name, dict);
                        }
                        catch
                        {
                        }
                    }
                }
            }
            finally
            {
                rm.ReleaseAllResources();
            }

            SetLanguage(Config.Language, saveConfig: false);
        }

        public static void SetLanguage(string name, bool saveConfig = true)
        {
            if (LanguageDicts.TryGetValue(name, out var dict))
            {
                Current.Resources.MergedDictionaries.Add(dict);

                if (saveConfig)
                {
                    Config.Language = name;
                }
            }
        }

        public static string GetTranslation(string key, params object[] args)
        {
            try
            {
                var translation = Current.FindResource(key);
                if (translation != null && translation is string t)
                {
                    t = t.Replace("\\n", "\n");
                    return string.Format(t, args);
                }
            }
            catch
            {
            }

            return string.Format("{{{{{0}}}}}", key);
        }
    }
}