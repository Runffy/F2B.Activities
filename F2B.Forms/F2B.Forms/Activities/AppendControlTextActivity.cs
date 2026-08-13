using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Append Control Text")]
    [Description("Append text to a control (TextArea/TextBox recommended). Separator is inserted before the new text when content already exists. Use \\n for a new line; any other string is used as a literal separator. Scrolls to end by default.")]
    public sealed class AppendControlTextActivity : CodeActivity
    {
        public AppendControlTextActivity()
        {
            DisplayName = "Append Control Text";
            Separator = "\\n";
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

        /// <summary>
        /// Plain string so the property pane can type \n without an expression.
        /// </summary>
        [DisplayName("Separator")]
        [Description("Inserted between existing content and new text when the control is not empty. Type \\n for newline, \\r\\n for CRLF, or any literal like \" | \". Leave empty to concatenate with no separator.")]
        [Category("Input.B")]
        [DefaultValue("\\n")]
        public string Separator { get; set; }

        [DisplayName("Scroll To End")]
        [Description("After append, move caret to the end and scroll into view (recommended for log TextArea).")]
        [Category("Input.B")]
        [DefaultValue(true)]
        public bool ScrollToEnd { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.AppendControlText(
                ControlId.Get(context),
                Text.Get(context),
                Separator,
                ScrollToEnd);
        }
    }
}
