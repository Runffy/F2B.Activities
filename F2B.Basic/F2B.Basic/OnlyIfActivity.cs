using System;
using System.Activities;
using System.Activities.Statements;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(OnlyIfDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Only If")]
    [Description("Execute the Then branch only when the condition is true. There is no Else branch.")]
    public sealed class OnlyIfActivity : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public OnlyIfActivity()
        {
            DisplayName = "Only If";
        }

        [DisplayName("Condition")]
        [Description("When true, the Then activity is executed. When false or unset, Then is skipped.")]
        [Category("Input.A")]
        public InArgument<bool> Condition { get; set; }

        [Browsable(false)]
        public Activity Then { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new OnlyIfActivity
            {
                Then = new Sequence()
            };
        }

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            RuntimeArgument conditionArgument = new RuntimeArgument("Condition", typeof(bool), ArgumentDirection.In);
            metadata.Bind(Condition, conditionArgument);
            metadata.AddArgument(conditionArgument);

            if (Then != null)
            {
                metadata.AddChild(Then);
            }
        }

        protected override void Execute(NativeActivityContext context)
        {
            bool condition = Condition != null && Condition.Get(context);
            if (condition && Then != null)
            {
                context.ScheduleActivity(Then);
            }
        }
    }
}
