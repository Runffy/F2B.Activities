using System.Activities;
using System.ComponentModel;
using F2B.Forms.Designers;
using F2B.Forms.Session;

namespace F2B.Forms.Activities
{
    [Designer(typeof(SimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("Dialog")]
    [DisplayName("Show Input On Form")]
    [Description("Modal popup: message + text input + button. Uses the active form when inside AsyncForm; otherwise standalone. Is Password=true uses a masked text box; otherwise a multiline text area. Blocks until OK or close (X → Input Text null). Allow Null=false throws on null.")]
    public sealed class ShowInputOnFormActivity : CodeActivity<string>
    {
        public ShowInputOnFormActivity()
        {
            DisplayName = "Show Input On Form";
            ButtonName = new InArgument<string>("OK");
            IsPassword = new InArgument<bool>(false);
            AllowNull = new InArgument<bool>(true);
        }

        [DisplayName("Title")]
        [Category("Input.A")]
        public InArgument<string> Title { get; set; }

        [RequiredArgument]
        [DisplayName("Message")]
        [Category("Input.A")]
        public InArgument<string> Message { get; set; }

        [DisplayName("Is Password")]
        [Description("When true, shows a single-line text box with password mask (**). When false, shows a multiline text area. Default: false.")]
        [Category("Input.B")]
        public InArgument<bool> IsPassword { get; set; }

        [DisplayName("Button Name")]
        [Description("Confirm button caption. Default: OK.")]
        [Category("Input.B")]
        public InArgument<string> ButtonName { get; set; }

        [DisplayName("Allow Null")]
        [Description("When false, null input (close X without OK) throws. Default: true.")]
        [Category("Input.C")]
        public InArgument<bool> AllowNull { get; set; }

        [DisplayName("Input Text")]
        [Category("Output")]
        public OutArgument<string> InputText { get; set; }

        protected override string Execute(CodeActivityContext context)
        {
            string input = FormDialogAccess.ShowInput(
                context,
                Title == null ? null : Title.Get(context),
                Message.Get(context),
                IsPassword.Get(context),
                ButtonName == null ? null : ButtonName.Get(context),
                AllowNull.Get(context));
            InputText?.Set(context, input);
            return input;
        }
    }
}
