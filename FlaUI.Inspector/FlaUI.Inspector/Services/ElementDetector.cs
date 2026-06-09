using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Inspector.Helpers;

namespace FlaUI.Inspector.Services
{
    public sealed class ElementDetector
    {
        private readonly int _currentProcessId = Process.GetCurrentProcess().Id;

        public AutomationElement DetectAtPoint(int x, int y)
        {
            var automation = AutomationContext.Instance.Automation;
            var point = new Point(x, y);

            var topWindowHandle = NativeMethods.WindowFromPoint(new NativeMethods.POINT { X = x, Y = y });
            if (topWindowHandle == IntPtr.Zero)
                return null;

            AutomationElement hitElement;
            try
            {
                hitElement = automation.FromPoint(point);
            }
            catch
            {
                return null;
            }

            if (hitElement == null)
                return null;

            if (IsOwnProcessElement(hitElement))
                return null;

            var rootWindow = FindRootWindow(hitElement, topWindowHandle);
            if (rootWindow == null)
                return null;

            return FindSmallestContainingElement(rootWindow, point, hitElement);
        }

        private bool IsOwnProcessElement(AutomationElement element)
        {
            try
            {
                if (element.Properties.ProcessId.IsSupported &&
                    element.Properties.ProcessId.ValueOrDefault == _currentProcessId)
                    return true;
            }
            catch
            {
            }

            return false;
        }

        private static AutomationElement FindRootWindow(AutomationElement hitElement, IntPtr topWindowHandle)
        {
            AutomationElement current = hitElement;
            AutomationElement lastWindow = null;

            while (current != null)
            {
                try
                {
                    if (current.Properties.ControlType.IsSupported &&
                        current.Properties.ControlType.Value == ControlType.Window)
                    {
                        lastWindow = current;
                        if (current.Properties.NativeWindowHandle.IsSupported &&
                            current.Properties.NativeWindowHandle.ValueOrDefault == topWindowHandle)
                        {
                            return current;
                        }
                    }
                }
                catch
                {
                }

                try
                {
                    current = current.Parent;
                }
                catch
                {
                    break;
                }
            }

            if (lastWindow != null)
                return lastWindow;

            try
            {
                return AutomationContext.Instance.Automation.FromHandle(topWindowHandle);
            }
            catch
            {
                return hitElement;
            }
        }

        private static AutomationElement FindSmallestContainingElement(
            AutomationElement rootWindow,
            Point point,
            AutomationElement hitElement)
        {
            var candidates = new List<AutomationElement>();
            var current = hitElement;

            while (current != null)
            {
                var rect = GetBoundingRectangle(current);
                if (!rect.IsEmpty && rect.Contains(point))
                    candidates.Add(current);

                if (current.Equals(rootWindow))
                    break;

                try
                {
                    current = current.Parent;
                }
                catch
                {
                    break;
                }
            }

            if (candidates.Count == 0)
                return hitElement;

            return candidates
                .OrderBy(GetBoundingRectangle, RectangleAreaComparer.Instance)
                .First();
        }

        public static Rectangle GetBoundingRectangle(AutomationElement element)
        {
            try
            {
                if (element.Properties.BoundingRectangle.IsSupported)
                    return element.Properties.BoundingRectangle.ValueOrDefault;
            }
            catch
            {
            }

            return Rectangle.Empty;
        }

        private sealed class RectangleAreaComparer : IComparer<Rectangle>
        {
            public static readonly RectangleAreaComparer Instance = new RectangleAreaComparer();

            public int Compare(Rectangle x, Rectangle y)
            {
                var areaX = (long)x.Width * x.Height;
                var areaY = (long)y.Width * y.Height;
                return areaX.CompareTo(areaY);
            }
        }
    }
}
