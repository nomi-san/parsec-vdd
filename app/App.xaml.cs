using System;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;

namespace ParsecVDisplay
{
    public partial class App : Application
    {
        public static string[] Languages => LanguageDicts.Keys.ToArray();
        static Dictionary<string, ResourceDictionary> LanguageDicts;
        static ResourceDictionary CurrentLanguage;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Disable GPU to prevent flickering when adding display
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            base.OnStartup(e);

            var silent = e.Args.Contains("-silent");
            var window = new MainWindow();

            if (!silent)
            {
                window.Show();
            }
        }

        public static class BamlReader
        {
            public static object Load(Stream stream)
            {
                ParserContext pc = new ParserContext();
                MethodInfo loadBamlMethod = typeof(XamlReader).GetMethod("LoadBaml",
                    BindingFlags.NonPublic | BindingFlags.Static);
                return loadBamlMethod.Invoke(null, new object[] { stream, pc, null, false });
            }
        }

        public static void LoadTranslations()
        {
            LanguageDicts = new Dictionary<string, ResourceDictionary>();

            var assembly = ResourceAssembly;
            var rm = new ResourceManager(assembly.GetName().Name + ".g", assembly);

            try
            {
                var list = rm.GetResourceSet(CultureInfo.CurrentCulture, true, true);
                foreach (DictionaryEntry item in list)
                {
                    if (item.Key is string key
                        && key.StartsWith("languages/"))
                    {
                        try
                        {
                            var source = key
                                .Replace("languages/", "/Languages/")
                                .Replace(".baml", ".xaml");

                            var sri = GetResourceStream(new Uri(source, UriKind.Relative));
                            var resources = (ResourceDictionary)BamlReader.Load(sri.Stream);

                            var name = resources["lang_name"].ToString();
                            LanguageDicts.Add(name, resources);
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
                CurrentLanguage = dict;

                if (Current != null)
                {
                    Current.Dispatcher.Invoke(() =>
                    {
                        Current.Resources.MergedDictionaries.Add(dict);
                    });
                }

                if (saveConfig)
                {
                    Config.Language = name;
                }
            }
        }

        public static string GetTranslation(string key, params object[] args)
        {
            if (CurrentLanguage != null)
            {
                if (CurrentLanguage.Contains(key))
                {
                    var t = CurrentLanguage[key]
                        .ToString()
                        .Replace("\\n", "\n");
                    return string.Format(t, args);
                }
            }

            return string.Format("{{{{{0}}}}}", key);
        }
    }
}