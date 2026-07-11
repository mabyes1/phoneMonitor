using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace PhoneMonitor.Host.Windows
{
    public sealed class DisplayCatalog
    {
        private const uint MonitorInfoPrimary = 1;

        public IReadOnlyList<DisplayInfo> GetDisplays()
        {
            var displays = new List<DisplayInfo>();

            NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, delegate (IntPtr monitor, IntPtr hdc, ref Rect rect, IntPtr data)
            {
                var info = new MonitorInfoEx
                {
                    Size = Marshal.SizeOf(typeof(MonitorInfoEx))
                };

                if (!NativeMethods.GetMonitorInfo(monitor, ref info))
                {
                    return true;
                }

                var device = GetDisplayDevice(info.DeviceName);
                displays.Add(new DisplayInfo
                {
                    OutputIndex = displays.Count,
                    DeviceName = info.DeviceName,
                    FriendlyName = device.DeviceString ?? info.DeviceName,
                    DeviceId = device.DeviceId,
                    Left = info.Monitor.Left,
                    Top = info.Monitor.Top,
                    Width = info.Monitor.Width,
                    Height = info.Monitor.Height,
                    IsPrimary = (info.Flags & MonitorInfoPrimary) == MonitorInfoPrimary,
                    IsPhoneMonitor = IsPhoneMonitor(device)
                });

                return true;
            }, IntPtr.Zero);

            return displays
                .OrderByDescending(d => d.IsPhoneMonitor)
                .ThenBy(d => d.Left)
                .ThenBy(d => d.Top)
                .ToList();
        }

        private static DisplayDevice GetDisplayDevice(string deviceName)
        {
            var device = new DisplayDevice
            {
                Size = Marshal.SizeOf(typeof(DisplayDevice))
            };

            NativeMethods.EnumDisplayDevices(deviceName, 0, ref device, 0);
            return device;
        }

        private static bool IsPhoneMonitor(DisplayDevice device)
        {
            return ContainsPhoneMonitor(device.DeviceString)
                || ContainsPhoneMonitor(device.DeviceId)
                || ContainsPhoneMonitor(device.DeviceKey)
                || ContainsMicrosoftSampleMonitor(device.DeviceString)
                || ContainsMicrosoftSampleMonitor(device.DeviceId);
        }

        private static bool ContainsPhoneMonitor(string value)
        {
            return value != null && value.IndexOf("PhoneMonitor", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsMicrosoftSampleMonitor(string value)
        {
            if (value == null)
            {
                return false;
            }

            return value.IndexOf("DELD0E6", StringComparison.OrdinalIgnoreCase) >= 0
                || value.IndexOf("S2719DGF", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public sealed class DisplayInfo
    {
        public string DeviceName { get; set; }
        public string FriendlyName { get; set; }
        public string DeviceId { get; set; }
        public int OutputIndex { get; set; }
        public int Left { get; set; }
        public int Top { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsPhoneMonitor { get; set; }
    }
}
