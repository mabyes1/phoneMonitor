using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using PhoneMonitor.Host.Windows;

namespace PhoneMonitor.Host.Display
{
    public sealed class DisplayModeController
    {
        private readonly DisplayCatalog displays;

        public DisplayModeController(DisplayCatalog displays)
        {
            this.displays = displays;
        }

        public IReadOnlyList<DisplayModePreset> GetPresets()
        {
            return new[]
            {
                new DisplayModePreset { Id = "iphonexs-css-812x375", Label = "iPhone XS exact 812x375", Width = 812, Height = 375, RefreshRate = 60, Kind = "iphone-xs" },
                new DisplayModePreset { Id = "iphonexs-large-974x450", Label = "iPhone XS large 974x450", Width = 974, Height = 450, RefreshRate = 60, Kind = "iphone-xs" },
                new DisplayModePreset { Id = "iphonexs-comfy-1082x500", Label = "iPhone XS comfy 1082x500", Width = 1082, Height = 500, RefreshRate = 60, Kind = "iphone-xs" },
                new DisplayModePreset { Id = "iphonexs-sharp-1218x562", Label = "iPhone XS sharp 1218x562", Width = 1218, Height = 562, RefreshRate = 60, Kind = "iphone-xs" },
                new DisplayModePreset { Id = "galaxys23-large-1170x540", Label = "Galaxy S23 large 1170x540", Width = 1170, Height = 540, RefreshRate = 60, Kind = "galaxy-s23" },
                new DisplayModePreset { Id = "galaxys23-comfy-1404x648", Label = "Galaxy S23 comfy 1404x648", Width = 1404, Height = 648, RefreshRate = 60, Kind = "galaxy-s23" },
                new DisplayModePreset { Id = "galaxys23-sharp-1560x720", Label = "Galaxy S23 sharp 1560x720", Width = 1560, Height = 720, RefreshRate = 60, Kind = "galaxy-s23" },
                new DisplayModePreset { Id = "htcuu-huge-854x480", Label = "HTC UU huge 854x480", Width = 854, Height = 480, RefreshRate = 60, Kind = "htc-u-ultra" },
                new DisplayModePreset { Id = "htcuu-large-960x540", Label = "HTC UU large 960x540", Width = 960, Height = 540, RefreshRate = 60, Kind = "htc-u-ultra" },
                new DisplayModePreset { Id = "htcuu-comfy-1024x576", Label = "HTC UU comfy 1024x576", Width = 1024, Height = 576, RefreshRate = 60, Kind = "htc-u-ultra" },
                new DisplayModePreset { Id = "htcuu-balanced-1152x648", Label = "HTC UU balanced 1152x648", Width = 1152, Height = 648, RefreshRate = 60, Kind = "htc-u-ultra" },
                new DisplayModePreset { Id = "htcuu-sharp-1280x720", Label = "HTC UU sharp 1280x720", Width = 1280, Height = 720, RefreshRate = 60, Kind = "htc-u-ultra" },
                new DisplayModePreset { Id = "standard-1366x768", Label = "Standard 1366x768", Width = 1366, Height = 768, RefreshRate = 60, Kind = "standard" },
                new DisplayModePreset { Id = "standard-1600x900", Label = "Standard 1600x900", Width = 1600, Height = 900, RefreshRate = 60, Kind = "standard" },
                new DisplayModePreset { Id = "standard-1920x1080", Label = "Standard 1920x1080", Width = 1920, Height = 1080, RefreshRate = 60, Kind = "standard" },
                new DisplayModePreset { Id = "portrait-1440x2560", Label = "HTC UU portrait 1440x2560", Width = 1440, Height = 2560, RefreshRate = 60, Kind = "phone-portrait" },
                new DisplayModePreset { Id = "portrait-1080x1920", Label = "Portrait 1080x1920", Width = 1080, Height = 1920, RefreshRate = 60, Kind = "phone-portrait" },
            };
        }

        public ApplyDisplayModeResult Apply(int width, int height, int refreshRate)
        {
            var display = FindPhoneDisplay();
            if (display == null)
            {
                return ApplyDisplayModeResult.Failed("PhoneMonitor display was not found.");
            }

            var devMode = new DevMode
            {
                Size = (ushort)Marshal.SizeOf(typeof(DevMode)),
                Fields = 0x00080000 | 0x00100000 | 0x00400000,
                PelsWidth = (uint)width,
                PelsHeight = (uint)height,
                DisplayFrequency = (uint)refreshRate
            };

            const uint cdsUpdateRegistry = 0x00000001;
            var result = NativeMethods.ChangeDisplaySettingsEx(display.DeviceName, ref devMode, IntPtr.Zero, cdsUpdateRegistry, IntPtr.Zero);

            return new ApplyDisplayModeResult
            {
                Success = result == 0,
                ResultCode = result,
                DeviceName = display.DeviceName,
                Width = width,
                Height = height,
                RefreshRate = refreshRate,
                Message = result == 0
                    ? $"Applied {width}x{height}@{refreshRate} to {display.DeviceName}."
                    : $"Windows rejected {width}x{height}@{refreshRate} for {display.DeviceName}. Result code: {result}."
            };
        }

        private DisplayInfo FindPhoneDisplay()
        {
            foreach (var display in displays.GetDisplays())
            {
                if (display.IsPhoneMonitor)
                {
                    return display;
                }
            }

            return null;
        }
    }

    public sealed class ApplyDisplayModeResult
    {
        public bool Success { get; set; }
        public int ResultCode { get; set; }
        public string DeviceName { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int RefreshRate { get; set; }
        public string Message { get; set; }

        public static ApplyDisplayModeResult Failed(string message)
        {
            return new ApplyDisplayModeResult
            {
                Success = false,
                ResultCode = int.MinValue,
                Message = message
            };
        }
    }
}
