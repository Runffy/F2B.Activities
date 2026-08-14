using System;
using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Append Control Text")]
    [Description("Append text to a control (TextArea/TextBox recommended). Separator defaults to vbNewLine when unset; pass \"\" to concatenate with no separator; any other value is used as-is. Scrolls to end by default.")]
    public sealed class AppendControlTextActivity : CodeActivity
    {
        public AppendControlTextActivity()
        {
            DisplayName = "Append Control Text";
            ScrollToEnd = true;
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Text")]
        [Description("Text to append.")]
        [Category("Input.A")]
        public InArgument<string> Text { get; set; }

        [DisplayName("Separator")]
        [Description("Inserted between existing content and new text when the control is not empty. Unset/null = vbNewLine (newline). \"\" = direct concatenate. Any other expression value is used as-is (e.g. \" | \").")]
        [Category("Input.B")]
        public InArgument<string> Separator { get; set; }

        [DisplayName("Scroll To End")]
        [Description("After append, move caret to the end and scroll into view (recommended for log TextArea).")]
        [Category("Input.B")]
        [DefaultValue(true)]
        public bool ScrollToEnd { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            string separator = ResolveSeparator(context);
            session.AppendControlText(
                ControlId.Get(context),
                Text.Get(context),
                separator,
                ScrollToEnd);
        }

        private string ResolveSeparator(CodeActivityContext context)
        {
            if (Separator == null)
            {
                return Environment.NewLine;
            }

            string value = Separator.Get(context);
            return value ?? Environment.NewLine;
        }
    }
}
