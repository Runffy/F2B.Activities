using System;
using System.Activities;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(ThrowSystemExceptionDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Throw System Exception")]
    public sealed class ThrowSystemExceptionActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        [RequiredArgument]
        [DisplayName("Message")]
        public InArgument<string> Message { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new ThrowSystemExceptionActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            string message = Message.Get(context) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Message is required.", nameof(Message));
            }

            throw new Exception(message);
        }
    }
}
