using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Set MixTable Style")]
    [Description("Set Font/color for MixTable Default | Column | Row | Cell. Empty fields keep current. Maps to DataGridViewCellStyle (Font + ForeColor/BackColor).")]
    public sealed class SetMixTableStyleActivity : CodeActivity
    {
        public SetMixTableStyleActivity()
        {
            DisplayName = "Set MixTable Style";
            Scope = new InArgument<string>("Cell");
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Description("MixTable id.")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Scope")]
        [Description("Default | Column | Row | Cell.")]
        [Category("Input.A")]
        public InArgument<string> Scope { get; set; }

        [DisplayName("Row Index")]
        [Description("Required for Row / Cell scope.")]
        [Category("Input.A")]
        public InArgument<int> RowIndex { get; set; }

        [DisplayName("Column")]
        [Description("Required for Column / Cell scope. Name or index.")]
        [Category("Input.A")]
        public InArgument<string> Column { get; set; }

        [DisplayName("Font Family")]
        [Description("Empty = keep.")]
        [Category("Input.B")]
        public InArgument<string> FontFamily { get; set; }

        [DisplayName("Font Size")]
        [Description("Point size. Empty = keep.")]
        [Category("Input.B")]
        public InArgument<string> FontSize { get; set; }

        [DisplayName("Bold")]
        [Description("Blank = keep; None = off; True = on.")]
        [Category("Input.C")]
        public InArgument<string> Bold { get; set; }

        [DisplayName("Italic")]
        [Description("Blank = keep; None = off; True = on.")]
        [Category("Input.C")]
        public InArgument<string> Italic { get; set; }

        [DisplayName("Underline")]
        [Description("Blank = keep; None = off; True = on.")]
        [Category("Input.C")]
        public InArgument<string> Underline { get; set; }

        [DisplayName("Fore Color")]
        [Description("HTML/named color e.g. #FF0000 or Red. Empty = keep.")]
        [Category("Input.D")]
        public InArgument<string> ForeColor { get; set; }

        [DisplayName("Back Color")]
        [Description("HTML/named color. Empty = keep. Ignored for header when Scope=Default.")]
        [Category("Input.D")]
        public InArgument<string> BackColor { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            string scope = Scope.Get(context);
            string normalized = MixTableStyleScopePreview(scope);

            int? row = null;
            if (string.Equals(normalized, "Row", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Cell", System.StringComparison.OrdinalIgnoreCase))
            {
                row = RowIndex.Get(context);
            }

            session.SetMixTableStyle(
                ControlId.Get(context),
                scope,
                row,
                Column.Get(context),
                FontFamily.Get(context),
                SetFontActivity.ParseSize(FontSize.Get(context)),
                SetFontActivity.ParseStyleFlag(Bold.Get(context)),
                SetFontActivity.ParseStyleFlag(Italic.Get(context)),
                SetFontActivity.ParseStyleFlag(Underline.Get(context)),
                ForeColor.Get(context),
                BackColor.Get(context));
        }

        private static string MixTableStyleScopePreview(string scope)
        {
            // Lightweight normalize for deciding whether RowIndex is needed;
            // FormSession.SetMixTableStyle validates fully.
            if (string.IsNullOrWhiteSpace(scope))
            {
                return "Default";
            }

            return scope.Trim();
        }
    }
}
