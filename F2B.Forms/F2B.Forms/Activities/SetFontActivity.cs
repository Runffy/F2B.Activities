using System;
using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SetFontDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Set Font")]
    [Description("Partially update font on a control. Leave a field empty to keep the current value.")]
    public sealed class SetFontActivity : CodeActivity
    {
        public const string StyleKeep = "";
        public const string StyleNone = "None";
        public const string StyleTrue = "True";

        public SetFontActivity()
        {
            DisplayName = "Set Font";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [DisplayName("Font Family")]
        [Description("Empty / null = do not change.")]
        [Category("Input.B")]
        public InArgument<string> FontFamily { get; set; }

        [DisplayName("Font Size")]
        [Description("Point size. Empty / null = do not change.")]
        [Category("Input.B")]
        public InArgument<string> FontSize { get; set; }

        [DisplayName("Bold")]
        [Description("Blank = keep; None = not bold; True = bold.")]
        [Category("Input.C")]
        public InArgument<string> Bold { get; set; }

        [DisplayName("Italic")]
        [Description("Blank = keep; None = not italic; True = italic.")]
        [Category("Input.C")]
        public InArgument<string> Italic { get; set; }

        [DisplayName("Underline")]
        [Description("Blank = keep; None = not underline; True = underline.")]
        [Category("Input.C")]
        public InArgument<string> Underline { get; set; }

        [DisplayName("Fore Color")]
        [Description("HTML color e.g. #000000 or Red. Empty / null = do not change.")]
        [Category("Input.D")]
        public InArgument<string> ForeColor { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.SetControlFont(
                ControlId.Get(context),
                FontFamily.Get(context),
                ParseSize(FontSize.Get(context)),
                ParseStyleFlag(Bold.Get(context)),
                ParseStyleFlag(Italic.Get(context)),
                ParseStyleFlag(Underline.Get(context)),
                ForeColor.Get(context));
        }

        internal static float? ParseSize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (float.TryParse(value.Trim(), out float size) && size > 0)
            {
                return size;
            }

            throw new InvalidOperationException("Font Size must be a positive number, or empty to keep current.");
        }

        /// <summary>
        /// null = keep; false = None (clear style); true = apply style.
        /// </summary>
        internal static bool? ParseStyleFlag(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string text = value.Trim();
            if (string.Equals(text, StyleNone, StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "False", StringComparison.OrdinalIgnoreCase)
                || text == "0")
            {
                return false;
            }

            if (string.Equals(text, StyleTrue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "Yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "Bold", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "Italic", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "Underline", StringComparison.OrdinalIgnoreCase)
                || text == "1")
            {
                return true;
            }

            throw new InvalidOperationException(
                "Style flag must be blank (keep), 'None' (clear), or 'True' (apply). Value: '" + value + "'.");
        }
    }
}
