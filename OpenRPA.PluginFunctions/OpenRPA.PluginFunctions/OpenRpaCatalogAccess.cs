using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Reads RobotInstance.Projects / Workflows via reflection (OpenRPA assembly is not referenced).
    /// Note: on RobotInstance these are public fields, not properties.
    /// </summary>
    internal static class OpenRpaCatalogAccess
    {
        public static IReadOnlyList<IWorkflow> GetAllWorkflows()
        {
            var list = new List<IWorkflow>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                object robot = GetRobotInstance();
                if (robot != null)
                {
                    AddWorkflows(GetEnumerableMember(robot, "Workflows"), list, seen);

                    if (list.Count == 0)
                    {
                        IEnumerable projects = GetEnumerableMember(robot, "Projects");
                        if (projects != null)
                        {
                            foreach (object projectObj in projects)
                            {
                                if (projectObj == null)
                                {
                                    continue;
                                }

                                IEnumerable projectWorkflows = GetEnumerableMember(projectObj, "Workflows");
                                var project = projectObj as IProject;
                                if (projectWorkflows == null && project != null)
                                {
                                    projectWorkflows = project.Workflows;
                                }

                                AddWorkflows(projectWorkflows, list, seen);
                            }
                        }
                    }
                }
            }
            catch
            {
            }

            return list;
        }

        public static string ResolveXaml(IWorkflow workflow)
        {
            if (workflow == null)
            {
                return null;
            }

            try
            {
                string xaml = workflow.Xaml;
                if (!string.IsNullOrWhiteSpace(xaml))
                {
                    return xaml;
                }
            }
            catch
            {
            }

            try
            {
                string path = workflow.FilePath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = workflow.Filename;
                }

                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }
            catch
            {
            }

            return null;
        }

        public static string GetProjectName(IWorkflow workflow)
        {
            if (workflow == null)
            {
                return string.Empty;
            }

            try
            {
                string projectAndName = workflow.ProjectAndName;
                if (!string.IsNullOrWhiteSpace(projectAndName))
                {
                    int slash = projectAndName.IndexOf('/');
                    if (slash > 0)
                    {
                        return projectAndName.Substring(0, slash).Trim();
                    }

                    return projectAndName.Trim();
                }

                string relative = workflow.RelativeFilename;
                if (!string.IsNullOrWhiteSpace(relative))
                {
                    int slash = relative.IndexOf('/');
                    if (slash > 0)
                    {
                        return relative.Substring(0, slash).Trim();
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static void AddWorkflows(IEnumerable source, List<IWorkflow> list, HashSet<string> seen)
        {
            if (source == null || list == null || seen == null)
            {
                return;
            }

            foreach (object item in source)
            {
                var wf = item as IWorkflow;
                if (wf == null)
                {
                    continue;
                }

                string key = !string.IsNullOrWhiteSpace(wf._id)
                    ? wf._id
                    : (wf.RelativeFilename ?? wf.Filename ?? wf.name ?? Guid.NewGuid().ToString());

                if (!seen.Add(key))
                {
                    continue;
                }

                list.Add(wf);
            }
        }

        private static object GetRobotInstance()
        {
            // Prefer live client — runtime type is RobotInstance.
            if (PluginContext.Client != null)
            {
                return PluginContext.Client;
            }

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly == null)
                {
                    continue;
                }

                string name = assembly.GetName().Name;
                if (!string.Equals(name, "OpenRPA", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Type type = assembly.GetType("OpenRPA.RobotInstance", false);
                if (type == null)
                {
                    continue;
                }

                PropertyInfo prop = type.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
                if (prop != null)
                {
                    return prop.GetValue(null, null);
                }

                FieldInfo field = type.GetField("instance", BindingFlags.Public | BindingFlags.Static);
                if (field != null)
                {
                    return field.GetValue(null);
                }
            }

            return null;
        }

        private static IEnumerable GetEnumerableMember(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
            {
                return null;
            }

            Type type = target.GetType();

            PropertyInfo prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (prop != null)
            {
                try
                {
                    return prop.GetValue(target, null) as IEnumerable;
                }
                catch
                {
                }
            }

            FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
            if (field != null)
            {
                try
                {
                    return field.GetValue(target) as IEnumerable;
                }
                catch
                {
                }
            }

            return null;
        }
    }
}
