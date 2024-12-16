using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace ParsecVDisplay.Vdd
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
        /// Ref: https://support.parsec.app/hc/en-us/articles/4423615425293-VDD-Advanced-Configuration#parent_gpu
        /// </summary>
        public enum ParentGPU
        {
            Auto = 0,
            NVIDIA = 0x10DE,
            AMD = 0x1002,
        }

        /// <summary>
        /// Get parent GPU of VDD.
        /// </summary>
        public static ParentGPU GetParentGPU()
        {
            using (var parameters = Registry.LocalMachine.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\WUDF\\Services\\ParsecVDA\\Parameters",
                RegistryKeyPermissionCheck.ReadSubTree))
            {
                if (parameters != null)
                {
                    object value = parameters.GetValue("PreferredRenderAdapterVendorId");
                    if (value != null)
                    {
                        return (ParentGPU)Convert.ToInt32(value);
                    }
                }
            }

            return ParentGPU.Auto;
        }

        /// <summary>
        /// Set parent GPU for VDD.
        /// This function requires admin rights.
        /// </summary>
        public static void SetParentGPU(ParentGPU kind)
        {
            using (var parameters = Registry.LocalMachine.OpenSubKey(
                "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\WUDF\\Services\\ParsecVDA\\Parameters",
                RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (parameters != null)
                {
                    if (kind == ParentGPU.Auto)
                    {
                        parameters.DeleteValue("PreferredRenderAdapterVendorId", false);
                    }
                    else
                    {
                        parameters.SetValue("PreferredRenderAdapterVendorId",
                            (uint)kind, RegistryValueKind.DWord);
                    }
                }
            }
        }
    }
}