using System;
using System.Activities;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(MultipleAssignDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Multiple Assign")]
    [Description("Assign multiple variables in one activity. Add rows on the canvas with Add.")]
    public sealed class MultipleAssignActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        private readonly Collection<AssignEntry> _assignments = new Collection<AssignEntry>();

        public MultipleAssignActivity()
        {
            DisplayName = "Multiple Assign";
            EnsureAtLeastOneAssignment();
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public Collection<AssignEntry> Assignments
        {
            get { return _assignments; }
        }

        public Activity Create(DependencyObject target)
        {
            var activity = new MultipleAssignActivity();
            activity.Assignments.Clear();
            activity.Assignments.Add(new AssignEntry());
            return activity;
        }

        protected override void CacheMetadata(CodeActivityMetadata metadata)
        {
            EnsureAtLeastOneAssignment();

            var runtimeArguments = new Collection<RuntimeArgument>();
            for (int i = 0; i < _assignments.Count; i++)
            {
                AssignEntry entry = _assignments[i] ?? new AssignEntry();
                if (_assignments[i] == null)
                {
                    _assignments[i] = entry;
                }

                if (entry.To == null)
                {
                    entry.To = new OutArgument<object>();
                }

                if (entry.Value == null)
                {
                    entry.Value = new InArgument<object>();
                }

                Type toType = entry.To.ArgumentType ?? typeof(object);
                Type valueType = entry.Value.ArgumentType ?? typeof(object);

                if (entry.To.Direction != ArgumentDirection.Out && entry.To.Direction != ArgumentDirection.InOut)
                {
                    metadata.AddValidationError("Assignment To must be an OutArgument (row " + i + ").");
                }

                if (entry.Value.Direction != ArgumentDirection.In && entry.Value.Direction != ArgumentDirection.InOut)
                {
                    metadata.AddValidationError("Assignment Value must be an InArgument (row " + i + ").");
                }

                var toArgument = new RuntimeArgument(
                    "Assignment_To_" + i,
                    toType,
                    ArgumentDirection.Out);
                metadata.Bind(entry.To, toArgument);
                runtimeArguments.Add(toArgument);

                var valueArgument = new RuntimeArgument(
                    "Assignment_Value_" + i,
                    valueType,
                    ArgumentDirection.In);
                metadata.Bind(entry.Value, valueArgument);
                runtimeArguments.Add(valueArgument);
            }

            metadata.SetArgumentsCollection(runtimeArguments);
        }

        protected override void Execute(CodeActivityContext context)
        {
            foreach (AssignEntry entry in _assignments)
            {
                if (entry == null || entry.To == null || entry.To.Expression == null)
                {
                    continue;
                }

                object value = null;
                if (entry.Value != null && entry.Value.Expression != null)
                {
                    value = entry.Value.Get(context);
                }

                entry.To.Set(context, value);
            }
        }

        private void EnsureAtLeastOneAssignment()
        {
            if (_assignments.Count == 0)
            {
                _assignments.Add(new AssignEntry());
            }
        }
    }
}
