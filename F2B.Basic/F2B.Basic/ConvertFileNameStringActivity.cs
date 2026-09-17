using System.Activities;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(BasicSimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [ToolboxPath("Misc")]
    [DisplayName("Convert File Name String")]
    [Description(
        "Replace characters illegal in a Windows file name with a user-specified string. "
        + "Expression: F2B.Basic.FileName.ConvertFileNameString(in_string, replace_string)")]
    public sealed class ConvertFileNameStringActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public ConvertFileNameStringActivity()
        {
            DisplayName = "Convert File Name String";
            ReplaceString = new InArgument<string>("_");
        }

        [RequiredArgument]
        [DisplayName("In String")]
        [Description("Candidate file name that may contain illegal characters (\\ / : * ? \" < > | and control chars).")]
        [Category("Input.A")]
        public InArgument<string> InString { get; set; }

        [DisplayName("Replace String")]
        [Description("Replacement for each illegal character. Must itself be legal in a file name. Empty string strips illegal characters. Default: \"_\".")]
        [Category("Input.A")]
        public InArgument<string> ReplaceString { get; set; }

        [DisplayName("Out String")]
        [Description("Sanitized file name.")]
        [Category("Output")]
        public OutArgument<string> OutString { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new ConvertFileNameStringActivity
            {
                ReplaceString = new InArgument<string>("_")
            };
        }

        protected override void Execute(CodeActivityContext context)
        {
            string input = InString.Get(context) ?? string.Empty;
            string replace = ReplaceString.Get(context);
            if (replace == null)
            {
                replace = "_";
            }

            string result = FileName.ConvertFileNameString(input, replace);
            OutString?.Set(context, result);
        }
    }
}
