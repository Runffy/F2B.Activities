using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Set MixTable Align")]
    [Description("Set cell content alignment for Column | Row | Cell. Does not change header align (Columns Json headerAlign only). Align: left-top|middle-center|right-bottom|...")]
    public sealed class SetMixTableAlignActivity : CodeActivity
    {
        public SetMixTableAlignActivity()
        {
            DisplayName = "Set MixTable Align";
            Scope = new InArgument<string>("Cell");
            Align = new InArgument<string>("middle-center");
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Description("MixTable id.")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Scope")]
        [Description("Column | Row | Cell. Header alignment is not supported here.")]
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

        [RequiredArgument]
        [DisplayName("Align")]
        [Description("left-top | middle-top | right-top | left-center | middle-center | right-center | left-bottom | middle-bottom | right-bottom (or TopLeft, MiddleCenter, ...).")]
        [Category("Input.B")]
        public InArgument<string> Align { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            string scope = Scope.Get(context);
            string normalized = string.IsNullOrWhiteSpace(scope) ? "Cell" : scope.Trim();

            int? row = null;
            if (string.Equals(normalized, "Row", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "Cell", System.StringComparison.OrdinalIgnoreCase))
            {
                row = RowIndex.Get(context);
            }

            session.SetMixTableAlign(
                ControlId.Get(context),
                scope,
                row,
                Column.Get(context),
                Align.Get(context));
        }
    }
}
