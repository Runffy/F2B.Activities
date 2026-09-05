using System;
using System.Activities;
using System.Activities.XamlIntegration;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Base for emitted per-file Lib activities. Subclasses declare real In/OutArgument
    /// properties (like a DLL activity) so WF binds each Argument exactly once.
    /// Runtime uses OpenRPA WorkflowInstance (Idle/bookmarks/F2B/Lib↔Lib via Customized.*).
    /// </summary>
    [DisplayName("Invoke Lib XAML")]
    [Description("Runs a workflow XAML from Documents\\OpenRPA\\Libs via OpenRPA workflow host.")]
    public abstract class InvokeLibXamlActivityBase : NativeActivity
    {
        protected override bool CanInduceIdle
        {
            get { return true; }
        }

        /// <summary>Path relative to Libs, e.g. add.xaml or MWS/Foo.xaml.</summary>
        protected abstract string GetRelativePath();

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            string relative = GetRelativePath();
            if (string.IsNullOrWhiteSpace(relative))
            {
                metadata.AddValidationError("Lib relative path is empty.");
            }

            base.CacheMetadata(metadata);
        }

        protected override void Execute(NativeActivityContext context)
        {
            string relative = GetRelativePath();
            if (string.IsNullOrWhiteSpace(relative))
            {
                throw new InvalidOperationException("Invoke Lib XAML: relative path is empty.");
            }

            string fullPath = ResolveFullPath(relative);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Lib XAML not found: " + fullPath, fullPath);
            }

            Dictionary<string, object> inputs;
            List<PropertyInfo> outProperties;
            CollectArguments(context, out inputs, out outProperties);

            LibXamlOpenRpaHost.Start(
                context,
                fullPath,
                DisplayName,
                inputs,
                this,
                outProperties,
                OnLibWorkflowBookmark);
        }

        private void OnLibWorkflowBookmark(NativeActivityContext context, Bookmark bookmark, object value)
        {
            var instance = value as IWorkflowInstance;
            if (instance == null)
            {
                throw new InvalidOperationException("Invoke Lib XAML: bookmark returned a non WorkflowInstance.");
            }

            string key = bookmark != null ? bookmark.Name : instance._id;
            LibXamlPendingOutputs.TryTake(
                key,
                out InvokeLibXamlActivityBase activity,
                out List<PropertyInfo> outProperties);

            object target = activity ?? this;
            List<PropertyInfo> outs = outProperties ?? CollectOutPropertiesOnly();

            LibXamlOpenRpaHost.ThrowIfFailed(instance, DisplayName);
            LibXamlOpenRpaHost.ApplyOutputs(context, instance, outs, target);
        }

        private void CollectArguments(
            NativeActivityContext context,
            out Dictionary<string, object> inputs,
            out List<PropertyInfo> outProperties)
        {
            inputs = new Dictionary<string, object>();
            outProperties = new List<PropertyInfo>();

            foreach (PropertyInfo property in GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property == null || !property.CanRead)
                {
                    continue;
                }

                if (!typeof(Argument).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                Argument argument;
                try
                {
                    argument = property.GetValue(this, null) as Argument;
                }
                catch
                {
                    continue;
                }

                if (argument == null)
                {
                    continue;
                }

                if (argument.Direction != ArgumentDirection.Out)
                {
                    try
                    {
                        inputs[property.Name] = argument.Get(context);
                    }
                    catch
                    {
                        inputs[property.Name] = null;
                    }
                }

                if (argument.Direction != ArgumentDirection.In)
                {
                    outProperties.Add(property);
                }
            }
        }

        private List<PropertyInfo> CollectOutPropertiesOnly()
        {
            var outProperties = new List<PropertyInfo>();
            foreach (PropertyInfo property in GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property == null || !typeof(Argument).IsAssignableFrom(property.PropertyType))
                {
                    continue;
                }

                Argument argument = property.GetValue(this, null) as Argument;
                if (argument != null && argument.Direction != ArgumentDirection.In)
                {
                    outProperties.Add(property);
                }
            }

            return outProperties;
        }

        internal static string ToRelativePath(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                return string.Empty;
            }

            try
            {
                string root = Path.GetFullPath(LibXamlPaths.GetLibsRoot())
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string full = Path.GetFullPath(absolutePath);
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                    && full.Length > root.Length)
                {
                    string rel = full.Substring(root.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    return rel.Replace(Path.DirectorySeparatorChar, '/');
                }
            }
            catch
            {
            }

            return absolutePath.Replace(Path.DirectorySeparatorChar, '/');
        }

        internal static string ResolveFullPath(string relativeOrAbsolute)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolute))
            {
                return relativeOrAbsolute;
            }

            if (Path.IsPathRooted(relativeOrAbsolute) && File.Exists(relativeOrAbsolute))
            {
                return relativeOrAbsolute;
            }

            string normalized = relativeOrAbsolute.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(LibXamlPaths.GetLibsRoot(), normalized));
        }

        internal static Activity LoadActivity(string fullPath)
        {
            string xaml = File.ReadAllText(fullPath);
            if (string.IsNullOrWhiteSpace(xaml))
            {
                throw new InvalidOperationException("XAML file is empty: " + fullPath);
            }

            using (var reader = new StringReader(xaml))
            {
                return ActivityXamlServices.Load(reader);
            }
        }

        internal static List<LibXamlArgumentSpec> ReadArgumentSpecs(string absolutePath)
        {
            var result = new List<LibXamlArgumentSpec>();
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                return result;
            }

            try
            {
                Activity loaded = LoadActivity(absolutePath);
                var dynamicActivity = loaded as DynamicActivity;
                if (dynamicActivity?.Properties == null)
                {
                    return result;
                }

                foreach (DynamicActivityProperty property in dynamicActivity.Properties)
                {
                    if (property == null || string.IsNullOrWhiteSpace(property.Name) || property.Type == null)
                    {
                        continue;
                    }

                    result.Add(new LibXamlArgumentSpec
                    {
                        Name = property.Name,
                        ArgumentClrType = property.Type
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warning("PluginFunctions: could not read arguments from " + absolutePath + ": " + ex.Message);
            }

            return result;
        }
    }

    /// <summary>
    /// Holds Out argument property list between Execute and bookmark callback.
    /// Keyed by child OpenRPA instance _id (bookmark name) so nested/parallel Libs are safe.
    /// </summary>
    internal static class LibXamlPendingOutputs
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, PendingEntry> Map =
            new Dictionary<string, PendingEntry>(StringComparer.OrdinalIgnoreCase);

        private sealed class PendingEntry
        {
            public InvokeLibXamlActivityBase Activity;
            public List<PropertyInfo> OutProperties;
        }

        public static void Set(string childInstanceId, InvokeLibXamlActivityBase activity, List<PropertyInfo> outProperties)
        {
            if (string.IsNullOrWhiteSpace(childInstanceId))
            {
                return;
            }

            lock (Gate)
            {
                Map[childInstanceId] = new PendingEntry
                {
                    Activity = activity,
                    OutProperties = outProperties
                };
            }
        }

        public static bool TryTake(
            string childInstanceId,
            out InvokeLibXamlActivityBase activity,
            out List<PropertyInfo> outProperties)
        {
            lock (Gate)
            {
                PendingEntry entry;
                if (!string.IsNullOrWhiteSpace(childInstanceId)
                    && Map.TryGetValue(childInstanceId, out entry))
                {
                    Map.Remove(childInstanceId);
                    activity = entry.Activity;
                    outProperties = entry.OutProperties;
                    return true;
                }
            }

            activity = null;
            outProperties = null;
            return false;
        }
    }

    internal sealed class LibXamlArgumentSpec
    {
        public string Name { get; set; }
        public Type ArgumentClrType { get; set; }
    }
}
