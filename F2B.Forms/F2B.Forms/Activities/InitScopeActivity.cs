using System.Activities;
using System.ComponentModel;
using System.Windows;
using F2B.Forms.Designers;

namespace F2B.Forms.Activities
{
    /// <summary>
    /// Optional toolbox wrapper; AsyncForm prefers its Init child slot.
    /// When used, Body is scheduled by AsyncForm as Init if present in BindEvents collection — not used that way.
    /// Kept for clarity: Init is a Sequence on AsyncForm, not this type.
    /// </summary>
    [Designer(typeof(SimpleBodyDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Init Scope")]
    [Description("Initialization scope for AsyncForm. Prefer the Init slot on AsyncForm.")]
    public sealed class InitScopeActivity : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public InitScopeActivity()
        {
            DisplayName = "Init Scope";
        }

        [Browsable(false)]
        public Activity Body { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new InitScopeActivity
            {
                Body = new System.Activities.Statements.Sequence { DisplayName = "Init" }
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
            if (Body != null)
            {
                context.ScheduleActivity(Body);
            }
        }
    }
}
