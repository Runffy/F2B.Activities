using System;
using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Create Control")]
    [Description("Create a control inside a parent container (form / Panel / ScrollContainer / GroupBox / TabPage). For TableLayout cells use Create Control In Cell.")]
    public sealed class CreateControlActivity : CodeActivity
    {
        public CreateControlActivity()
        {
            DisplayName = "Create Control";
            X = new InArgument<string>("0");
            Y = new InArgument<string>("0");
        }

        [RequiredArgument]
        [DisplayName("Parent Control Id")]
        [Description("Container id: form / Panel / ScrollContainer / GroupBox / TabPage (or TabControl when creating a TabPage).")]
        [Category("Input.A")]
        public InArgument<string> ParentControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Type")]
        [Description("Control type, e.g. Button, Label, TextBox, Panel, ScrollContainer, TableLayout, DataGrid, TabPage.")]
        [Category("Input.A")]
        public InArgument<string> Type { get; set; }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Description("Unique id for the new control.")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [DisplayName("Text")]
        [Category("Input.B")]
        public InArgument<string> Text { get; set; }

        [DisplayName("X")]
        [Description("Left offset relative to the parent container. Default 0.")]
        [Category("Input.B")]
        public InArgument<string> X { get; set; }

        [DisplayName("Y")]
        [Description("Top offset relative to the parent container. Default 0.")]
        [Category("Input.B")]
        public InArgument<string> Y { get; set; }

        [DisplayName("Width")]
        [Description("Width in pixels. Empty / 0 = default (120).")]
        [Category("Input.B")]
        public InArgument<string> Width { get; set; }

        [DisplayName("Height")]
        [Description("Height in pixels. Empty / 0 = default (30).")]
        [Category("Input.B")]
        public InArgument<string> Height { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.CreateControl(
                ParentControlId.Get(context),
                Type.Get(context),
                ControlId.Get(context),
                Text.Get(context),
                ParseCoordinate(X.Get(context), "X"),
                ParseCoordinate(Y.Get(context), "Y"),
                ParseSize(Width.Get(context), "Width"),
                ParseSize(Height.Get(context), "Height"));
        }

        internal static int ParseCoordinate(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            if (!int.TryParse(value.Trim(), out int coordinate))
            {
                throw new InvalidOperationException(
                    name + " must be an integer. Value: '" + value + "'.");
            }

            return coordinate;
        }

        internal static int ParseSize(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return 0;
            }

            if (!int.TryParse(value.Trim(), out int size) || size < 0)
            {
                throw new InvalidOperationException(
                    name + " must be a non-negative integer, or empty for default. Value: '" + value + "'.");
            }

            return size;
        }
    }
}
