using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace ParsecVDisplay
{
    /// <summary>
    /// Lightweight diagnostic logger. Appends to &lt;exe-dir&gt;\debug.log on each
    /// launch (with a separator/header), and mirrors output to the attached
    /// console (CLI / debugger). Thread-safe via a single file lock; both
    /// destinations no-op silently on failure.
    /// </summary>
    internal static class Log
    {
        static readonly object FileLock = new object();
        static readonly string LogPath;

        static Log()
        {
            try
            {
                var exe = Assembly.GetExecutingAssembly().Location;
                var dir = Path.GetDirectoryName(exe) ?? Environment.CurrentDirectory;
                LogPath = Path.Combine(dir, "debug.log");

                var sep = new string('=', 70);
                var header =
                    sep + Environment.NewLine +
                    $"{Program.AppName} v{Program.AppVersion} | pid={Process.GetCurrentProcess().Id} | {DateTime.Now:yyyy-MM-dd HH:mm:ss}" + Environment.NewLine +
                    sep + Environment.NewLine;
                File.AppendAllText(LogPath, header);
            }
            catch
            {
                LogPath = null;
            }
        }

        public static void Info (string msg) => Write("INF", msg, false);
        public static void Debug(string msg) => Write("DBG", msg, false);
        public static void Warn (string msg) => Write("WRN", msg, true);
        public static void Error(string msg) => Write("ERR", msg, true);

        public static void Info (string fmt, params object[] args) => Write("INF", Fmt(fmt, args), false);
        public static void Debug(string fmt, params object[] args) => Write("DBG", Fmt(fmt, args), false);
        public static void Warn (string fmt, params object[] args) => Write("WRN", Fmt(fmt, args), true);
        public static void Error(string fmt, params object[] args) => Write("ERR", Fmt(fmt, args), true);

        static string Fmt(string fmt, object[] args)
        {
            if (args == null || args.Length == 0) return fmt;
            try { return string.Format(fmt, args); }
            catch { return fmt; }
        }

        static void Write(string level, string message, bool toStderr)
        {
            var line = $"[{DateTime.Now:HH:mm:ss.fff}] {level} {message}";

            // Console — silent no-op for GUI processes with no attached console.
            try
            {
                ConsoleColor? color = null;
                switch (level)
                {
                    case "DBG": color = ConsoleColor.DarkGray; break;
                    case "WRN": color = ConsoleColor.Yellow;   break;
                    case "ERR": color = ConsoleColor.Red;      break;
                }
                var prev = color.HasValue ? Console.ForegroundColor : ConsoleColor.Gray;
                if (color.HasValue) Console.ForegroundColor = color.Value;
                if (toStderr) Console.Error.WriteLine(line);
                else          Console.Out.WriteLine(line);
                if (color.HasValue) Console.ForegroundColor = prev;
            }
            catch { /* no console attached */ }

            // File
            if (LogPath != null)
            {
                lock (FileLock)
                {
                    try { File.AppendAllText(LogPath, line + Environment.NewLine); }
                    catch { /* disk full, perms, etc — drop silently */ }
                }
            }
        }
    }
}
