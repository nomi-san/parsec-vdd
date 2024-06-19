using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace ParsecVDisplay
{
    internal static class CLI
    {
        static StreamWriter _stdOutWriter;

        public static int Execute(string[] args)
        {
            AttachConsole(-1);

            if (args.Length > 0)
            {
                try
                {
                    switch (args[0])
                    {
                        case "add":
                            return AddDisplay();
                        case "remove":
                            return RemoveDisplay(args);
                        case "list":
                            return ListDisplay();
                        case "set":
                            return SetDisplayMode(args);
                        case "status":
                            return QueryDriverStatus();
                        case "version":
                            return QueryDriverVersion();
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
                    return -1;
                }
                finally
                {
                    ParsecVDD.Uninit();
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
            var status = ParsecVDD.QueryStatus();

            if (status == Device.Status.NOT_INSTALLED)
            {
                throw new Exception("The driver is not found, please install it first");
            }
            else if (status != Device.Status.OK)
            {
                throw new Exception($"The driver is not OK, got status {status}");
            }

            if (!ParsecVDD.Init())
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
            var displays = ParsecVDD.GetDisplays();
            if (displays.Count >= ParsecVDD.MAX_DISPLAYS)
            {
                throw new Exception(string.Format("Exceeded limit ({0}), could not add more displays", ParsecVDD.MAX_DISPLAYS));
            }

            PrepareVdd();
            CheckAppRunning();

            if (ParsecVDD.AddDisplay(out int index))
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
            if (args.Length < 2)
                throw new Exception("Missing display index.");

            var arg1 = args[1];
            bool removeAll = arg1 == "all" || arg1 == "*";
            int index = -1;

            if (removeAll || int.TryParse(arg1, out index))
            {
                var displays = ParsecVDD.GetDisplays();

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
                        if (!ParsecVDD.RemoveDisplay(di.DisplayIndex))
                            throw new Exception(string.Format("Failed to remove the display at index {0}.", index));
                    }
                    return 0;
                }
                else
                {
                    var display = index == -1 ? displays.LastOrDefault()
                        : displays.FirstOrDefault(di => di.DisplayIndex == index);
                    if (display != null)
                    {
                        PrepareVdd();
                        if (!ParsecVDD.RemoveDisplay(index))
                            throw new Exception(string.Format("Failed to remove the display at index {0}.", index));
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
            var displays = ParsecVDD.GetDisplays();

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
                Console.WriteLine("No Parsec Display available.");
            }

            return 0;
        }

        static int SetDisplayMode(string[] args)
        {
            // todo
            throw new NotImplementedException();
        }

        static int QueryDriverStatus()
        {
            var status = ParsecVDD.QueryStatus();
            Console.WriteLine("The driver status is {0}", status);

            return (int)status;
        }

        static int QueryDriverVersion()
        {
            if (ParsecVDD.QueryVersion(out var version))
            {
                Console.WriteLine("{0} - v{1}", ParsecVDD.ADAPTER, version);
                return 0;
            }
            else
            {
                throw new Exception("Failed to query the driver version.");
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("vdd-cli [command] [args...]");
            Console.WriteLine("  add             - Add a virtual display");
            Console.WriteLine("  remove X        - Remove the virtual display at index X (number)");
            Console.WriteLine("         -1       - Remove the last added virtual display");
            Console.WriteLine("         all      - Remove all the added virtual displays");
            Console.WriteLine("  list            - Show all the added virtual displays and specs");
            Console.WriteLine("  set    X WxH    - Set resolution for a virtual display");
            Console.WriteLine("                    where X is index number, WxH is size, e.g 1920x1080");
            Console.WriteLine("         X @R     - Set only the refresh rate R, e.g @60, @120 (hz)");
            Console.WriteLine("         X WxH@R  - Set full display mode as above, e.g 1920x1080@144");
            Console.WriteLine("  status          - Query the driver status");
            Console.WriteLine("  version         - Query the driver version");
            Console.WriteLine("  help            - Show this help");
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AttachConsole(int dwProcessId);
    }
}