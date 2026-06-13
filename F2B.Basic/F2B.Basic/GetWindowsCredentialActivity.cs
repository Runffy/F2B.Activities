using System;
using System.Activities;
using System.ComponentModel;
using System.Net;
using System.Security;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(GetWindowsCredentialDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Get Windows Credential")]
    [Description("Reads a Generic credential from Windows Credential Manager by name.")]
    public sealed class GetWindowsCredentialActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public GetWindowsCredentialActivity()
        {
            DisplayName = "Get Windows Credential";
        }

        [RequiredArgument]
        [DisplayName("Credential name")]
        [Description("Generic credential name. Use cmdkey /list to confirm the exact target name.")]
        [Category("Input.A")]
        public InArgument<string> CredentialName { get; set; }

        [DisplayName("Username")]
        [Category("Output")]
        public OutArgument<string> Username { get; set; }

        [DisplayName("Password")]
        [Description("Plain-text password. Do not log this value.")]
        [Category("Output")]
        public OutArgument<string> Password { get; set; }

        [DisplayName("Secure password")]
        [Description("Password as SecureString.")]
        [Category("Output")]
        public OutArgument<SecureString> SecurePassword { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new GetWindowsCredentialActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            string credentialName = (CredentialName.Get(context) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(credentialName))
            {
                throw new ArgumentException("Credential name is required.", nameof(CredentialName));
            }

            WindowsCredential credential = WindowsCredentialManager.ReadGenericCredential(credentialName);
            string password = credential.Password ?? string.Empty;
            SecureString securePassword = new NetworkCredential(string.Empty, password).SecurePassword;

            Username.Set(context, credential.UserName ?? string.Empty);
            Password.Set(context, password);
            SecurePassword.Set(context, securePassword);
        }
    }
}
