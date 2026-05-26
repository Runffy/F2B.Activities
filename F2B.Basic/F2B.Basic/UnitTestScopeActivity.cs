using System;
using System.Activities;
using System.Activities.Statements;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(UnitTestScopeDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("UnitTest Scope")]
    public sealed class UnitTestScopeActivity : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public UnitTestScopeActivity()
        {
            DisplayName = "UnitTest Scope";
        }

        [Browsable(false)]
        public Activity Body { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new UnitTestScopeActivity
            {
                Body = new Sequence()
            };
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            if (Body != null)
            {
                metadata.AddChild(Body);
            }
        }

        protected override void Execute(NativeActivityContext context)
        {
            if (ShouldExecuteScope(context) && Body != null)
            {
                context.ScheduleActivity(Body);
            }
        }

        private static bool ShouldExecuteScope(NativeActivityContext context)
        {
            return !IsInvoked(context);
        }

        private static bool IsInvoked(ActivityContext context)
        {
            try
            {
                string workflowInstanceId = context.WorkflowInstanceId.ToString();
                Type wfType = Type.GetType("OpenRPA.WorkflowInstance, OpenRPA", false);
                if (wfType == null)
                {
                    return false;
                }

                PropertyInfo instancesProp = wfType.GetProperty("Instances", BindingFlags.Public | BindingFlags.Static);
                if (instancesProp == null)
                {
                    return false;
                }

                var instancesEnum = instancesProp.GetValue(null) as global::System.Collections.IEnumerable;
                if (instancesEnum == null)
                {
                    return false;
                }

                foreach (object instance in instancesEnum)
                {
                    if (instance == null)
                    {
                        continue;
                    }

                    string instanceId = GetStringPropertyValue(instance, "InstanceId", "instanceid", "Id", "id");
                    if (!string.Equals(instanceId, workflowInstanceId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string caller = GetStringPropertyValue(instance, "caller", "Caller", "callerid", "CallerId");
                    return !string.IsNullOrWhiteSpace(caller);
                }
            }
            catch
            {
                // Keep scope behavior robust; if context cannot be resolved, default to direct-run behavior.
            }

            return false;
        }

        private static string GetStringPropertyValue(object target, params string[] propertyNames)
        {
            if (target == null || propertyNames == null || propertyNames.Length == 0)
            {
                return null;
            }

            Type type = target.GetType();
            foreach (string propertyName in propertyNames)
            {
                PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (property == null)
                {
                    continue;
                }

                object value = property.GetValue(target);
                if (value == null)
                {
                    continue;
                }

                string text = value as string;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                text = Convert.ToString(value, CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            return null;
        }
    }
}
