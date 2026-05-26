using OpenRPA.Interfaces;
using System;
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
    public sealed class LogMessageDesigner : ActivityDesigner
    {
        private readonly ComboBox levelComboBox;
        private readonly Border messageEditorBorder;
        private readonly ExpressionTextBox messageExpressionBox;
        private bool isSyncingLevel;

        public LogMessageDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
            };

            var panel = new StackPanel();
            panel.Children.Add(CreateLabeledLevelDropdown(out levelComboBox));

            panel.Children.Add(CreateLabeledExpressionEditor(
                "Message",
                "ModelItem.Message",
                typeof(object),
                "Any object",
                out messageEditorBorder,
                out messageExpressionBox));

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
            out ExpressionTextBox expressionTextBox)
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = label,
                Width = 60,
                VerticalAlignment = VerticalAlignment.Center
            });

            expressionTextBox = new ExpressionTextBox
            {
                Width = 200,
                MaxWidth = 200,
                HintText = hint,
                ExpressionType = expressionType,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };

            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.ExpressionProperty, new Binding(bindingPath)
            {
                Mode = BindingMode.TwoWay,
                Converter = new ArgumentToExpressionConverter(),
                ConverterParameter = "In"
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

        private static FrameworkElement CreateLabeledLevelDropdown(out ComboBox combo)
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = "Level",
                Width = 60,
                VerticalAlignment = VerticalAlignment.Center
            });

            var localCombo = new ComboBox
            {
                Margin = new Thickness(4, 0, 0, 0),
                Width = 200,
                MaxWidth = 200,
                ItemsSource = new[] { "INFO", "WARN", "ERROR" }
            };

            localCombo.SelectionChanged += (s, e) =>
            {
                if (!(localCombo.Tag is LogMessageDesigner owner) || owner.ModelItem == null || localCombo.SelectedItem == null)
                {
                    return;
                }

                if (owner.isSyncingLevel)
                {
                    return;
                }

                string selectedLevel = localCombo.SelectedItem.ToString();
                string currentLevel = null;
                try
                {
                    currentLevel = owner.ModelItem.GetValue<string>("Level");
                }
                catch
                {
                    // Keep designer resilient; fallback to write value below.
                }

                if (string.Equals((currentLevel ?? string.Empty).Trim(), selectedLevel, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                owner.ModelItem.Properties["Level"].SetValue(new global::System.Activities.InArgument<string>(selectedLevel));
            };
            localCombo.Tag = null;

            row.Children.Add(localCombo);
            combo = localCombo;
            return row;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            levelComboBox.Tag = this;
            string current = "INFO";
            try
            {
                var modelValue = ModelItem.GetValue<string>("Level");
                if (!string.IsNullOrWhiteSpace(modelValue))
                {
                    current = modelValue.Trim().ToUpperInvariant();
                }
            }
            catch
            {
                current = "INFO";
            }

            if (current != "INFO" && current != "WARN" && current != "ERROR")
            {
                current = "INFO";
            }

            isSyncingLevel = true;
            levelComboBox.SelectedItem = current;
            isSyncingLevel = false;
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void RefreshRequiredBorders()
        {
            SetRequiredBorder(messageEditorBorder, IsArgumentFilled(ModelItem, "Message", messageExpressionBox));
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
