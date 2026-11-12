using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("MixTable")]
    [DisplayName("Set MixTable Columns")]
    [Description("Define MixTable columns JSON: [{name,type,text,items,width,headerAlign,contentAlign}]. type: Label|TextBox|ComboBox|CheckBox|Button|Link. headerAlign/contentAlign: left-top|middle-center|...")]
    public sealed class SetMixTableColumnsActivity : CodeActivity
    {
        public SetMixTableColumnsActivity()
        {
            DisplayName = "Set MixTable Columns";
        }

        [RequiredArgument]
        [DisplayName("Control Id")]
        [Category("Input.A")]
        public InArgument<string> ControlId { get; set; }

        [RequiredArgument]
        [DisplayName("Columns Json")]
        [Description("JSON array of columns. Example: [{\"name\":\"Select\",\"type\":\"Button\",\"text\":\"-\"},{\"name\":\"AccountNo\",\"type\":\"Text\"}]")]
        [Category("Input.A")]
        public InArgument<string> ColumnsJson { get; set; }

        protected override void Execute(CodeActivityContext context)
        {
            FormSession session = FormSessionAccess.GetRequired(context);
            session.SetMixTableColumns(ControlId.Get(context), ColumnsJson.Get(context));
        }
    }
}
