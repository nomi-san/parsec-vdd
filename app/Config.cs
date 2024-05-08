using System;
using System.Reflection;
using Microsoft.Win32;

namespace ParsecVDisplay
{
    internal static class Config
    {
        static string REG_PATH => $"HKEY_CURRENT_USER\\SOFTWARE\\{App.NAME}";
        static string REG_STARTUP_PATH => @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        public static string Language
        {
            get => GetString(nameof(Language), "English");
            set => SetString(nameof(Language), value);
        }

        public static int DisplayCount
        {
            get => GetInt(nameof(DisplayCount));
            set => SetInt(nameof(DisplayCount), value);
        }

        public static bool FallbackDisplay
        {
            get => GetInt(nameof(FallbackDisplay)) != 0;
            set => SetInt(nameof(FallbackDisplay), value ? 1 : 0);
        }

        public static bool KeepScreenOn
        {
            get
            {
                bool enable = GetInt(nameof(KeepScreenOn)) != 0;
                Helper.StayAwake(enable);
                return enable;
            }
            set
            {
                SetInt(nameof(KeepScreenOn), value ? 1 : 0);
                Helper.StayAwake(value);
            }
        }

        static string GetString(string key, string @default)
        {
            var value = Registry.GetValue(REG_PATH, key, null);
            return value == null ? @default : Convert.ToString(value);
        }

        static void SetString(string key, string value)
        {
            Registry.SetValue(REG_PATH, key, value, RegistryValueKind.String);
        }

        static int GetInt(string key, int @default = 0)
        {
            var value = Registry.GetValue(REG_PATH, key, null);
            return value == null ? @default : Convert.ToInt32(value);
        }

        static void SetInt(string key, int value)
        {
            Registry.SetValue(REG_PATH, key, value, RegistryValueKind.DWord);
        }

        public static bool RunOnStartup
        {
            get
            {
                using (var key = Registry.CurrentUser.OpenSubKey(REG_STARTUP_PATH, false))
                {
                    return key.GetValue(App.NAME) != null;
                }
            }
            set
            {
                using (var key = Registry.CurrentUser.OpenSubKey(REG_STARTUP_PATH, true))
                {
                    if (value)
                    {
                        var exePath = Assembly.GetExecutingAssembly().Location;
                        key.SetValue(App.NAME, $"\"{exePath}\" -silent", RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue(App.NAME, false);
                    }
                }
            }
        }
    }
}