using System.Activities;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(BasicSimpleActivityDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get All Windows Credentials")]
    [Description("Enumerate Windows Generic credentials as Dict[address, Dict[username/password]].")]
    public sealed class GetAllWindowsCredentialsActivity
        : CodeActivity<Dictionary<string, Dictionary<string, string>>>,
          System.Activities.Presentation.IActivityTemplateFactory
    {
        public GetAllWindowsCredentialsActivity()
        {
            DisplayName = "Get All Windows Credentials";
        }

        [DisplayName("Credentials")]
        [Description("Key = Internet or network address; Value = { username, password }.")]
        [Category("Output")]
        public OutArgument<Dictionary<string, Dictionary<string, string>>> Credentials { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new GetAllWindowsCredentialsActivity();
        }

        protected override Dictionary<string, Dictionary<string, string>> Execute(CodeActivityContext context)
        {
            Dictionary<string, Dictionary<string, string>> result =
                WindowsCredentialManager.ReadAllGenericCredentials();
            Credentials?.Set(context, result);
            return result;
        }
    }
}
