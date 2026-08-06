using System;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;

namespace F2B.OpenRpa.Design
{
    /// <summary>
    /// Resolves OpenRPA project root / name via reflection (no hard reference to OpenRPA assemblies).
    /// Screencast files live under: {ProjectsDirectory}\Projects\{projectName}\Screens\{uuid}.png
    /// </summary>
    public static class OpenRpaProjectPaths
    {
        private static readonly object CacheGate = new object();
        private static string _cachedProjectName;
        private static string _cachedScreensDirectory;
        private static readonly ConcurrentDictionary<string, string> UuidPathCache =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static string GetProjectsDirectory()
        {
            try
            {
                var extensionsType = FindType("OpenRPA.Interfaces.Extensions");
                var prop = extensionsType?.GetProperty("ProjectsDirectory", BindingFlags.Public | BindingFlags.Static);
                if (prop != null)
                {
                    var value = prop.GetValue(null) as string;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            catch
            {
            }

            return ResolveProjectsDirectoryFallback();
        }

        /// <summary>
        /// Returns the current designer workflow's project name, or null when unavailable.
        /// </summary>
        public static string TryGetCurrentProjectName()
        {
            try
            {
                var workflow = TryGetCurrentWorkflow();
                if (workflow == null)
                {
                    return GetCachedProjectName();
                }

                var projectAndName = GetStringProperty(workflow, "ProjectAndName");
                if (!string.IsNullOrWhiteSpace(projectAndName))
                {
                    var slash = projectAndName.IndexOf('/');
                    if (slash > 0)
                    {
                        return CacheProjectName(projectAndName.Substring(0, slash));
                    }

                    var backslash = projectAndName.IndexOf('\\');
                    if (backslash > 0)
                    {
                        return CacheProjectName(projectAndName.Substring(0, backslash));
                    }
                }

                var projectMethod = workflow.GetType().GetMethod("Project", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                var project = projectMethod?.Invoke(workflow, null);
                if (project != null)
                {
                    var name = GetStringProperty(project, "name") ?? GetStringProperty(project, "Name");
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return CacheProjectName(name);
                    }

                    var path = GetStringProperty(project, "Path");
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        return CacheProjectName(
                            Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                    }
                }
            }
            catch
            {
            }

            return GetCachedProjectName();
        }

        public static string GetScreensDirectory(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                throw new ArgumentException("projectName is required.", nameof(projectName));
            }

            return Path.Combine(GetProjectsDirectory(), "Projects", projectName, "Screens");
        }

        public static string ResolveScreencastPath(string projectName, string uuid)
        {
            if (string.IsNullOrWhiteSpace(uuid))
            {
                throw new ArgumentException("uuid is required.", nameof(uuid));
            }

            return Path.Combine(GetScreensDirectory(projectName), uuid + ".png");
        }

        public static bool TryGetScreensDirectory(out string screensDirectory, out string error)
        {
            screensDirectory = null;
            var projectName = TryGetCurrentProjectName();
            if (string.IsNullOrWhiteSpace(projectName))
            {
                lock (CacheGate)
                {
                    if (!string.IsNullOrWhiteSpace(_cachedScreensDirectory) &&
                        Directory.Exists(_cachedScreensDirectory))
                    {
                        screensDirectory = _cachedScreensDirectory;
                        error = null;
                        return true;
                    }
                }

                error = "Unable to resolve the current OpenRPA project name. Open a workflow in the designer and try again.";
                return false;
            }

            try
            {
                screensDirectory = GetScreensDirectory(projectName);
                CacheScreensDirectory(projectName, screensDirectory);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Resolves a screencast PNG path. Falls back to cache and Projects/*/Screens search
        /// when the active designer/project is not ready yet (common while opening a workflow).
        /// </summary>
        public static bool TryResolveScreencastFile(string uuid, out string fullPath, out string error)
        {
            fullPath = null;
            error = null;
            if (string.IsNullOrWhiteSpace(uuid))
            {
                error = "Screencast id is empty.";
                return false;
            }

            var key = uuid.Trim();
            string cachedPath;
            if (UuidPathCache.TryGetValue(key, out cachedPath) && File.Exists(cachedPath))
            {
                fullPath = cachedPath;
                return true;
            }

            string screensDir;
            string screensError;
            if (TryGetScreensDirectory(out screensDir, out screensError))
            {
                var candidate = Path.Combine(screensDir, key + ".png");
                if (File.Exists(candidate))
                {
                    UuidPathCache[key] = candidate;
                    fullPath = candidate;
                    return true;
                }
            }

            // Designer may not be ready yet; locate the file under any project Screens folder.
            var found = FindScreencastFileByUuid(key);
            if (!string.IsNullOrWhiteSpace(found) && File.Exists(found))
            {
                UuidPathCache[key] = found;
                try
                {
                    var screens = Path.GetDirectoryName(found);
                    var projectDir = string.IsNullOrWhiteSpace(screens) ? null : Path.GetDirectoryName(screens);
                    var projectName = string.IsNullOrWhiteSpace(projectDir) ? null : Path.GetFileName(projectDir);
                    if (!string.IsNullOrWhiteSpace(projectName) && !string.IsNullOrWhiteSpace(screens))
                    {
                        CacheScreensDirectory(projectName, screens);
                    }
                }
                catch
                {
                }

                fullPath = found;
                return true;
            }

            // Keep the expected path for callers that create files, even when missing.
            if (!string.IsNullOrWhiteSpace(screensDir))
            {
                fullPath = Path.Combine(screensDir, key + ".png");
                error = screensError;
                return true;
            }

            error = screensError ?? "Screencast image file was not found.";
            return false;
        }

        private static string FindScreencastFileByUuid(string uuid)
        {
            try
            {
                var projectsRoot = Path.Combine(GetProjectsDirectory(), "Projects");
                if (!Directory.Exists(projectsRoot))
                {
                    return null;
                }

                var fileName = uuid + ".png";
                foreach (var projectDir in Directory.EnumerateDirectories(projectsRoot))
                {
                    var candidate = Path.Combine(projectDir, "Screens", fileName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string CacheProjectName(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                return projectName;
            }

            lock (CacheGate)
            {
                _cachedProjectName = projectName.Trim();
            }

            return _cachedProjectName;
        }

        private static string GetCachedProjectName()
        {
            lock (CacheGate)
            {
                return _cachedProjectName;
            }
        }

        private static void CacheScreensDirectory(string projectName, string screensDirectory)
        {
            if (string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(screensDirectory))
            {
                return;
            }

            lock (CacheGate)
            {
                _cachedProjectName = projectName.Trim();
                _cachedScreensDirectory = screensDirectory;
            }
        }

        private static object TryGetCurrentWorkflow()
        {
            // OpenRPA.Interfaces.GenericTools.Designer.Workflow
            var genericTools = FindType("OpenRPA.Interfaces.GenericTools");
            if (genericTools != null)
            {
                var designerProp = genericTools.GetProperty("Designer", BindingFlags.Public | BindingFlags.Static);
                var designer = designerProp?.GetValue(null);
                var workflow = GetPropertyValue(designer, "Workflow");
                if (workflow != null)
                {
                    return workflow;
                }
            }

            // Fallback: ((IMainWindow)GenericTools.MainWindow).Designer
            if (genericTools != null)
            {
                var mainWindowProp = genericTools.GetProperty("MainWindow", BindingFlags.Public | BindingFlags.Static);
                var mainWindow = mainWindowProp?.GetValue(null);
                var designer = GetPropertyValue(mainWindow, "Designer");
                var workflow = GetPropertyValue(designer, "Workflow");
                if (workflow != null)
                {
                    return workflow;
                }
            }

            return null;
        }

        private static string ResolveProjectsDirectoryFallback()
        {
            var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var myDocumentsOpenRpa = Path.Combine(myDocuments, "OpenRPA");
            var appDataOpenRpa = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OpenRPA");

            if (File.Exists(Path.Combine(appDataOpenRpa, "settings.json")))
            {
                return appDataOpenRpa;
            }

            if (File.Exists(Path.Combine(myDocumentsOpenRpa, "settings.json")))
            {
                return myDocumentsOpenRpa;
            }

            return myDocumentsOpenRpa;
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(fullName, false);
                    if (type != null)
                    {
                        return type;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static object GetPropertyValue(object target, string propertyName)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
            {
                return null;
            }

            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return prop?.GetValue(target);
        }

        private static string GetStringProperty(object target, string propertyName)
        {
            return GetPropertyValue(target, propertyName) as string;
        }
    }
}
