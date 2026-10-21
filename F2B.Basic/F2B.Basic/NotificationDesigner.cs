using System;
using System.Activities.Presentation;
using F2B.OpenRpa.Design;
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
    public sealed class NotificationDesigner : ActivityDesigner
    {
        private readonly Border _messageBorder;
        private readonly ComboBox _logMessageCombo;
        private bool _syncingLogMessage;

        public NotificationDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
            };

            var panel = new StackPanel();
            panel.Children.Add(CreateMessageRow(out _messageBorder));
            panel.Children.Add(CreateLogMessageRow(out _logMessageCombo));

            border.Child = panel;
            ActivityDesignerCollapseHelper.Attach(this, border);
            Loaded += OnLoaded;
        }

        private static FrameworkElement CreateMessageRow(out Border editorBorder)
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = "Message",
                Width = 90,
                VerticalAlignment = VerticalAlignment.Center
            });

            var expressionTextBox = new ExpressionTextBox
            {
                Width = 220,
                MaxWidth = 220,
                HintText = "Notification text",
                ExpressionType = typeof(string),
                MinLines = 1,
                MaxLines = 2
            };
            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.ExpressionProperty, new Binding("ModelItem.Message")
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

        private FrameworkElement CreateLogMessageRow(out ComboBox combo)
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = "Log Message",
                Width = 90,
                VerticalAlignment = VerticalAlignment.Center
            });

            combo = new ComboBox
            {
                Margin = new Thickness(4, 0, 0, 0),
                Width = 220,
                MaxWidth = 220,
                ItemsSource = new[] { "INFO", "WARN", "ERROR", "NONE" }
            };
            combo.SelectionChanged += OnLogMessageSelectionChanged;
            row.Children.Add(combo);
            return row;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            SyncLogMessageCombo();
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorder();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                string.Equals(e.PropertyName, "Message", StringComparison.Ordinal))
            {
                Dispatcher.BeginInvoke(new Action(RefreshRequiredBorder), DispatcherPriority.Background);
            }

            if (string.IsNullOrEmpty(e.PropertyName) ||
                string.Equals(e.PropertyName, "LogMessage", StringComparison.Ordinal))
            {
                Dispatcher.BeginInvoke(new Action(SyncLogMessageCombo), DispatcherPriority.Background);
            }
        }

        private void OnLogMessageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingLogMessage || ModelItem == null || _logMessageCombo.SelectedItem == null)
            {
                return;
            }

            string selected = _logMessageCombo.SelectedItem.ToString();
            string current = ReadLogMessage(ModelItem);
            if (string.Equals(current, selected, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ModelItem.Properties["LogMessage"].SetValue(selected);
        }

        private void SyncLogMessageCombo()
        {
            if (ModelItem == null)
            {
                return;
            }

            string level = ReadLogMessage(ModelItem);
            _syncingLogMessage = true;
            _logMessageCombo.SelectedItem = level;
            _syncingLogMessage = false;
        }

        private static string ReadLogMessage(ModelItem modelItem)
        {
            object value = modelItem?.Properties["LogMessage"]?.ComputedValue;
            string text = value as string;
            if (string.IsNullOrWhiteSpace(text))
            {
                return "INFO";
            }

            text = text.Trim().ToUpperInvariant();
            if (text == "INFO" || text == "WARN" || text == "ERROR" || text == "NONE")
            {
                return text;
            }

            return "INFO";
        }

        private void RefreshRequiredBorder()
        {
            var property = ModelItem?.Properties["Message"];
            bool filled = property != null && property.IsSet && property.Value != null;
            if (filled)
            {
                _messageBorder.BorderBrush = Brushes.Transparent;
                _messageBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                _messageBorder.BorderBrush = Brushes.Red;
                _messageBorder.BorderThickness = new Thickness(1);
            }
        }
    }
}
