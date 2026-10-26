using System;
using System.Management;
using System.Runtime.Versioning;

namespace NAITSSMS.Infrastructure.Data.Security
{
    /// <summary>
    /// Isolated so FreePlugIn does not JIT-load System.Management on Linux.
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class WindowsWmiHardwareId
    {
        public static string GetBoardSerialNumber()
        {
            var text = string.Empty;
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (ManagementObject item in searcher.Get())
            {
                text = item.GetPropertyValue("SerialNumber")?.ToString() ?? string.Empty;
            }

            return text;
        }

        public static string GetProcessorId()
        {
            var text = string.Empty;
            using var searcher = new ManagementObjectSearcher("Select * FROM Win32_Processor");
            foreach (ManagementObject item in searcher.Get())
            {
                text = item.GetPropertyValue("ProcessorId")?.ToString() ?? string.Empty;
            }

            return text;
        }
    }
}
