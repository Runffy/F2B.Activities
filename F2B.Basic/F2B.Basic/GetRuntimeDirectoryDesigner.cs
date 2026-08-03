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
    public sealed class GetRuntimeDirectoryDesigner : ActivityDesigner
    {
        private readonly ComboBox _modeCombo;
        private bool _syncing;

        public GetRuntimeDirectoryDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
            };

            var panel = new StackPanel();
            panel.Children.Add(CreateModeRow(out _modeCombo));
            panel.Children.Add(CreateLabeledOutExpressionEditor(
                "Runtime Directory",
                "RuntimeDirectory",
                typeof(string),
                "Output path"));

            border.Child = panel;
            Content = border;
            Loaded += OnLoaded;
        }

        private FrameworkElement CreateModeRow(out ComboBox combo)
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = "Mode",
                Width = 120,
                VerticalAlignment = VerticalAlignment.Center
            });

            combo = new ComboBox
            {
                Margin = new Thickness(4, 0, 0, 0),
                Width = 200,
                MaxWidth = 200,
                ItemsSource = new[]
                {
                    RuntimeDirectoryMode.Year,
                    RuntimeDirectoryMode.Month,
                    RuntimeDirectoryMode.Day,
                    RuntimeDirectoryMode.Hour,
                    RuntimeDirectoryMode.Minute,
                    RuntimeDirectoryMode.Second
                }
            };
            combo.SelectionChanged += OnModeSelectionChanged;
            row.Children.Add(combo);
            return row;
        }

        private static FrameworkElement CreateLabeledOutExpressionEditor(
            string label,
            string pathToArgument,
            Type expressionType,
            string hint)
        {
            var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 2, 0, 2) };
            row.Children.Add(new TextBlock
            {
                Text = label,
                Width = 120,
                VerticalAlignment = VerticalAlignment.Center
            });

            var expressionTextBox = new ExpressionTextBox
            {
                Margin = new Thickness(4, 0, 0, 0),
                Width = 200,
                MaxWidth = 200,
                HintText = hint,
                ExpressionType = expressionType,
                UseLocationExpression = true,
                PathToArgument = pathToArgument,
                MinLines = 1,
                MaxLines = 1
            };

            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(expressionTextBox, ExpressionTextBox.ExpressionProperty, new Binding("ModelItem." + pathToArgument)
            {
                Mode = BindingMode.TwoWay,
                Converter = new ArgumentToExpressionConverter(),
                ConverterParameter = "Out"
            });

            row.Children.Add(expressionTextBox);
            return row;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            SyncModeCombo();
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) ||
                string.Equals(e.PropertyName, "Mode", StringComparison.Ordinal))
            {
                Dispatcher.BeginInvoke(new Action(SyncModeCombo), DispatcherPriority.Background);
            }
        }

        private void OnModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncing || ModelItem == null || !(_modeCombo.SelectedItem is RuntimeDirectoryMode mode))
            {
                return;
            }

            ModelItem.Properties["Mode"].SetValue(mode);
        }

        private void SyncModeCombo()
        {
            if (ModelItem == null)
            {
                return;
            }

            var mode = ReadMode(ModelItem);
            _syncing = true;
            _modeCombo.SelectedItem = mode;
            _syncing = false;
        }

        private static RuntimeDirectoryMode ReadMode(ModelItem modelItem)
        {
            object value = modelItem?.Properties["Mode"]?.ComputedValue;
            if (value is RuntimeDirectoryMode mode)
            {
                return mode;
            }

            return RuntimeDirectoryMode.Second;
        }
    }
}
