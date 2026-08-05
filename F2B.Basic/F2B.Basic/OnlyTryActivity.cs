using System;
using System.Activities;
using System.Activities.Statements;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    /// <summary>
    /// Scope with only a Try body: any fault inside is swallowed (empty catch of Exception).
    /// </summary>
    [Designer(typeof(OnlyTryDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Only Try")]
    [Description("Run the Try body and silently ignore any exception (like Catch Exception with an empty handler).")]
    public sealed class OnlyTryActivity : NativeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public OnlyTryActivity()
        {
            DisplayName = "Only Try";
        }

        [Browsable(false)]
        public Activity Body { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new OnlyTryActivity
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
            if (Body != null)
            {
                context.ScheduleActivity(Body, OnBodyComplete, OnBodyFault);
            }
        }

        private void OnBodyFault(
            NativeActivityFaultContext faultContext,
            Exception propagatedException,
            ActivityInstance propagatedFrom)
        {
            // HandleFault alone is not enough: without CancelChild, a Sequence Body can
            // continue scheduling activities after the faulted one.
            faultContext.HandleFault();
            if (propagatedFrom != null)
            {
                faultContext.CancelChild(propagatedFrom);
            }
        }

        private void OnBodyComplete(NativeActivityContext context, ActivityInstance completedInstance)
        {
            // Faulted path already handled in OnBodyFault.
        }
    }
}
