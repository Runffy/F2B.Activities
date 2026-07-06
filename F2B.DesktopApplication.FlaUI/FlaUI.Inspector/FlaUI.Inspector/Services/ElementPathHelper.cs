using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace FlaUI.Inspector.Services
{
    public static class ElementPathHelper
    {
        /// <summary>
        /// 从目标元素向上构建路径，根节点为目标应用的顶层窗口（不含桌面 Shell）。
        /// </summary>
        public static IList<AutomationElement> BuildPathFromApplicationRoot(AutomationElement target)
        {
            var fullPath = BuildFullPathRootToTarget(target);
            if (fullPath.Count == 0)
                return fullPath;

            var appRoot = FindApplicationRootWindow(target);
            if (appRoot == null)
                return TrimDesktopShellPrefix(fullPath);

            for (var i = 0; i < fullPath.Count; i++)
            {
                if (ElementsEqual(fullPath[i], appRoot))
                    return fullPath.Skip(i).ToList();
            }

            return TrimDesktopShellPrefix(fullPath);
        }

        public static AutomationElement FindApplicationRootWindow(AutomationElement target)
        {
            if (target == null)
                return null;

            AutomationElement appRoot = null;
            var targetProcessId = TryGetProcessId(target);
            var current = target;

            while (current != null)
            {
                if (IsApplicationWindow(current, targetProcessId))
                    appRoot = current;

                try
                {
                    current = current.Parent;
                }
                catch
                {
                    break;
                }
            }

            return appRoot;
        }

        private static IList<AutomationElement> BuildFullPathRootToTarget(AutomationElement target)
        {
            var path = new List<AutomationElement>();
            var current = target;

            while (current != null)
            {
                if (path.Any(e => ElementsEqual(e, current)))
                    break;

                path.Add(current);

                try
                {
                    current = current.Parent;
                }
                catch
                {
                    break;
                }
            }

            path.Reverse();
            return path;
        }

        private static IList<AutomationElement> TrimDesktopShellPrefix(IList<AutomationElement> path)
        {
            var start = 0;
            for (var i = 0; i < path.Count; i++)
            {
                if (!IsDesktopShell(path[i]))
                {
                    start = i;
                    break;
                }
            }

            return path.Skip(start).ToList();
        }

        private static bool IsApplicationWindow(AutomationElement element, int targetProcessId)
        {
            if (!IsWindow(element) || IsDesktopShell(element))
                return false;

            if (targetProcessId <= 0)
                return true;

            return TryGetProcessId(element) == targetProcessId;
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

        private static bool IsDesktopShell(AutomationElement element)
        {
            try
            {
                var className = element.Properties.ClassName.IsSupported
                    ? element.Properties.ClassName.ValueOrDefault ?? string.Empty
                    : string.Empty;

                if (className.Equals("#32769", StringComparison.OrdinalIgnoreCase) ||
                    className.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
                    className.Equals("WorkerW", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var processId = element.Properties.ProcessId.ValueOrDefault;
                if (processId <= 0)
                    return false;

                try
                {
                    var processName = Process.GetProcessById(processId).ProcessName;
                    if (processName.Equals("csrss", StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (processName.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                        className.Equals("#32769", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch
                {
                }

                var name = element.Properties.Name.IsSupported
                    ? element.Properties.Name.ValueOrDefault ?? string.Empty
                    : string.Empty;

                if ((name.IndexOf("desktop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     name.IndexOf("桌面", StringComparison.Ordinal) >= 0) &&
                    IsDesktopClass(className))
                {
                    return TryGetProcessId(element) != Process.GetCurrentProcess().Id;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool IsDesktopClass(string className)
        {
            return className.Equals("#32769", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("Progman", StringComparison.OrdinalIgnoreCase);
        }

        private static int TryGetProcessId(AutomationElement element)
        {
            try
            {
                if (element.Properties.ProcessId.IsSupported)
                    return element.Properties.ProcessId.ValueOrDefault;
            }
            catch
            {
            }

            return -1;
        }

        private static bool ElementsEqual(AutomationElement left, AutomationElement right)
        {
            if (left == null || right == null)
                return false;

            try
            {
                return left.Equals(right);
            }
            catch
            {
                return ReferenceEquals(left, right);
            }
        }
    }
}
