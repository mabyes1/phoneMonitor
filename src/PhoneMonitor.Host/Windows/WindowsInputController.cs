using System;
using System.Linq;

namespace PhoneMonitor.Host.Windows
{
    public sealed class WindowsInputController
    {
        private const uint MouseEventLeftDown = 0x0002;
        private const uint MouseEventLeftUp = 0x0004;
        private const uint MouseEventRightDown = 0x0008;
        private const uint MouseEventRightUp = 0x0010;

        private readonly DisplayCatalog catalog;

        public WindowsInputController(DisplayCatalog catalog)
        {
            this.catalog = catalog;
        }

        public bool Apply(InputEvent inputEvent)
        {
            if (inputEvent == null || string.IsNullOrWhiteSpace(inputEvent.Type))
            {
                return false;
            }

            var display = FindDisplay(inputEvent.DeviceName);
            if (display == null || display.Width <= 0 || display.Height <= 0)
            {
                return false;
            }

            var x = display.Left + ClampToPixel(inputEvent.X, display.Width);
            var y = display.Top + ClampToPixel(inputEvent.Y, display.Height);
            NativeMethods.SetCursorPos(x, y);

            switch (inputEvent.Type.ToLowerInvariant())
            {
                case "pointerdown":
                    NativeMethods.mouse_event(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);
                    return true;
                case "pointerup":
                case "pointercancel":
                    NativeMethods.mouse_event(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
                    return true;
                case "pointermove":
                    return true;
                case "rightclick":
                    NativeMethods.mouse_event(MouseEventRightDown, 0, 0, 0, UIntPtr.Zero);
                    NativeMethods.mouse_event(MouseEventRightUp, 0, 0, 0, UIntPtr.Zero);
                    return true;
                default:
                    return false;
            }
        }

        private DisplayInfo FindDisplay(string deviceName)
        {
            var displays = catalog.GetDisplays();
            return displays.FirstOrDefault(display =>
                    string.Equals(display.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase))
                ?? displays.FirstOrDefault(display => display.IsPhoneMonitor)
                ?? displays.FirstOrDefault();
        }

        private static int ClampToPixel(double value, int size)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return 0;
            }

            return (int)Math.Round(Math.Max(0, Math.Min(1, value)) * Math.Max(0, size - 1));
        }
    }
}
