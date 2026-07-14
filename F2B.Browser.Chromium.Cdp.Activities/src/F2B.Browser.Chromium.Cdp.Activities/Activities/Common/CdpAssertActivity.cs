using System;
using System.Activities;
using System.Activities.Presentation;
using System.ComponentModel;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    [DisplayName("Assert")]
    [Description("Fails the workflow when Condition is false.")]
    [Designer(typeof(CdpEmptyActivityDesigner))]
    public sealed class CdpAssertActivity : CodeActivity
    {
        public CdpAssertActivity()
        {
            DisplayName = "Assert";
        }

        [DisplayName("Condition")]
        [Description("When false, the assertion fails.")]
        [RequiredArgument]
        public InArgument<bool> Condition { get; set; }

        [DisplayName("Message")]
        [Description("Error message when the assertion fails.")]
        public InArgument<string> Message { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            if (Condition.Get(context))
            {
                return;
            }

            var message = Message == null ? null : Message.Get(context);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(message) ? "Assertion failed." : message);
        }
    }
}
