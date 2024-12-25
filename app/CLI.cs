using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace ParsecVDisplay
{
    internal static class CLI
    {
        static IntPtr VddHandle = IntPtr.Zero;

        static void ShowHelp()
        {
            Console.WriteLine("vdd command [args...]");
            Console.WriteLine("    -a|add           - Add a virtual display");
            Console.WriteLine("    -r|remove        - Remove the last added virtual display");
            Console.WriteLine("            X        - Remove the virtual display at index X (number)");
            Console.WriteLine("            all      - Remove all the added virtual displays");
            Console.WriteLine("    -l|list          - Show all the added virtual displays and specs");
            Console.WriteLine("    -s|set  X WxH    - Set resolution for a virtual display");
            Console.WriteLine("                        where X is index number, WxH is size, e.g 1920x1080");
            Console.WriteLine("            X @R     - Set only the refresh rate R, e.g @60, @120 (hz)");
            Console.WriteLine("                        on Powershell, you should replace '@' with 'r'");
            Console.WriteLine("            X WxH@R  - Set full display mode as above, e.g 1920x1080@144");
            Console.WriteLine("    -v|version       - Query driver version and status");
            Console.WriteLine("    -h|help          - Show this help");
        }

        public static int Execute(string[] args)
        {
            AttachConsole(-1);

            if (args.Length > 0)
            {
                try
                {
                    switch (args[0])
                    {
                        case "-a":
                        case "add":
                            return AddDisplay();

                        case "-r":
                        case "remove":
                            return RemoveDisplay(args);

                        case "-l":
                        case "list":
                            return ListDisplay();

                        case "-s":
                        case "set":
                            return SetDisplayMode(args);

                        case "-v":
                        case "version":
                            return QueryVersion();

                        case "-h":
                        case "help":
                            ShowHelp();
                            return 0;

                        default:
                            Console.WriteLine("Invalid command '{0}'", args[0]);
                            ShowHelp();
                            return 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: {0}", ex.Message);
#if DEBUG
                    Console.Error.WriteLine(ex.StackTrace);
#endif
                    return -1;
                }
                finally
                {
                    Vdd.Core.CloseHandle(VddHandle);
                }
            }
            else
            {
                ShowHelp();
                return 0;
            }
        }

        static Device.Status PrepareVdd()
        {
            var status = Vdd.Core.QueryStatus(out var _);

            if (status == Device.Status.NOT_INSTALLED)
            {
                throw new Exception("The driver is not found, please install it first");
            }
            else if (status != Device.Status.OK)
            {
                throw new Exception($"The driver is not OK, got status {status}");
            }

            if (!Vdd.Core.OpenHandle(out VddHandle))
            {
                throw new Exception("Failed to obtain the driver device handle");
            }

            return status;
        }

        static void CheckAppRunning()
        {
            if (!EventWaitHandle.TryOpenExisting(Program.AppId, out var _))
            {
                throw new Exception($"{Program.AppName} app is not running");
            }
        }

        static int AddDisplay()
        {
            var displays = Vdd.Core.GetDisplays();
            int maxCount = Vdd.Core.MAX_DISPLAYS;

            if (displays.Count >= maxCount)
            {
                throw new Exception(string.Format("Exceeded limit ({0}), could not add more displays", maxCount));
            }

            PrepareVdd();
            CheckAppRunning();

            if (Vdd.Core.AddDisplay(VddHandle, out int index))
            {
                Console.WriteLine($"Added a virtual display with index {0}.", index);
                return index;
            }
            else
            {
                throw new Exception("Failed to add a virtual display.");
            }
        }

        static int RemoveDisplay(string[] args)
        {
            var arg1 = args.Length >= 2 ? args[1] : "";
            bool removeAll = arg1 == "all" || arg1 == "*";
            int index = -1;

            if (args.Length == 1 || removeAll || int.TryParse(arg1, out index))
            {
                var displays = Vdd.Core.GetDisplays();

                if (displays.Count == 0)
                {
                    Console.WriteLine("No Parsec Display available.");
                    return 0;
                }
                else if (removeAll)
                {
                    PrepareVdd();
                    foreach (var di in displays)
                    {
                        if (!Vdd.Core.RemoveDisplay(VddHandle, di.DisplayIndex))
                            throw new Exception(string.Format("Failed to remove the display at index {0}.", index));
                    }

                    Console.WriteLine("Removed all added displays.");
                    return 0;
                }
                else
                {
                    var display = index < 0 ? displays.LastOrDefault()
                        : displays.FirstOrDefault(di => di.DisplayIndex == index);

                    if (display != null)
                    {
                        PrepareVdd();
                        if (!Vdd.Core.RemoveDisplay(VddHandle, display.DisplayIndex))
                            throw new Exception(string.Format("Failed to remove the display at index {0}.", display.DisplayIndex));

                        Console.WriteLine("Removed display at index {0}.", display.DisplayIndex);
                        return 0;
                    }
                    else
                    {
                        throw new Exception(string.Format("Display index {0} is not found.", index));
                    }
                }
            }
            else
            {
                throw new Exception(string.Format("Invalid display index '{0}'.", arg1));
            }
        }

        static int ListDisplay()
        {
            var displays = Vdd.Core.GetDisplays();

            if (displays.Count > 0)
            {
                foreach (var di in displays)
                {
                    Console.WriteLine("Index: {0}", di.DisplayIndex);
                    Console.WriteLine("  - Device: {0}", di.DeviceName);
                    Console.WriteLine("  - Number: {0}", di.Identifier);
                    Console.WriteLine("  - Name: {0}", di.DisplayName);
                    Console.WriteLine("  - Mode: {0}", di.CurrentMode);
                    Console.WriteLine("  - Orientation: {0} ({1}°)", di.CurrentOrientation, (int)di.CurrentOrientation * 90);
                }
            }
            else
            {
                Console.WriteLine("No virtual displays present.");
            }

            return displays.Count;
        }

        static int SetDisplayMode(string[] args)
        {
            if (args.Length < 2)
                throw new Exception("Missing display index.");
            if (args.Length < 3)
                throw new Exception("Missing resolution and/or refresh rate.");

            var argIndex = args[1];
            var argDMode = string.Join(" ", args.Skip(2));

            if (int.TryParse(argIndex, out int index))
            {
                var displays = Vdd.Core.GetDisplays();

                if (displays.Count == 0)
                {
                    Console.WriteLine("No Parsec Display available.");
                    return 0;
                }
                else
                {
                    var display = displays.Find(di => di.DisplayIndex == index);

                    if (display == null)
                        throw new Exception(string.Format("Display index {0} is not found.", index));

                    int? width, height, hz;
                    ParseDisplayModeArg(argDMode, out width, out height, out hz);

                    if ((width != null && height != null) || hz != null)
                    {
                        if (display.ChangeMode(width, height, hz, null))
                        {
                            Console.WriteLine($"Display index {index} is set to '{argDMode}'.");
                            return 0;
                        }
                        else
                        {
                            throw new Exception($"Failed to set, display mode '{argDMode}' is not supported.");
                        }
                    }
                    else
                    {
                        throw new Exception("Nothing to do, recheck your syntax.");
                    }
                }
            }
            else
            {
                throw new Exception(string.Format("Invalid display index '{0}'.", argIndex));
            }
        }

        static int QueryVersion()
        {
            var status = Vdd.Core.QueryStatus(out var version);

            Console.WriteLine(Vdd.Core.ADAPTER);
            Console.WriteLine("- Status: {0}", status);
            Console.WriteLine("- Version: {0}.{1}", version.Major, version.Minor);

            return (int)status;
        }

        static void ParseDisplayModeArg(string arg, out int? width, out int? height, out int? hz)
        {
            Match match;
            arg = arg.Trim();

            width = null;
            height = null;
            hz = null;

            const string regexSize = @"^(\d+)\s*[xX]\s*(\d+)$";
            if (Regex.IsMatch(arg, regexSize))
            {
                match = Regex.Match(arg, regexSize);
                width = int.Parse(match.Groups[1].Value);
                height = int.Parse(match.Groups[2].Value);
                return;
            }

            const string regexHz = @"^[r@](\d+)$";
            if (Regex.IsMatch(arg, regexHz))
            {
                match = Regex.Match(arg, regexHz);
                hz = int.Parse(match.Groups[1].Value);
                return;
            }

            const string regexAll = @"^(\d+)\s*[xX]\s*(\d+)\s*[r@](\d+)$";
            if (Regex.IsMatch(arg, regexAll))
            {
                match = Regex.Match(arg, regexAll);
                width = int.Parse(match.Groups[1].Value);
                height = int.Parse(match.Groups[2].Value);
                hz = int.Parse(match.Groups[3].Value);
                return;
            }
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AttachConsole(int dwProcessId);
    }
}