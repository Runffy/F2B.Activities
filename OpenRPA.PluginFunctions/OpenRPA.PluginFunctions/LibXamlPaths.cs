using System;
using System.IO;
using System.Reflection;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Resolves Documents\OpenRPA\Libs (under OpenRPA ProjectsDirectory).
    /// </summary>
    internal static class LibXamlPaths
    {
        public const string LibsFolderName = "Libs";
        public const string CategoryPrefix = "Customized.";
        public const string UncategorizedSegment = "Uncategorized";

        public static string UncategorizedCategory
        {
            get { return CategoryPrefix + UncategorizedSegment; }
        }

        public static string GetLibsRoot()
        {
            string projectsDir = TryGetProjectsDirectory();
            if (string.IsNullOrWhiteSpace(projectsDir))
            {
                projectsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "OpenRPA");
            }

            return Path.Combine(projectsDir, LibsFolderName);
        }

        public static string ToCategoryName(string firstLevelFolderName)
        {
            if (string.IsNullOrWhiteSpace(firstLevelFolderName))
            {
                return UncategorizedCategory;
            }

            return CategoryPrefix + firstLevelFolderName.Trim();
        }

        public static void EnsureLibsRootExists()
        {
            try
            {
                string root = GetLibsRoot();
                if (!Directory.Exists(root))
                {
                    Directory.CreateDirectory(root);
                    Log.Information("PluginFunctions: created Libs folder at " + root);
                }
            }
            catch (Exception ex)
            {
                Log.Warning("PluginFunctions: could not create Libs folder: " + ex.Message);
            }
        }

        private static string TryGetProjectsDirectory()
        {
            try
            {
                return Extensions.ProjectsDirectory;
            }
            catch
            {
            }

            try
            {
                Type extensionsType = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        extensionsType = assembly.GetType("OpenRPA.Interfaces.Extensions", false);
                    }
                    catch
                    {
                        continue;
                    }

                    if (extensionsType != null)
                    {
                        break;
                    }
                }

                PropertyInfo prop = extensionsType?.GetProperty("ProjectsDirectory", BindingFlags.Public | BindingFlags.Static);
                object value = prop?.GetValue(null, null);
                return value as string;
            }
            catch
            {
                return null;
            }
        }
    }
}
