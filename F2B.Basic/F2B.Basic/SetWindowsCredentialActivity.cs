using System;
using System.Activities;
using System.ComponentModel;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(SetWindowsCredentialDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Set Windows Credential")]
    [Description("Creates or updates a Generic credential in Windows Credential Manager.")]
    public sealed class SetWindowsCredentialActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        public SetWindowsCredentialActivity()
        {
            DisplayName = "Set Windows Credential";
        }

        [RequiredArgument]
        [DisplayName("Credential name")]
        [Description("Generic credential target name (same name used by Get Windows Credential / cmdkey).")]
        [Category("Input.A")]
        public InArgument<string> CredentialName { get; set; }

        [RequiredArgument]
        [DisplayName("Username")]
        [Category("Input.B")]
        public InArgument<string> Username { get; set; }

        [RequiredArgument]
        [DisplayName("Password")]
        [Description("Plain-text password to store. Do not log this value.")]
        [Category("Input.C")]
        public InArgument<string> Password { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new SetWindowsCredentialActivity();
        }

        protected override void Execute(CodeActivityContext context)
        {
            string credentialName = (CredentialName.Get(context) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(credentialName))
            {
                throw new ArgumentException("Credential name is required.", nameof(CredentialName));
            }

            string username = Username.Get(context) ?? string.Empty;
            string password = Password.Get(context) ?? string.Empty;

            WindowsCredentialManager.WriteGenericCredential(credentialName, username, password);
        }
    }
}
