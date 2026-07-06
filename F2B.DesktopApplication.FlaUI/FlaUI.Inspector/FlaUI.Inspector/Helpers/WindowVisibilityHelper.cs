using System;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Inspector.Models;

namespace FlaUI.Inspector.Helpers
{
    internal static class WindowVisibilityHelper
    {
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        /// <summary>
        /// Returns true for user-visible top-level windows. Hidden/off-screen/zero-size windows are excluded.
        /// Windows minimized to the taskbar remain eligible; tray-only or background windows are excluded.
        /// </summary>
        public static bool IsVisibleWindow(AutomationElement element)
        {
            if (element == null)
                return false;

            try
            {
                if (element.Properties.ControlType.IsSupported)
                {
                    var controlType = element.Properties.ControlType.Value;
                    if (controlType != ControlType.Window && controlType != ControlType.Pane)
                        return false;
                }

                var isMinimized = false;
                if (element.Properties.NativeWindowHandle.IsSupported)
                {
                    var handle = element.Properties.NativeWindowHandle.ValueOrDefault;
                    if (handle != IntPtr.Zero)
                    {
                        isMinimized = IsIconic(handle);
                        if (!isMinimized && !IsWindowVisible(handle))
                            return false;
                    }
                }

                if (!isMinimized)
                {
                    if (element.Properties.IsOffscreen.IsSupported && element.Properties.IsOffscreen.ValueOrDefault)
                        return false;

                    if (element.Properties.BoundingRectangle.IsSupported)
                    {
                        var rect = element.Properties.BoundingRectangle.ValueOrDefault;
                        if (rect.Width <= 0 || rect.Height <= 0)
                            return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsWindowLevel(SelectorLevel level)
        {
            return level != null
                && string.Equals(level.TagName, "wnd", StringComparison.OrdinalIgnoreCase);
        }
    }
}
