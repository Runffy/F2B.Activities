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
    public sealed class NumberIncrementDesigner : ActivityDesigner
    {
        private const string LabelSharedSizeGroup = "NumberIncrementLabelColumn";

        private readonly ComboBox _operationCombo;
        private readonly ComboBox _numberTypeCombo;
        private readonly FrameworkElement _intRow;
        private readonly FrameworkElement _doubleRow;
        private readonly Border _intVariableBorder;
        private readonly Border _doubleVariableBorder;
        private readonly ExpressionTextBox _intVariableBox;
        private readonly ExpressionTextBox _doubleVariableBox;
        private bool _syncing;

        public NumberIncrementDesigner()
        {
            var border = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
            };

            var panel = new StackPanel();
            Grid.SetIsSharedSizeScope(panel, true);

            panel.Children.Add(CreateEnumComboRow("Operation", new[] { "Increment", "Decrement" }, out _operationCombo));
            panel.Children.Add(CreateEnumComboRow("Number Type", new[] { "Int", "Double" }, out _numberTypeCombo));

            _intRow = CreateVariableStepBlock(
                "Int Variable",
                "ModelItem.IntVariable",
                typeof(int),
                "Int Step",
                "ModelItem.IntStep",
                out _intVariableBorder,
                out _intVariableBox);
            panel.Children.Add(_intRow);

            _doubleRow = CreateVariableStepBlock(
                "Double Variable",
                "ModelItem.DoubleVariable",
                typeof(double),
                "Double Step",
                "ModelItem.DoubleStep",
                out _doubleVariableBorder,
                out _doubleVariableBox);
            panel.Children.Add(_doubleRow);

            border.Child = panel;
            Content = border;
            Loaded += OnLoaded;
        }

        private FrameworkElement CreateEnumComboRow(string label, string[] items, out ComboBox combo)
        {
            var row = CreateLabelEditorGrid(label, out Border host);
            combo = new ComboBox
            {
                Width = 200,
                MaxWidth = 200,
                ItemsSource = items
            };
            combo.SelectionChanged += OnEnumComboSelectionChanged;
            host.Child = combo;
            return row;
        }

        private FrameworkElement CreateVariableStepBlock(
            string variableLabel,
            string variablePath,
            Type variableType,
            string stepLabel,
            string stepPath,
            out Border variableBorder,
            out ExpressionTextBox variableBox)
        {
            var block = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            block.Children.Add(CreateLabeledExpressionEditor(
                variableLabel,
                variablePath,
                variableType,
                "Workflow variable",
                isLocationExpression: true,
                converterParameter: "InOut",
                out variableBorder,
                out variableBox));
            block.Children.Add(CreateLabeledExpressionEditor(
                stepLabel,
                stepPath,
                variableType,
                "Step (default 1)",
                isLocationExpression: false,
                converterParameter: "In",
                out _,
                out _));
            return block;
        }

        private FrameworkElement CreateLabeledExpressionEditor(
            string label,
            string bindingPath,
            Type expressionType,
            string hint,
            bool isLocationExpression,
            string converterParameter,
            out Border editorBorder,
            out ExpressionTextBox expressionTextBox)
        {
            var row = CreateLabelEditorGrid(label, out editorBorder);

            expressionTextBox = new ExpressionTextBox
            {
                Width = 200,
                MaxWidth = 200,
                HintText = hint,
                ExpressionType = expressionType,
                UseLocationExpression = isLocationExpression,
                PathToArgument = bindingPath.StartsWith("ModelItem.", StringComparison.Ordinal)
                    ? bindingPath.Substring("ModelItem.".Length)
                    : bindingPath,
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

            editorBorder.Child = expressionTextBox;
            return row;
        }

        private static Grid CreateLabelEditorGrid(string label, out Border editorHost)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto,
                SharedSizeGroup = LabelSharedSizeGroup
            });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelText = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                TextTrimming = TextTrimming.None
            };
            Grid.SetColumn(labelText, 0);
            row.Children.Add(labelText);

            editorHost = new Border
            {
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetColumn(editorHost, 1);
            row.Children.Add(editorHost);
            return row;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            _syncing = true;
            _operationCombo.SelectedItem = ReadEnumDisplay(ModelItem, "Operation", NumberOperation.Increment);
            _numberTypeCombo.SelectedItem = ReadEnumDisplay(ModelItem, "NumberType", NumberValueType.Int);
            _syncing = false;

            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshTypeVisibility();
            RefreshRequiredBorders();
            SyncDisplayName();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Operation" || e.PropertyName == "DisplayName")
            {
                Dispatcher.BeginInvoke(new Action(SyncDisplayNameFromModel), DispatcherPriority.Background);
            }

            if (e.PropertyName == "NumberType" || e.PropertyName == "IntVariable" || e.PropertyName == "DoubleVariable")
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RefreshTypeVisibility();
                    RefreshRequiredBorders();
                }), DispatcherPriority.Background);
            }
        }

        private void OnEnumComboSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncing || ModelItem == null || !(sender is ComboBox combo) || combo.SelectedItem == null)
            {
                return;
            }

            string selected = combo.SelectedItem.ToString();
            if (ReferenceEquals(combo, _operationCombo))
            {
                var operation = string.Equals(selected, "Decrement", StringComparison.OrdinalIgnoreCase)
                    ? NumberOperation.Decrement
                    : NumberOperation.Increment;
                ModelItem.Properties["Operation"].SetValue(operation);
                SyncDisplayName();
                return;
            }

            if (ReferenceEquals(combo, _numberTypeCombo))
            {
                var numberType = string.Equals(selected, "Double", StringComparison.OrdinalIgnoreCase)
                    ? NumberValueType.Double
                    : NumberValueType.Int;
                ModelItem.Properties["NumberType"].SetValue(numberType);
                RefreshTypeVisibility();
                RefreshRequiredBorders();
            }
        }

        private void SyncDisplayName()
        {
            if (ModelItem == null)
            {
                return;
            }

            var operation = ReadEnumValue(ModelItem, "Operation", NumberOperation.Increment);
            string displayName = NumberIncrementActivity.GetDisplayNameFor(operation);
            object current = ModelItem.Properties["DisplayName"]?.ComputedValue;
            if (!string.Equals(current as string, displayName, StringComparison.Ordinal))
            {
                ModelItem.Properties["DisplayName"].SetValue(displayName);
            }
        }

        private void SyncDisplayNameFromModel()
        {
            if (ModelItem == null)
            {
                return;
            }

            _syncing = true;
            _operationCombo.SelectedItem = ReadEnumDisplay(ModelItem, "Operation", NumberOperation.Increment);
            _syncing = false;
            SyncDisplayName();
        }

        private void RefreshTypeVisibility()
        {
            bool isDouble = ReadEnumValue(ModelItem, "NumberType", NumberValueType.Int) == NumberValueType.Double;
            _intRow.Visibility = isDouble ? Visibility.Collapsed : Visibility.Visible;
            _doubleRow.Visibility = isDouble ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshRequiredBorders()
        {
            bool isDouble = ReadEnumValue(ModelItem, "NumberType", NumberValueType.Int) == NumberValueType.Double;
            if (isDouble)
            {
                SetRequiredBorder(_doubleVariableBorder, IsArgumentFilled(ModelItem, "DoubleVariable", _doubleVariableBox));
                SetRequiredBorder(_intVariableBorder, true);
            }
            else
            {
                SetRequiredBorder(_intVariableBorder, IsArgumentFilled(ModelItem, "IntVariable", _intVariableBox));
                SetRequiredBorder(_doubleVariableBorder, true);
            }
        }

        private static string ReadEnumDisplay<TEnum>(ModelItem modelItem, string propertyName, TEnum fallback)
            where TEnum : struct
        {
            return ReadEnumValue(modelItem, propertyName, fallback).ToString();
        }

        private static TEnum ReadEnumValue<TEnum>(ModelItem modelItem, string propertyName, TEnum fallback)
            where TEnum : struct
        {
            try
            {
                object value = modelItem?.Properties[propertyName]?.ComputedValue;
                if (value is TEnum typed)
                {
                    return typed;
                }

                if (value != null && Enum.TryParse(value.ToString(), true, out TEnum parsed))
                {
                    return parsed;
                }
            }
            catch
            {
                // Keep designer resilient.
            }

            return fallback;
        }

        private static bool IsArgumentFilled(ModelItem modelItem, string propertyName, ExpressionTextBox editor)
        {
            var property = modelItem?.Properties[propertyName];
            if (property == null)
            {
                return false;
            }

            if (!property.IsSet || property.Value == null)
            {
                return false;
            }

            var expressionProperty = property.Value.Properties["Expression"];
            if (expressionProperty == null)
            {
                return false;
            }

            if (expressionProperty.Value == null && expressionProperty.ComputedValue == null)
            {
                return false;
            }

            return true;
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
