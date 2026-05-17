using OpenRPA.Interfaces;
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
    public sealed class StartFileDesigner : ActivityDesigner
    {
        private readonly ComboBox _waitForExitComboBox;
        private readonly ComboBox _showWindowComboBox;
        private readonly Border _pathEditorBorder;
        private readonly ExpressionTextBox _pathExpressionBox;

        public StartFileDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
            };

            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = "Start File",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });

            panel.Children.Add(CreateLabeledExpressionEditor("Path", "ModelItem.Path", typeof(string), "File / folder / URL", out _pathEditorBorder, out _pathExpressionBox));
            panel.Children.Add(CreateLabeledExpressionEditor("Operation", "ModelItem.Operation", typeof(string), "open"));
            panel.Children.Add(CreateLabeledExpressionEditor("Arguments", "ModelItem.Arguments", typeof(string), "Optional args"));
            panel.Children.Add(CreateLabeledExpressionEditor("Working dir", "ModelItem.WorkingDirectory", typeof(string), "Optional"));
            panel.Children.Add(CreateLabeledBooleanDropdown("Wait for exit", out _waitForExitComboBox));
            panel.Children.Add(CreateLabeledBooleanDropdown("Show window", out _showWindowComboBox));
            panel.Children.Add(CreateLabeledExpressionEditor("Result", "ModelItem.Result", typeof(string), "Result string", "Out"));

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
                MinWidth = 200,
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

        private static FrameworkElement CreateLabeledBooleanDropdown(string label, out ComboBox combo)
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = label,
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center
            });

            var localCombo = new ComboBox
            {
                Margin = new Thickness(4, 0, 0, 0),
                MinWidth = 200,
                ItemsSource = new[] { "True", "False" }
            };

            row.Children.Add(localCombo);
            combo = localCombo;
            return row;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            _waitForExitComboBox.SelectionChanged += OnWaitForExitSelectionChanged;
            _showWindowComboBox.SelectionChanged += OnShowWindowSelectionChanged;

            _waitForExitComboBox.SelectedItem = GetBoolString("WaitForExit", false);
            _showWindowComboBox.SelectedItem = GetBoolString("ShowWindow", true);

            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void OnWaitForExitSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelItem == null || _waitForExitComboBox.SelectedItem == null)
            {
                return;
            }

            bool value = string.Equals(_waitForExitComboBox.SelectedItem.ToString(), "True", StringComparison.OrdinalIgnoreCase);
            ModelItem.Properties["WaitForExit"].SetValue(new InArgument<bool>(value));
        }

        private void OnShowWindowSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ModelItem == null || _showWindowComboBox.SelectedItem == null)
            {
                return;
            }

            bool value = string.Equals(_showWindowComboBox.SelectedItem.ToString(), "True", StringComparison.OrdinalIgnoreCase);
            ModelItem.Properties["ShowWindow"].SetValue(new InArgument<bool>(value));
        }

        private string GetBoolString(string propertyName, bool defaultValue)
        {
            try
            {
                bool value = ModelItem.GetValue<bool>(propertyName);
                return value ? "True" : "False";
            }
            catch
            {
                return defaultValue ? "True" : "False";
            }
        }

        private void RefreshRequiredBorders()
        {
            SetRequiredBorder(_pathEditorBorder, IsArgumentFilled(ModelItem, "Path", _pathExpressionBox));
        }

        private static bool IsArgumentFilled(ModelItem modelItem, string propertyName, ExpressionTextBox editor)
        {
            var property = modelItem?.Properties[propertyName];
            if (property == null)
            {
                return HasEditorInput(editor);
            }

            if (property.IsSet)
            {
                return true;
            }

            if (property.Value == null)
            {
                return HasEditorInput(editor);
            }

            var expressionProperty = property.Value.Properties["Expression"];
            if (expressionProperty == null)
            {
                return HasEditorInput(editor);
            }

            if (expressionProperty.Value == null && expressionProperty.ComputedValue == null)
            {
                return HasEditorInput(editor);
            }

            if (expressionProperty.ComputedValue is string text)
            {
                return !string.IsNullOrWhiteSpace(text);
            }

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
            {
                return;
            }

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
