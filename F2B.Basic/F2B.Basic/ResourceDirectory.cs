using OpenRPA.Interfaces;
using System.IO;
using System.Linq;

namespace F2B.Basic
{
    /// <summary>
    /// Source-project resource directory:
    /// <c>{ProjectsDirectory}/Projects/{sourceProjectName}</c>
    /// Source project always from <see cref="OpenRpaSourceWorkflow"/> (shared with RuntimeDirectory).
    /// Expression: <c>F2B.Basic.ResourceDirectory.Path</c>
    /// </summary>
    public static class ResourceDirectory
    {
        public static string Path
        {
            get { return GetOrCreate(OpenRpaSourceWorkflow.ResolveSourceProjectName()); }
        }

        public static string GetOrCreate(System.Activities.CodeActivityContext context)
        {
            return GetOrCreate(OpenRpaSourceWorkflow.ResolveSourceProjectName(context));
        }

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
