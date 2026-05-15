using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace ParsecDisplay.Vdd
{
    internal static class Utils
    {
        /// <summary>
        /// Get list of custom display modes.
        /// </summary>
        public static IList<Display.Mode> GetCustomDisplayModes()
        {
            var list = new List<Display.Mode>();

            using (var vdd = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Parsec\\vdd", RegistryKeyPermissionCheck.ReadSubTree))
            {
                if (vdd != null)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        using (var index = vdd.OpenSubKey($"{i}", RegistryKeyPermissionCheck.ReadSubTree))
                        {
                            if (index != null)
                            {
                                var width = index.GetValue("width");
                                var height = index.GetValue("height");
                                var hz = index.GetValue("hz");

                                if (width != null && height != null && hz != null)
                                {
                                    list.Add(new Display.Mode
                                    {
                                        Width = Convert.ToUInt16(width),
                                        Height = Convert.ToUInt16(height),
                                        Hz = Convert.ToUInt16(hz),
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Set list of custom display modes.
        /// This function requires admin rights.
        /// </summary>
        public static void SetCustomDisplayModes(List<Display.Mode> modes)
        {
            using (var vdd = Registry.LocalMachine.CreateSubKey("SOFTWARE\\Parsec\\vdd", RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (vdd != null)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        using (var index = vdd.CreateSubKey($"{i}", RegistryKeyPermissionCheck.ReadWriteSubTree))
                        {
                            if (i >= modes.Count && index != null)
                            {
                                index.Dispose();
                                vdd.DeleteSubKey($"{i}");
                            }
                            else if (index != null)
                            {
                                index.SetValue("width", modes[i].Width, RegistryValueKind.DWord);
                                index.SetValue("height", modes[i].Height, RegistryValueKind.DWord);
                                index.SetValue("hz", modes[i].Hz, RegistryValueKind.DWord);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// DXGI adapter vendor ids for VDD parent GPU selection.
        /// Ref: https://support.parsec.app/hc/en-us/articles/4423615425293-VDD-Advanced-Configuration#parent_gpu
        /// </summary>
        public enum ParentGPU
        {
            Auto   = 0,
            NVIDIA = 0x10DE,
            AMD    = 0x1002,
            Intel  = 0x8086,
        }

        const string PARAMETERS_KEY =
            "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\WUDF\\Services\\ParsecVDA\\Parameters";

        const string VALUE_VENDOR_ID = "PreferredRenderAdapterVendorId";
        const string VALUE_DISABLE_CHANGE = "DisablePreferredRenderAdapterChange";

        /// <summary>
        /// Get parent GPU of VDD.
        /// </summary>
        public static ParentGPU GetParentGPU()
        {
            using (var parameters = Registry.LocalMachine.OpenSubKey(
                PARAMETERS_KEY, RegistryKeyPermissionCheck.ReadSubTree))
            {
                if (parameters != null)
                {
                    object value = parameters.GetValue(VALUE_VENDOR_ID);
                    if (value != null)
                    {
                        return (ParentGPU)Convert.ToInt32(value);
                    }
                }
            }

            return ParentGPU.Auto;
        }

        /// <summary>
        /// Set parent GPU for VDD. Requires admin rights.
        /// The driver ships with <c>DisablePreferredRenderAdapterChange = 1</c> which
        /// blocks any vendor preference, so we toggle that to 0 before writing the
        /// vendor id, and restore it to 1 when switching back to Auto.
        /// </summary>
        public static void SetParentGPU(ParentGPU kind)
        {
            using (var parameters = Registry.LocalMachine.OpenSubKey(
                PARAMETERS_KEY, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (parameters == null)
                    return;

                if (kind == ParentGPU.Auto)
                {
                    parameters.DeleteValue(VALUE_VENDOR_ID, false);
                    parameters.SetValue(VALUE_DISABLE_CHANGE, 1, RegistryValueKind.DWord);
                }
                else
                {
                    parameters.SetValue(VALUE_DISABLE_CHANGE, 0, RegistryValueKind.DWord);
                    parameters.SetValue(VALUE_VENDOR_ID, (uint)kind, RegistryValueKind.DWord);
                }
            }
        }
    }
}