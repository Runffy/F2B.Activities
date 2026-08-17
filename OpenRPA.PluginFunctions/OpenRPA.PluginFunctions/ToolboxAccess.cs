using System;
using System.Activities.Presentation.Toolbox;
using System.Reflection;
using System.Windows;
using System.Windows.Media;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Locates the OpenRPA ToolboxControl instance.
    /// </summary>
    internal static class ToolboxAccess
    {
        public static ToolboxControl FindToolboxControl()
        {
            object wfToolbox = GetWfToolboxInstance();
            if (wfToolbox != null)
            {
                FieldInfo tbField = wfToolbox.GetType().GetField(
                    "tb",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var named = tbField != null ? tbField.GetValue(wfToolbox) as ToolboxControl : null;
                if (named != null)
                {
                    return named;
                }

                var dep = wfToolbox as DependencyObject;
                if (dep != null)
                {
                    ToolboxControl nested = FindDescendant<ToolboxControl>(dep);
                    if (nested != null)
                    {
                        return nested;
                    }
                }
            }

            Window main = PluginContext.MainWindow;
            return main != null ? FindDescendant<ToolboxControl>(main) : null;
        }

        private static object GetWfToolboxInstance()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try
                {
                    type = assembly.GetType("OpenRPA.Views.WFToolbox", false);
                }
                catch
                {
                    continue;
                }

                if (type == null)
                {
                    continue;
                }

                PropertyInfo instanceProp = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp != null)
                {
                    return instanceProp.GetValue(null);
                }
            }

            return null;
        }

        private static T FindDescendant<T>(DependencyObject root) where T : DependencyObject
        {
            if (root == null)
            {
                return null;
            }

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                var match = child as T;
                if (match != null)
                {
                    return match;
                }

                T nested = FindDescendant<T>(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
