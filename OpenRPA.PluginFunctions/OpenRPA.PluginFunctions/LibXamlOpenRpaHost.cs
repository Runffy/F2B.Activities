using System;
using System.Activities;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenRPA;
using OpenRPA.Interfaces;
using OpenRPA.Interfaces.entity;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Runs a Lib XAML as an OpenRPA <see cref="WorkflowInstance"/> (same model as Invoke OpenRPA):
    /// supports Delay/bookmarks/Idle, F2B activities, and Lib→Lib via Customized.* wrappers.
    /// </summary>
    internal static class LibXamlOpenRpaHost
    {
        public static void Start(
            NativeActivityContext context,
            string fullPath,
            string displayName,
            Dictionary<string, object> inputs,
            InvokeLibXamlActivityBase activity,
            List<PropertyInfo> outProperties,
            BookmarkCallback bookmarkCallback)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (string.IsNullOrWhiteSpace(fullPath) || !System.IO.File.Exists(fullPath))
            {
                throw new System.IO.FileNotFoundException("Lib XAML not found.", fullPath);
            }

            string parentInstanceId = context.WorkflowInstanceId.ToString();
            WorkflowInstance myInstance = FindInstanceByActivityId(parentInstanceId);
            if (myInstance == null)
            {
                throw new InvalidOperationException(
                    "Invoke Lib XAML: parent OpenRPA WorkflowInstance not found. Libs must run inside an OpenRPA workflow.");
            }

            IProject project = ResolveProject(myInstance);
            if (project == null)
            {
                throw new InvalidOperationException(
                    "Invoke Lib XAML: cannot resolve an OpenRPA project for hosting the Lib workflow.");
            }

            Workflow workflow = Workflow.FromFile(project, fullPath);
            workflow.Serializable = false;
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                workflow.name = displayName;
            }

            try
            {
                workflow.ParseParameters();
            }
            catch (Exception ex)
            {
                Log.Warning("PluginFunctions: ParseParameters for Lib failed: " + ex.Message);
                if (workflow.Parameters == null)
                {
                    workflow.Parameters = new List<workflowparameter>();
                }
            }

            EnsureParameterMetadata(workflow, inputs);

            string traceId = myInstance.TraceId;
            string spanId = myInstance.SpanId;
            int nextIdent = myInstance.ident + 1;

            Exception createError = null;
            IWorkflowInstance instance = null;
            Views.WFDesigner designer = null;

            GenericTools.RunUI(() =>
            {
                try
                {
                    string designerKey = null;
                    try
                    {
                        designerKey = myInstance.Workflow != null
                            ? myInstance.Workflow.IDOrRelativeFilename
                            : null;
                    }
                    catch
                    {
                    }

                    if (!string.IsNullOrEmpty(designerKey))
                    {
                        designer = RobotInstance.instance.GetWorkflowDesignerByIDOrRelativeFilename(designerKey) as Views.WFDesigner;
                    }

                    idleOrComplete onIdle = null;
                    VisualTrackingHandler onTrack = null;
                    if (designer != null)
                    {
                        designer.BreakpointLocations = null;
                        onIdle = designer.IdleOrComplete;
                        onTrack = designer.OnVisualTracking;
                    }
                    else if (RobotInstance.instance.Window != null)
                    {
                        onIdle = RobotInstance.instance.Window.IdleOrComplete;
                    }

                    instance = workflow.CreateInstance(
                        inputs ?? new Dictionary<string, object>(),
                        null,
                        null,
                        onIdle,
                        onTrack,
                        nextIdent);

                    instance.caller = parentInstanceId;
                    if (!string.IsNullOrEmpty(traceId))
                    {
                        instance.TraceId = traceId;
                    }

                    if (!string.IsNullOrEmpty(spanId))
                    {
                        instance.SpanId = spanId;
                    }
                }
                catch (Exception ex)
                {
                    createError = ex;
                }
            }, 60000);

            if (createError != null)
            {
                throw createError;
            }

            if (instance == null)
            {
                throw new InvalidOperationException("Invoke Lib XAML: CreateInstance returned null.");
            }

            Log.Verbose("Invoke Lib XAML: started instance " + instance._id + " for " + fullPath);

            LibXamlPendingOutputs.Set(instance._id, activity, outProperties);

            context.CreateBookmark(instance._id, bookmarkCallback);
            if (instance.Bookmarks == null)
            {
                instance.Bookmarks = new Dictionary<string, object>();
            }

            if (!instance.Bookmarks.ContainsKey(instance._id))
            {
                instance.Bookmarks.Add(instance._id, null);
            }

            GenericTools.RunUI(() =>
            {
                if (designer != null)
                {
                    designer.Run(designer.VisualTracking, designer.SlowMotion, instance);
                }
                else
                {
                    instance.Run();
                }
            }, 60000);
        }

        public static void ApplyOutputs(
            NativeActivityContext context,
            IWorkflowInstance instance,
            IEnumerable<PropertyInfo> outProperties,
            object activityInstance)
        {
            if (context == null || instance == null || outProperties == null || activityInstance == null)
            {
                return;
            }

            if (instance.Parameters == null)
            {
                WorkflowInstance live = WorkflowInstance.Instances.FirstOrDefault(x => x._id == instance._id);
                if (live != null)
                {
                    instance = live;
                }
            }

            if (instance.Parameters == null)
            {
                return;
            }

            foreach (PropertyInfo property in outProperties)
            {
                if (property == null || string.IsNullOrWhiteSpace(property.Name))
                {
                    continue;
                }

                if (!instance.Parameters.ContainsKey(property.Name))
                {
                    continue;
                }

                Argument argument = property.GetValue(activityInstance, null) as Argument;
                if (argument == null)
                {
                    continue;
                }

                try
                {
                    argument.Set(context, instance.Parameters[property.Name]);
                }
                catch (Exception ex)
                {
                    Log.Error("PluginFunctions: failed setting Out '" + property.Name + "': " + ex.Message);
                }
            }
        }

        public static void ThrowIfFailed(IWorkflowInstance instance, string displayName)
        {
            if (instance == null || !instance.hasError)
            {
                return;
            }

            string name = string.IsNullOrWhiteSpace(displayName) ? "Lib XAML" : displayName;
            string message = name + " failed with " + (instance.errormessage ?? "error");
            throw new Exception(message, instance.Exception)
            {
                Source = instance.errorsource
            };
        }

        private static WorkflowInstance FindInstanceByActivityId(string workflowInstanceId)
        {
            if (string.IsNullOrWhiteSpace(workflowInstanceId))
            {
                return null;
            }

            return WorkflowInstance.Instances.FirstOrDefault(x =>
                x != null
                && string.Equals(x.InstanceId, workflowInstanceId, StringComparison.OrdinalIgnoreCase));
        }

        private static IProject ResolveProject(WorkflowInstance myInstance)
        {
            try
            {
                IProject fromParent = myInstance?.Workflow?.Project();
                if (fromParent != null)
                {
                    return fromParent;
                }
            }
            catch
            {
            }

            try
            {
                return RobotInstance.instance?.Projects?.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }

        private static void EnsureParameterMetadata(Workflow workflow, Dictionary<string, object> inputs)
        {
            if (workflow == null)
            {
                return;
            }

            if (workflow.Parameters == null)
            {
                workflow.Parameters = new List<workflowparameter>();
            }

            if (inputs == null || inputs.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<string, object> pair in inputs)
            {
                if (string.IsNullOrWhiteSpace(pair.Key))
                {
                    continue;
                }

                if (workflow.Parameters.Any(p => p != null && string.Equals(p.name, pair.Key, StringComparison.Ordinal)))
                {
                    continue;
                }

                string typeName = pair.Value != null ? pair.Value.GetType().FullName : typeof(object).FullName;
                workflow.Parameters.Add(new workflowparameter
                {
                    name = pair.Key,
                    type = typeName,
                    direction = workflowparameterdirection.@in
                });
            }
        }
    }
}
