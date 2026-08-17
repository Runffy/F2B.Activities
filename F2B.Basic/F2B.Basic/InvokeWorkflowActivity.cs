using System;
using System.Activities;
using System.Activities.Presentation.PropertyEditing;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using OpenRPA;
using OpenRPA.Interfaces;

namespace F2B.Basic
{
    [Designer(typeof(InvokeWorkflowDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Invoke Workflow")]
    [Description("Invoke another OpenRPA workflow. Select Project then Workflow; optionally log mapped In/Out arguments.")]
    public sealed class InvokeWorkflowActivity : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public InvokeWorkflowActivity()
        {
            DisplayName = "Invoke Workflow";
            WaitForCompleted = true;
            KillIfRunning = true;
            LogInputArguments = false;
            LogOutputArguments = false;
            Arguments = new Dictionary<string, Argument>();

            var builder = new System.Activities.Presentation.Metadata.AttributeTableBuilder();
            builder.AddCustomAttributes(
                typeof(InvokeWorkflowActivity),
                "Arguments",
                new EditorAttribute(
                    typeof(OpenRPA.Interfaces.Activities.ArgumentCollectionEditor),
                    typeof(PropertyValueEditor)));
            System.Activities.Presentation.Metadata.MetadataStore.AddAttributeTable(builder.CreateTable());
        }

        [RequiredArgument]
        [DisplayName("Workflow")]
        [Description("Stored as ProjectAndName (project/workflow). Prefer selecting via the designer dropdowns.")]
        [Category("Input.A")]
        public InArgument<string> Workflow { get; set; }

        [RequiredArgument]
        [DisplayName("Wait For Completed")]
        [Category("Input.B")]
        [DefaultValue(true)]
        public InArgument<bool> WaitForCompleted { get; set; }

        [RequiredArgument]
        [DisplayName("Kill If Running")]
        [Category("Input.B")]
        [DefaultValue(true)]
        public InArgument<bool> KillIfRunning { get; set; }

        [DisplayName("Log Input Arguments")]
        [Description("When true, log each In/InOut argument (Log Message formatting) before the target workflow starts. Expression allowed.")]
        [Category("Input.C")]
        [DefaultValue(false)]
        public InArgument<bool> LogInputArguments { get; set; }

        [DisplayName("Log Output Arguments")]
        [Description("When true, log each Out/InOut argument (Log Message formatting) after the target workflow completes. Expression allowed.")]
        [Category("Input.C")]
        [DefaultValue(false)]
        public InArgument<bool> LogOutputArguments { get; set; }

        [DisplayName("Arguments")]
        [Category("Input")]
        [Browsable(true)]
        public Dictionary<string, Argument> Arguments { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new InvokeWorkflowActivity();
        }

        protected override bool CanInduceIdle
        {
            get { return true; }
        }

        protected override void Execute(NativeActivityContext context)
        {
            string workflowInstanceId = context.WorkflowInstanceId.ToString();
            WorkflowInstance myInstance = WorkflowInstance.Instances
                .FirstOrDefault(x => x.InstanceId == workflowInstanceId);
            string traceId = myInstance?.TraceId;
            string spanId = myInstance?.SpanId;

            bool waitForCompleted = WaitForCompleted != null && WaitForCompleted.Get(context);
            bool killIfRunning = KillIfRunning != null && KillIfRunning.Get(context);
            bool logInput = LogInputArguments != null && LogInputArguments.Get(context);

            var param = BuildInputParameters(context);

            if (logInput)
            {
                LogMappedArguments(
                    context.WorkflowInstanceId.ToString(),
                    "IN",
                    param,
                    ArgumentDirection.In);
            }

            Exception error = null;
            try
            {
                string workflowId = Workflow.Get(context);
                Workflow workflow = RobotInstance.instance.GetWorkflowByIDOrRelativeFilename(workflowId) as Workflow;
                if (workflow == null)
                {
                    throw new ArgumentException("Failed locating workflow " + workflowId);
                }

                if (killIfRunning)
                {
                    KillRunningInstances(workflow._id, myInstance?.Workflow?.name);
                }

                IWorkflowInstance instance = null;
                OpenRPA.Views.WFDesigner designer = null;

                GenericTools.RunUI(() =>
                {
                    try
                    {
                        designer = RobotInstance.instance.GetWorkflowDesignerByIDOrRelativeFilename(workflowId) as OpenRPA.Views.WFDesigner;
                        if (designer != null)
                        {
                            designer.BreakpointLocations = null;
                            instance = workflow.CreateInstance(
                                param,
                                null,
                                null,
                                designer.IdleOrComplete,
                                designer.OnVisualTracking,
                                (myInstance?.ident ?? 0) + 1);
                        }
                        else
                        {
                            instance = workflow.CreateInstance(
                                param,
                                null,
                                null,
                                RobotInstance.instance.Window.IdleOrComplete,
                                null,
                                (myInstance?.ident ?? 0) + 1);
                        }

                        instance.caller = workflowInstanceId;
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
                        error = ex;
                    }
                }, 60000);

                if (error != null)
                {
                    throw error;
                }

                if (instance != null)
                {
                    Log.Verbose("Invoke Workflow: Run Instance ID " + instance._id);
                    if (waitForCompleted)
                    {
                        context.CreateBookmark(instance._id, OnBookmarkCallback);
                        if (instance.Bookmarks == null)
                        {
                            instance.Bookmarks = new Dictionary<string, object>();
                        }

                        instance.Bookmarks.Add(instance._id, null);
                    }
                }

                GenericTools.RunUI(() =>
                {
                    if (designer != null && instance != null)
                    {
                        designer.Run(designer.VisualTracking, designer.SlowMotion, instance);
                    }
                    else if (instance != null)
                    {
                        instance.Run();
                    }
                }, 60000);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                throw;
            }
        }

        private Dictionary<string, object> BuildInputParameters(NativeActivityContext context)
        {
            var param = new Dictionary<string, object>();
            if (Arguments == null || Arguments.Count == 0)
            {
                var vars = context.DataContext.GetProperties();
                foreach (PropertyDescriptor v in vars)
                {
                    try
                    {
                        param[v.Name] = v.GetValue(context.DataContext);
                    }
                    catch
                    {
                        // ignore unreadable variables
                    }
                }

                return param;
            }

            foreach (KeyValuePair<string, Argument> argument in Arguments)
            {
                if (argument.Value == null || argument.Value.Direction == ArgumentDirection.Out)
                {
                    continue;
                }

                try
                {
                    param[argument.Key] = argument.Value.Get(context);
                }
                catch
                {
                    param[argument.Key] = null;
                }
            }

            return param;
        }

        private static void KillRunningInstances(string workflowId, string callerWorkflowName)
        {
            if (string.IsNullOrWhiteSpace(workflowId))
            {
                return;
            }

            try
            {
                Type globalType = Type.GetType("OpenRPA.Interfaces.global, OpenRPA.Interfaces", false)
                    ?? Type.GetType("OpenRPA.Interfaces.global, OpenRPA", false);
                System.Reflection.PropertyInfo clientProp = globalType?.GetProperty(
                    "OpenRPAClient",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                object client = clientProp?.GetValue(null);
                object instancesObj = client?.GetType().GetProperty("WorkflowInstances")?.GetValue(client);
                var instances = instancesObj as System.Collections.IEnumerable;
                if (instances == null)
                {
                    return;
                }

                foreach (object item in instances.Cast<object>().ToList())
                {
                    var instance = item as IWorkflowInstance;
                    if (instance?.Workflow == null || instance.isCompleted)
                    {
                        continue;
                    }

                    if (!string.Equals(instance.Workflow._id, workflowId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    instance.Abort("Killed by KillIfRunning from " + callerWorkflowName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        private void OnBookmarkCallback(NativeActivityContext context, Bookmark bookmark, object obj)
        {
            try
            {
                bool waitForCompleted = WaitForCompleted != null && WaitForCompleted.Get(context);
                if (!waitForCompleted)
                {
                    return;
                }

                var instance = obj as WorkflowInstance;
                if (instance == null)
                {
                    throw new Exception("Bookmark returned a non WorkflowInstance");
                }

                Workflow workflow = RobotInstance.instance.GetWorkflowByIDOrRelativeFilename(Workflow.Get(context)) as Workflow;
                string name = "The invoked workflow";
                if (workflow != null && !string.IsNullOrEmpty(workflow.name))
                {
                    name = workflow.name;
                }

                if (workflow != null && !string.IsNullOrEmpty(workflow.ProjectAndName))
                {
                    name = workflow.ProjectAndName;
                }

                if (instance.hasError)
                {
                    throw new Exception(name + " failed with " + instance.errormessage, instance.Exception)
                    {
                        Source = instance.errorsource
                    };
                }

                if (Arguments == null || Arguments.Count == 0)
                {
                    if (instance.Parameters != null)
                    {
                        foreach (KeyValuePair<string, object> prop in instance.Parameters)
                        {
                            PropertyDescriptor myVar = context.DataContext.GetProperties().Find(prop.Key, true);
                            if (myVar != null)
                            {
                                myVar.SetValue(context.DataContext, prop.Value);
                            }
                            else
                            {
                                Log.Debug("Received property " + prop.Key + " but no variable exists to save the value.");
                            }
                        }
                    }
                }
                else
                {
                    if (instance.Parameters == null
                        && WorkflowInstance.Instances.FirstOrDefault(x => x._id == instance._id) != null)
                    {
                        instance = WorkflowInstance.Instances.FirstOrDefault(x => x._id == instance._id);
                    }

                    foreach (KeyValuePair<string, Argument> argument in Arguments)
                    {
                        if (argument.Value == null || argument.Value.Direction == ArgumentDirection.In)
                        {
                            continue;
                        }

                        if (instance.Parameters != null && instance.Parameters.ContainsKey(argument.Key))
                        {
                            Arguments[argument.Key].Set(context, instance.Parameters[argument.Key]);
                        }
                        else
                        {
                            try
                            {
                                if (Arguments[argument.Key].ArgumentType.IsValueType)
                                {
                                    Arguments[argument.Key].Set(
                                        context,
                                        Activator.CreateInstance(Arguments[argument.Key].ArgumentType));
                                }
                                else
                                {
                                    Arguments[argument.Key].Set(context, null);
                                }
                            }
                            catch (Exception ex)
                            {
                                Log.Error("Error setting " + argument.Key + ": " + ex.Message);
                            }
                        }
                    }
                }

                bool logOutput = LogOutputArguments != null && LogOutputArguments.Get(context);
                if (logOutput)
                {
                    var outputValues = new Dictionary<string, object>();
                    if (Arguments == null || Arguments.Count == 0)
                    {
                        if (instance.Parameters != null)
                        {
                            foreach (KeyValuePair<string, object> prop in instance.Parameters)
                            {
                                outputValues[prop.Key] = prop.Value;
                            }
                        }
                    }
                    else
                    {
                        foreach (KeyValuePair<string, Argument> argument in Arguments)
                        {
                            if (argument.Value == null || argument.Value.Direction == ArgumentDirection.In)
                            {
                                continue;
                            }

                            if (instance.Parameters != null && instance.Parameters.ContainsKey(argument.Key))
                            {
                                outputValues[argument.Key] = instance.Parameters[argument.Key];
                            }
                            else
                            {
                                try
                                {
                                    outputValues[argument.Key] = Arguments[argument.Key].Get(context);
                                }
                                catch
                                {
                                    outputValues[argument.Key] = null;
                                }
                            }
                        }
                    }

                    LogMappedArguments(
                        context.WorkflowInstanceId.ToString(),
                        "OUT",
                        outputValues,
                        ArgumentDirection.Out);
                }
            }
            catch (Exception ex) when (ex.InnerException is BusinessRuleException)
            {
                throw ex.InnerException;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                throw;
            }
        }

        private void LogMappedArguments(
            string workflowInstanceId,
            string directionLabel,
            IDictionary<string, object> values,
            ArgumentDirection direction)
        {
            if (values == null || values.Count == 0)
            {
                LogMessageActivity.WriteFormatted(
                    workflowInstanceId,
                    "INFO",
                    "Invoke Workflow " + directionLabel + ": (no arguments)");
                return;
            }

            // When Arguments mapping exists, restrict to matching directions (InOut counts for both).
            IEnumerable<KeyValuePair<string, object>> toLog = values;
            if (Arguments != null && Arguments.Count > 0)
            {
                toLog = values.Where(pair =>
                {
                    Argument mapped;
                    if (!Arguments.TryGetValue(pair.Key, out mapped) || mapped == null)
                    {
                        return true;
                    }

                    if (direction == ArgumentDirection.In)
                    {
                        return mapped.Direction == ArgumentDirection.In
                               || mapped.Direction == ArgumentDirection.InOut;
                    }

                    return mapped.Direction == ArgumentDirection.Out
                           || mapped.Direction == ArgumentDirection.InOut;
                });
            }

            foreach (KeyValuePair<string, object> pair in toLog)
            {
                string prefix = "Invoke Workflow " + directionLabel + " [" + pair.Key + "]: ";
                LogMessageActivity.WriteFormatted(
                    workflowInstanceId,
                    "INFO",
                    prefix + LogMessageActivity.FormatLogMessage(pair.Value));
            }
        }
    }
}
