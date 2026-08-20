using F2B.OpenRpa.Design;
using System;
using System.Activities.Presentation;
using System.Activities.Presentation.View;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace F2B.Basic
{
    /// <summary>
    /// Canvas editors for Credential name / Username / Password.
    /// </summary>
    public sealed class SetWindowsCredentialDesigner : ActivityDesigner
    {
        private readonly Border _credentialNameEditorBorder;
        private readonly Border _usernameEditorBorder;
        private readonly Border _passwordEditorBorder;
        private readonly ExpressionTextBox _credentialNameExpressionBox;
        private readonly ExpressionTextBox _usernameExpressionBox;
        private readonly ExpressionTextBox _passwordExpressionBox;

        public SetWindowsCredentialDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
            };

            var panel = new StackPanel();
            panel.Children.Add(BasicDesignerShared.CreateLabeledExpressionEditor(
                "Name",
                "ModelItem.CredentialName",
                typeof(string),
                "Credential target name",
                out _credentialNameEditorBorder,
                out _credentialNameExpressionBox));
            panel.Children.Add(BasicDesignerShared.CreateLabeledExpressionEditor(
                "Username",
                "ModelItem.Username",
                typeof(string),
                "Username",
                out _usernameEditorBorder,
                out _usernameExpressionBox));
            panel.Children.Add(BasicDesignerShared.CreateLabeledExpressionEditor(
                "Password",
                "ModelItem.Password",
                typeof(string),
                "Password",
                out _passwordEditorBorder,
                out _passwordExpressionBox));

            border.Child = panel;
            ActivityDesignerCollapseHelper.Attach(this, border);
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void RefreshRequiredBorders()
        {
            BasicDesignerShared.SetRequiredBorder(
                _credentialNameEditorBorder,
                BasicDesignerShared.IsArgumentFilled(ModelItem, "CredentialName", _credentialNameExpressionBox));
            BasicDesignerShared.SetRequiredBorder(
                _usernameEditorBorder,
                BasicDesignerShared.IsArgumentFilled(ModelItem, "Username", _usernameExpressionBox));
            BasicDesignerShared.SetRequiredBorder(
                _passwordEditorBorder,
                BasicDesignerShared.IsArgumentFilled(ModelItem, "Password", _passwordExpressionBox));
        }
    }
}
