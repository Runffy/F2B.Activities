using OpenRPA.Interfaces;
using System;
using System.IO;
using System.Linq;

namespace F2B.Basic
{
    /// <summary>
    /// Source-project resource directory:
    /// <c>{ProjectsDirectory}/Projects/{sourceProjectName}</c>
    /// (compare Runtime: <c>{ProjectsDirectory}/Runtime/{sourceProjectName}/{timestamp}</c>).
    /// Project name always follows the outermost source workflow (Invoke OpenRPA caller chain).
    /// Expression: <c>F2B.Basic.ResourceDirectory.Path</c>
    /// </summary>
    public static class ResourceDirectory
    {
        /// <summary>
        /// Absolute path of the source project's resource folder under Projects.
        /// </summary>
        public static string Path
        {
            get { return GetOrCreate(RuntimeDirectory.ResolveSourceProjectName()); }
        }

        /// <summary>
        /// Resolve (and ensure) the resource directory for the workflow bound to <paramref name="context"/>.
        /// </summary>
        public static string GetOrCreate(System.Activities.CodeActivityContext context)
        {
            return GetOrCreate(RuntimeDirectory.ResolveSourceProjectName(context));
        }

        /// <summary>
        /// Build <c>{ProjectsDirectory}/Projects/{projectName}</c> and create it if missing.
        /// </summary>
        public static string GetOrCreate(string projectName)
        {
            string safeProjectName = SanitizePathSegment(
                string.IsNullOrWhiteSpace(projectName) ? "UnknownProject" : projectName.Trim());
            string directory = System.IO.Path.Combine(
                Extensions.ProjectsDirectory,
                "Projects",
                safeProjectName);

            Directory.CreateDirectory(directory);
            return directory;
        }

        private static string SanitizePathSegment(string value)
        {
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }
}
