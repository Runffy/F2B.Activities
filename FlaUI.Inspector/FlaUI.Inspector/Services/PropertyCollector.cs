using System;
using System.Collections.Generic;
using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Inspector.Models;

namespace FlaUI.Inspector.Services
{
    public static class PropertyCollector
    {
        public static IList<ElementPropertyItem> CollectAll(AutomationElement element)
        {
            var items = new List<ElementPropertyItem>();
            if (element == null)
                return items;

            TryAdd(items, "ControlType", GetControlType(element));
            TryAddString(items, "Name", element);
            TryAddString(items, "AutomationId", element);
            TryAddString(items, "ClassName", element);
            TryAddString(items, "FrameworkId", element);
            TryAddString(items, "LocalizedControlType", element);
            TryAddString(items, "HelpText", element);
            TryAddString(items, "AcceleratorKey", element);
            TryAddString(items, "AccessKey", element);
            TryAddBool(items, "IsEnabled", element);
            TryAddBool(items, "IsOffscreen", element);
            TryAddBool(items, "HasKeyboardFocus", element);
            TryAddBool(items, "IsKeyboardFocusable", element);
            TryAddBool(items, "IsPassword", element);
            TryAddBool(items, "IsContentElement", element);
            TryAddBool(items, "IsControlElement", element);

            var rect = ElementDetector.GetBoundingRectangle(element);
            if (!rect.IsEmpty)
            {
                TryAdd(items, "BoundingRectangle", rect.ToString());
                TryAdd(items, "X", rect.X.ToString());
                TryAdd(items, "Y", rect.Y.ToString());
                TryAdd(items, "Width", rect.Width.ToString());
                TryAdd(items, "Height", rect.Height.ToString());
            }

            try
            {
                if (element.Properties.ProcessId.IsSupported)
                    TryAdd(items, "ProcessId", element.Properties.ProcessId.ValueOrDefault.ToString());
            }
            catch
            {
            }

            try
            {
                if (element.Properties.NativeWindowHandle.IsSupported &&
                    element.Properties.NativeWindowHandle.ValueOrDefault != IntPtr.Zero)
                {
                    TryAdd(items, "NativeWindowHandle",
                        "0x" + element.Properties.NativeWindowHandle.ValueOrDefault.ToString("X"));
                }
            }
            catch
            {
            }

            if (IsWindow(element))
                AddWindowProperties(items, element);

            return items;
        }

        private static bool IsWindow(AutomationElement element)
        {
            try
            {
                return element.Properties.ControlType.IsSupported &&
                       element.Properties.ControlType.Value == ControlType.Window;
            }
            catch
            {
                return false;
            }
        }

        private static void AddWindowProperties(IList<ElementPropertyItem> items, AutomationElement element)
        {
            try
            {
                var processId = element.Properties.ProcessId.ValueOrDefault;
                if (processId <= 0)
                    return;

                var process = Process.GetProcessById(processId);
                TryAdd(items, "ProcessName", process.ProcessName);
                try
                {
                    TryAdd(items, "FileName", process.MainModule?.FileName);
                }
                catch
                {
                }
            }
            catch
            {
            }
        }

        private static void TryAdd(IList<ElementPropertyItem> items, string name, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            items.Add(new ElementPropertyItem
            {
                Name = name,
                Value = value,
                CanToggle = false
            });
        }

        private static void TryAddString(IList<ElementPropertyItem> items, string name, AutomationElement element)
        {
            try
            {
                switch (name)
                {
                    case "Name":
                        if (element.Properties.Name.IsSupported)
                            TryAdd(items, name, element.Properties.Name.ValueOrDefault);
                        break;
                    case "AutomationId":
                        if (element.Properties.AutomationId.IsSupported)
                            TryAdd(items, name, element.Properties.AutomationId.ValueOrDefault);
                        break;
                    case "ClassName":
                        if (element.Properties.ClassName.IsSupported)
                            TryAdd(items, name, element.Properties.ClassName.ValueOrDefault);
                        break;
                    case "FrameworkId":
                        if (element.Properties.FrameworkId.IsSupported)
                            TryAdd(items, name, element.Properties.FrameworkId.ValueOrDefault);
                        break;
                    case "LocalizedControlType":
                        if (element.Properties.LocalizedControlType.IsSupported)
                            TryAdd(items, name, element.Properties.LocalizedControlType.ValueOrDefault);
                        break;
                    case "HelpText":
                        if (element.Properties.HelpText.IsSupported)
                            TryAdd(items, name, element.Properties.HelpText.ValueOrDefault);
                        break;
                    case "AcceleratorKey":
                        if (element.Properties.AcceleratorKey.IsSupported)
                            TryAdd(items, name, element.Properties.AcceleratorKey.ValueOrDefault);
                        break;
                    case "AccessKey":
                        if (element.Properties.AccessKey.IsSupported)
                            TryAdd(items, name, element.Properties.AccessKey.ValueOrDefault);
                        break;
                }
            }
            catch
            {
            }
        }

        private static void TryAddBool(IList<ElementPropertyItem> items, string name, AutomationElement element)
        {
            try
            {
                switch (name)
                {
                    case "IsEnabled":
                        if (element.Properties.IsEnabled.IsSupported)
                            TryAdd(items, name, element.Properties.IsEnabled.ValueOrDefault.ToString());
                        break;
                    case "IsOffscreen":
                        if (element.Properties.IsOffscreen.IsSupported)
                            TryAdd(items, name, element.Properties.IsOffscreen.ValueOrDefault.ToString());
                        break;
                    case "HasKeyboardFocus":
                        if (element.Properties.HasKeyboardFocus.IsSupported)
                            TryAdd(items, name, element.Properties.HasKeyboardFocus.ValueOrDefault.ToString());
                        break;
                    case "IsKeyboardFocusable":
                        if (element.Properties.IsKeyboardFocusable.IsSupported)
                            TryAdd(items, name, element.Properties.IsKeyboardFocusable.ValueOrDefault.ToString());
                        break;
                    case "IsPassword":
                        if (element.Properties.IsPassword.IsSupported)
                            TryAdd(items, name, element.Properties.IsPassword.ValueOrDefault.ToString());
                        break;
                    case "IsContentElement":
                        if (element.Properties.IsContentElement.IsSupported)
                            TryAdd(items, name, element.Properties.IsContentElement.ValueOrDefault.ToString());
                        break;
                    case "IsControlElement":
                        if (element.Properties.IsControlElement.IsSupported)
                            TryAdd(items, name, element.Properties.IsControlElement.ValueOrDefault.ToString());
                        break;
                }
            }
            catch
            {
            }
        }

        private static string GetControlType(AutomationElement element)
        {
            try
            {
                if (element.Properties.ControlType.IsSupported)
                {
                    var value = element.Properties.ControlType.Value;
                    if (value != ControlType.Unknown)
                        return value.ToString();
                }
            }
            catch
            {
            }

            return null;
        }
    }
}
