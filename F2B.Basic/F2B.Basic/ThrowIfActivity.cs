using System;
using System.Activities;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(ThrowIfDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Throw If")]
    [Description("When Condition is true, throw System.Exception with Message. When false, skip silently.")]
    public sealed class ThrowIfActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public ThrowIfActivity()
        {
            DisplayName = "Throw If";
        }

        [RequiredArgument]
        [DisplayName("Condition")]
        [Description("When true, throws. When false, does nothing.")]
        [Category("Input.A")]
        public InArgument<bool> Condition { get; set; }

        [RequiredArgument]
        [DisplayName("Message")]
        [Description("Exception message used when Condition is true.")]
        [Category("Input.A")]
        public InArgument<string> Message { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new ThrowIfActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            if (Condition == null || Condition.Expression == null)
            {
                throw new InvalidOperationException("Throw If: Condition is required.");
            }

            if (!Condition.Get(context))
            {
                return;
            }

            string message = Message == null ? null : Message.Get(context);
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Throw If: Message is required when Condition is true.", nameof(Message));
            }

            throw new Exception(message);
        }
    }
}
