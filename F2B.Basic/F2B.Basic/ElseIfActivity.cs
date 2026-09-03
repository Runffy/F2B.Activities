using System;
using System.Activities;
using System.Activities.Statements;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(ElseIfDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Else If")]
    [Description("If / ElseIf / Else: run the first branch whose condition is true; otherwise run Else.")]
    public sealed class ElseIfActivity : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        private readonly Collection<ElseIfBranch> _elseIfs = new Collection<ElseIfBranch>();

        /// <summary>
        /// -1 = main If condition; 0..n-1 = ElseIf branch index.
        /// </summary>
        private readonly Variable<int> _conditionIndex = new Variable<int>();

        public ElseIfActivity()
        {
            DisplayName = "Else If";
        }

        [DisplayName("Condition")]
        [Description("If condition. When true, Then is executed.")]
        [Category("Input.A")]
        public InArgument<bool> Condition { get; set; }

        [Browsable(false)]
        public Activity Then { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public Collection<ElseIfBranch> ElseIfs
        {
            get { return _elseIfs; }
        }

        [Browsable(false)]
        public Activity Else { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new ElseIfActivity
            {
                Then = new Sequence(),
                Else = new Sequence()
            };
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            // Do NOT Bind conditions as RuntimeArguments. WF evaluates all bound InArguments
            // before Execute, which breaks short-circuit and can throw on later ElseIf expressions
            // (e.g. a.ToString() when a is null). Schedule each Expression only when needed.
            if (Condition != null && Condition.Expression != null)
            {
                metadata.AddImplementationChild(Condition.Expression);
            }
            else
            {
                metadata.AddValidationError("If Condition is required.");
            }

            if (Then != null)
            {
                metadata.AddChild(Then);
            }

            for (int i = 0; i < _elseIfs.Count; i++)
            {
                ElseIfBranch branch = _elseIfs[i] ?? new ElseIfBranch();
                if (_elseIfs[i] == null)
                {
                    _elseIfs[i] = branch;
                }

                if (branch.Condition != null && branch.Condition.Expression != null)
                {
                    metadata.AddImplementationChild(branch.Condition.Expression);
                }
                else
                {
                    metadata.AddValidationError("Else If Condition #" + (i + 1) + " is required.");
                }

                if (branch.Body != null)
                {
                    metadata.AddChild(branch.Body);
                }
            }

            if (Else != null)
            {
                metadata.AddChild(Else);
            }

            metadata.AddImplementationVariable(_conditionIndex);
        }

        protected override void Execute(NativeActivityContext context)
        {
            context.SetValue(_conditionIndex, -1);
            ScheduleConditionAtIndex(context);
        }

        private void ScheduleConditionAtIndex(NativeActivityContext context)
        {
            int index = context.GetValue(_conditionIndex);
            Activity<bool> expression = GetConditionExpression(index);
            if (expression == null)
            {
                throw new InvalidOperationException(DescribeMissingCondition(index));
            }

            context.ScheduleActivity(expression, OnConditionComplete);
        }

        private void OnConditionComplete(
            NativeActivityContext context,
            ActivityInstance completedInstance,
            bool result)
        {
            int index = context.GetValue(_conditionIndex);
            if (result)
            {
                Activity body = GetBody(index);
                if (body != null)
                {
                    context.ScheduleActivity(body);
                }

                return;
            }

            int nextIndex = index + 1;
            if (nextIndex < _elseIfs.Count)
            {
                context.SetValue(_conditionIndex, nextIndex);
                ScheduleConditionAtIndex(context);
                return;
            }

            if (Else != null)
            {
                context.ScheduleActivity(Else);
            }
        }

        private Activity<bool> GetConditionExpression(int index)
        {
            if (index < 0)
            {
                return Condition != null ? Condition.Expression : null;
            }

            if (index >= _elseIfs.Count)
            {
                return null;
            }

            ElseIfBranch branch = _elseIfs[index];
            return branch != null && branch.Condition != null
                ? branch.Condition.Expression
                : null;
        }

        private Activity GetBody(int index)
        {
            if (index < 0)
            {
                return Then;
            }

            if (index >= _elseIfs.Count)
            {
                return null;
            }

            ElseIfBranch branch = _elseIfs[index];
            return branch != null ? branch.Body : null;
        }

        private string DescribeMissingCondition(int index)
        {
            if (index < 0)
            {
                return "Else If: If Condition is not set.";
            }

            return "Else If: Else If Condition #" + (index + 1) + " is not set.";
        }
    }
}
