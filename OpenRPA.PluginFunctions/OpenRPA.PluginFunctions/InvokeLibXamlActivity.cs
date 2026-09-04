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
    /// </summary>
    [DisplayName("Invoke Lib XAML")]
    [Description("Runs a workflow XAML from Documents\\OpenRPA\\Libs.")]
    public abstract class InvokeLibXamlActivityBase : NativeActivity
    {
        protected override bool CanInduceIdle
        {
            get { return false; }
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

            // Public In/OutArgument properties on the emitted subclass are bound by base.
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

            Activity activity = LoadActivity(fullPath);
            if (activity == null)
            {
                throw new InvalidOperationException("Failed to load Lib XAML: " + fullPath);
            }

            var inputs = new Dictionary<string, object>();
            var outProperties = new List<PropertyInfo>();

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

            IDictionary<string, object> outputs = WorkflowInvoker.Invoke(activity, inputs);

            if (outputs == null || outProperties.Count == 0)
            {
                return;
            }

            foreach (PropertyInfo property in outProperties)
            {
                if (!outputs.ContainsKey(property.Name))
                {
                    continue;
                }

                Argument argument = property.GetValue(this, null) as Argument;
                if (argument == null)
                {
                    continue;
                }

                try
                {
                    argument.Set(context, outputs[property.Name]);
                }
                catch (Exception ex)
                {
                    Log.Error(
                        "PluginFunctions: failed setting Out argument '" + property.Name + "': " + ex.Message);
                }
            }
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

    internal sealed class LibXamlArgumentSpec
    {
        public string Name { get; set; }
        /// <summary>Typically typeof(InArgument&lt;T&gt;) / OutArgument&lt;T&gt; / InOutArgument&lt;T&gt;.</summary>
        public Type ArgumentClrType { get; set; }
    }
}
