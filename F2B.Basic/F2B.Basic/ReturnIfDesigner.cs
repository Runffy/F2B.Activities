using System;
using System.Activities.Presentation;
using System.Activities.Presentation.View;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using F2B.OpenRpa.Design;

namespace F2B.Basic
{
    public sealed class ReturnIfDesigner : ActivityDesigner
    {
        private readonly Border _conditionEditorBorder;
        private readonly ExpressionTextBox _conditionExpressionBox;

        public ReturnIfDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
                MinWidth = 280
            };

            var panel = new StackPanel();
            panel.Children.Add(BasicDesignerShared.CreateLabeledExpressionEditor(
                "Condition",
                "ModelItem.Condition",
                typeof(bool),
                "Boolean expression",
                out _conditionEditorBorder,
                out _conditionExpressionBox));
            panel.Children.Add(new TextBlock
            {
                Text = "Execute Finally: configure in Property Grid",
                FontSize = 10,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 6, 0, 0)
            });

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
                _conditionEditorBorder,
                BasicDesignerShared.IsArgumentFilled(ModelItem, "Condition", _conditionExpressionBox));
        }
    }
}
