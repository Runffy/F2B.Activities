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
            var conditionArgument = new RuntimeArgument("Condition", typeof(bool), ArgumentDirection.In);
            metadata.Bind(Condition, conditionArgument);
            metadata.AddArgument(conditionArgument);

            if (Condition == null || Condition.Expression == null)
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

                var elseIfCondition = new RuntimeArgument(
                    "ElseIf_Condition_" + i,
                    typeof(bool),
                    ArgumentDirection.In);
                metadata.Bind(branch.Condition, elseIfCondition);
                metadata.AddArgument(elseIfCondition);

                if (branch.Condition == null || branch.Condition.Expression == null)
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
        }

        protected override void Execute(NativeActivityContext context)
        {
            if (Condition == null || Condition.Expression == null)
            {
                throw new InvalidOperationException("Else If: If Condition is not set.");
            }

            if (Condition.Get(context))
            {
                if (Then != null)
                {
                    context.ScheduleActivity(Then);
                }

                return;
            }

            for (int i = 0; i < _elseIfs.Count; i++)
            {
                ElseIfBranch branch = _elseIfs[i];
                if (branch == null)
                {
                    continue;
                }

                if (branch.Condition == null || branch.Condition.Expression == null)
                {
                    throw new InvalidOperationException(
                        "Else If: Else If Condition #" + (i + 1) + " is not set.");
                }

                if (branch.Condition.Get(context))
                {
                    if (branch.Body != null)
                    {
                        context.ScheduleActivity(branch.Body);
                    }

                    return;
                }
            }

            if (Else != null)
            {
                context.ScheduleActivity(Else);
            }
        }
    }
}
