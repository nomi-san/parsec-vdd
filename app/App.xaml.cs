using System;
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
        public static bool Silent { get; private set; }

        public static string[] Languages => LanguageDicts.Keys.ToArray();
        static Dictionary<string, ResourceDictionary> LanguageDicts;

        protected override void OnStartup(StartupEventArgs e)
        {
            Silent = e.Args.Contains("-silent");

            base.OnStartup(e);
            LoadLanguages();

            string error = null;
            var status = ParsecVDD.QueryStatus();

            if (!(status == Device.Status.OK || Config.SkipDriverCheck))
            {
                switch (status)
                {
                    case Device.Status.RESTART_REQUIRED:
                        error = GetTranslation("t_msg_must_restart_pc");
                        break;
                    case Device.Status.DISABLED:
                        error =GetTranslation("t_msg_driver_is_disabled", ParsecVDD.ADAPTER);
                        break;
                    case Device.Status.NOT_INSTALLED:
                        error = GetTranslation("t_msg_please_install_driver");
                        break;
                    default:
                        error = GetTranslation("t_msg_driver_status_not_ok", status);
                        break;
                }
            }
            else if (ParsecVDD.Init() != true)
            {
                error = GetTranslation("t_msg_failed_to_obtain_handle");
            }

            if (error != null)
            {
                MessageBox.Show(error, Program.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
            }

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