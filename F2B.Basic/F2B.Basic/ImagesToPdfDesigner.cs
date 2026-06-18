using System;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace F2B.Basic
{
    public sealed class ImagesToPdfDesigner : ActivityDesigner
    {
        private readonly Border _imagePathsEditorBorder;
        private readonly Border _outputPdfPathEditorBorder;
        private readonly ExpressionTextBox _imagePathsExpressionBox;
        private readonly ExpressionTextBox _outputPdfPathExpressionBox;

        public ImagesToPdfDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
            };

            var panel = new StackPanel();
            panel.Children.Add(CreateLabeledExpressionEditor(
                "Images",
                "ModelItem.ImagePaths",
                typeof(string[]),
                "string[] paths",
                out _imagePathsEditorBorder,
                out _imagePathsExpressionBox));
            panel.Children.Add(CreateLabeledExpressionEditor(
                "PDF path",
                "ModelItem.OutputPdfPath",
                typeof(string),
                "Output .pdf path",
                out _outputPdfPathEditorBorder,
                out _outputPdfPathExpressionBox));

            border.Child = panel;
            Content = border;
            Loaded += OnLoaded;
        }

        private static FrameworkElement CreateLabeledExpressionEditor(
            string label,
            string bindingPath,
            Type expressionType,
            string hint,
            out Border editorBorder,
            out ExpressionTextBox expressionTextBox,
            string converterParameter = "In")
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = label,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            });

            expressionTextBox = new ExpressionTextBox
            {
                Margin = new Thickness(0, 0, 0, 0),
                Width = 200,
                MaxWidth = 200,
                HintText = hint,
                ExpressionType = expressionType,
                MinLines = 1,
                MaxLines = 1
            };

            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.ExpressionProperty, new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay,
                Converter = new ArgumentToExpressionConverter(),
                ConverterParameter = converterParameter
            });

            editorBorder = new Border
            {
                Margin = new Thickness(4, 0, 0, 0),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = expressionTextBox
            };

            row.Children.Add(editorBorder);
            return row;
        }

        private static FrameworkElement CreateLabeledExpressionEditor(
            string label,
            string bindingPath,
            Type expressionType,
            string hint,
            string converterParameter = "In")
        {
            return CreateLabeledExpressionEditor(label, bindingPath, expressionType, hint, out _, out _, converterParameter);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
                return;

            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void RefreshRequiredBorders()
        {
            SetRequiredBorder(_imagePathsEditorBorder, IsArgumentFilled(ModelItem, "ImagePaths", _imagePathsExpressionBox));
            SetRequiredBorder(_outputPdfPathEditorBorder, IsArgumentFilled(ModelItem, "OutputPdfPath", _outputPdfPathExpressionBox));
        }

        private static bool IsArgumentFilled(ModelItem modelItem, string propertyName, ExpressionTextBox editor)
        {
            var property = modelItem?.Properties[propertyName];
            if (property == null)
                return HasEditorInput(editor);

            if (property.IsSet)
                return true;

            if (property.Value == null)
                return HasEditorInput(editor);

            var expressionProperty = property.Value.Properties["Expression"];
            if (expressionProperty == null)
                return HasEditorInput(editor);

            if (expressionProperty.Value == null && expressionProperty.ComputedValue == null)
                return HasEditorInput(editor);

            if (expressionProperty.ComputedValue is string text)
                return !string.IsNullOrWhiteSpace(text);

            if (expressionProperty.Value != null)
            {
                string expressionText = expressionProperty.Value.ToString();
                return !string.IsNullOrWhiteSpace(expressionText);
            }

            return HasEditorInput(editor);
        }

        private static bool HasEditorInput(ExpressionTextBox editor)
        {
            return editor != null && editor.Expression != null;
        }

        private static void SetRequiredBorder(Border border, bool filled)
        {
            if (border == null)
                return;

            if (filled)
            {
                border.BorderBrush = Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
                return;
            }

            border.BorderBrush = Brushes.Red;
            border.BorderThickness = new Thickness(1);
        }
    }
}
