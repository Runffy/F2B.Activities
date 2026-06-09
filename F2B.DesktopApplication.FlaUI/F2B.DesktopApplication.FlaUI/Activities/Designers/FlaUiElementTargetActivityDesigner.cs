using System;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace F2B.DesktopApplication.FlaUI
{
    public sealed class FlaUiElementTargetActivityDesigner : ActivityDesigner
    {
        private const double RowLabelMinWidth = 78;
        private const double EditorMinWidth = 190;
        private const double RowSpacing = 4;

        private static readonly HashSet<string> TargetManagedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TargetType",
            "InputWindow",
            "Selector",
            "Element",
            "Timeout",
            "Interval",
            "DelayBefore"
        };

        private readonly Border _rootPanel;
        private readonly StackPanel _bodyPanel;
        private readonly ComboBox _targetTypeComboBox;
        private readonly FrameworkElement _targetTypeRow;
        private readonly FrameworkElement _selectorRow;
        private readonly FrameworkElement _elementRow;
        private readonly Border _selectorEditorBorder;
        private readonly Border _elementEditorBorder;
        private readonly ExpressionTextBox _selectorExpressionBox;
        private readonly ExpressionTextBox _elementExpressionBox;
        private readonly Dictionary<string, Border> _extraEditorBorders = new Dictionary<string, Border>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ExpressionTextBox> _extraEditors = new Dictionary<string, ExpressionTextBox>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _requiredExtraProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool _isSyncingTargetType;
        private bool _initialized;

        public FlaUiElementTargetActivityDesigner()
        {
            var host = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(4)
            };

            _rootPanel = new Border
            {
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 5, 6, 5)
            };

            _bodyPanel = new StackPanel { Orientation = Orientation.Vertical };
            Grid.SetIsSharedSizeScope(_bodyPanel, true);

            _targetTypeComboBox = BuildTargetTypeComboBox();
            _targetTypeComboBox.SelectionChanged += OnTargetTypeSelectionChanged;
            _targetTypeRow = CreateRow("TargetType", _targetTypeComboBox);
            _bodyPanel.Children.Add(_targetTypeRow);

            _selectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _selectorRow = CreateRow("Selector (XML)", _selectorExpressionBox, out _selectorEditorBorder, RowSpacing);
            _bodyPanel.Children.Add(_selectorRow);

            _elementExpressionBox = CreateExpressionTextBox("Element", typeof(UiElement));
            _elementRow = CreateRow("Input Element", _elementExpressionBox, out _elementEditorBorder, RowSpacing);
            _bodyPanel.Children.Add(_elementRow);

            _rootPanel.Child = _bodyPanel;
            host.Children.Add(_rootPanel);
            Content = host;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
                return;

            if (!_initialized)
            {
                BuildExtraRows(ModelItem);
                _initialized = true;
                ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            }

            BindExpressionOwner(_rootPanel, ModelItem);

            var targetType = ReadTargetType(ModelItem);
            SyncTargetTypeCombo(targetType);
            RefreshTargetRows(targetType);
            RefreshRequiredBorders();
        }

        private void BuildExtraRows(ModelItem modelItem)
        {
            var activityType = modelItem.ItemType;
            var argumentProperties = activityType
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.PropertyType.IsGenericType &&
                            p.PropertyType.GetGenericTypeDefinition() == typeof(InArgument<>))
                .Where(p => !TargetManagedProperties.Contains(p.Name))
                .OrderBy(ResolveCategoryOrder)
                .ThenBy(p => p.MetadataToken)
                .ToList();

            foreach (var property in argumentProperties)
            {
                var expressionType = property.PropertyType.GetGenericArguments()[0];
                var displayName = ResolveDisplayName(property);
                var editor = CreateExpressionTextBox(property.Name, expressionType);
                var row = CreateRow(displayName, editor, out var editorBorder, RowSpacing);

                _bodyPanel.Children.Add(row);
                _extraEditors[property.Name] = editor;
                _extraEditorBorders[property.Name] = editorBorder;

                if (property.GetCustomAttribute<RequiredArgumentAttribute>() != null ||
                    IsCustomRequired(activityType, property.Name))
                    _requiredExtraProperties.Add(property.Name);
            }
        }

        private static bool IsCustomRequired(Type activityType, string propertyName)
        {
            if (string.Equals(activityType.Name, "InputTextActivity", StringComparison.Ordinal) &&
                string.Equals(propertyName, "Text", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(activityType.Name, "PressKeysActivity", StringComparison.Ordinal) &&
                string.Equals(propertyName, "Keys", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(activityType.Name, "GetPropertyActivity", StringComparison.Ordinal) &&
                string.Equals(propertyName, "PropertyName", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.Equals(activityType.Name, "TakeScreenshotActivity", StringComparison.Ordinal) &&
                string.Equals(propertyName, "OutputPath", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private void OnTargetTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingTargetType || ModelItem == null)
                return;

            if (_targetTypeComboBox.SelectedItem is ElementTargetType targetType)
            {
                ModelItem.Properties["TargetType"].SetValue(targetType);
                if (ModelItem.GetCurrentValue() != null)
                    TypeDescriptor.Refresh(ModelItem.GetCurrentValue());

                RefreshTargetRows(targetType);
                RefreshRequiredBorders();
            }
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ModelItem == null)
                    return;

                var targetType = ReadTargetType(ModelItem);
                SyncTargetTypeCombo(targetType);
                RefreshTargetRows(targetType);
                RefreshRequiredBorders();
            }), DispatcherPriority.Background);
        }

        private void RefreshTargetRows(ElementTargetType targetType)
        {
            var isElement = targetType == ElementTargetType.Element;
            _selectorRow.Visibility = isElement ? Visibility.Collapsed : Visibility.Visible;
            _elementRow.Visibility = isElement ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
                return;

            var targetType = ReadTargetType(ModelItem);
            var isElement = targetType == ElementTargetType.Element;

            SetRequiredBorder(_elementEditorBorder, isElement,
                IsArgumentFilled(ModelItem, "Element", _elementExpressionBox));
            SetRequiredBorder(_selectorEditorBorder, !isElement,
                IsArgumentFilled(ModelItem, "Selector", _selectorExpressionBox));

            foreach (var propertyName in _requiredExtraProperties)
            {
                if (!_extraEditorBorders.TryGetValue(propertyName, out var border))
                    continue;

                _extraEditors.TryGetValue(propertyName, out var editor);
                SetRequiredBorder(border, required: true, filled: IsArgumentFilled(ModelItem, propertyName, editor));
            }
        }

        private static ElementTargetType ReadTargetType(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["TargetType"];
            if (modelProperty?.ComputedValue is ElementTargetType value)
                return value;

            return ElementTargetType.Selector;
        }

        private void SyncTargetTypeCombo(ElementTargetType targetType)
        {
            _isSyncingTargetType = true;
            _targetTypeComboBox.SelectedItem = targetType;
            _isSyncingTargetType = false;
        }

        private static ComboBox BuildTargetTypeComboBox()
        {
            var comboBox = new ComboBox { IsEditable = false };
            comboBox.Items.Add(ElementTargetType.Selector);
            comboBox.Items.Add(ElementTargetType.Element);
            return comboBox;
        }

        private static int ResolveCategoryOrder(PropertyInfo property)
        {
            var category = property.GetCustomAttribute<CategoryAttribute>();
            if (category == null || string.IsNullOrWhiteSpace(category.Category))
                return 99;

            if (category.Category.StartsWith("Input.", StringComparison.OrdinalIgnoreCase))
            {
                var suffix = category.Category.Substring("Input.".Length);
                if (suffix.Length == 1 && suffix[0] >= 'A' && suffix[0] <= 'Z')
                    return suffix[0] - 'A';
            }

            if (string.Equals(category.Category, "Output", StringComparison.OrdinalIgnoreCase))
                return 100;

            return 50;
        }

        private static string ResolveDisplayName(PropertyInfo property)
        {
            var displayName = property.GetCustomAttribute<DisplayNameAttribute>();
            return displayName != null && !string.IsNullOrWhiteSpace(displayName.DisplayName)
                ? displayName.DisplayName
                : property.Name;
        }

        private static FrameworkElement CreateRow(string label, FrameworkElement editor, double top = 0)
        {
            return CreateRow(label, editor, out _, top);
        }

        private static FrameworkElement CreateRow(string label, FrameworkElement editor, out Border editorBorder, double top = 0)
        {
            NormalizeEditor(editor);

            var row = new Grid { Margin = new Thickness(0, top, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto,
                MinWidth = RowLabelMinWidth,
                SharedSizeGroup = "FlaUiElementTargetLabelColumn"
            });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelTextBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold,
                ToolTip = label
            };
            Grid.SetColumn(labelTextBlock, 0);
            row.Children.Add(labelTextBlock);

            var host = new Border
            {
                Margin = new Thickness(4, 0, 0, 0),
                MinWidth = EditorMinWidth,
                Height = 22,
                HorizontalAlignment = HorizontalAlignment.Left,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Child = editor
            };
            editorBorder = host;
            Grid.SetColumn(host, 1);
            row.Children.Add(host);
            return row;
        }

        private static void NormalizeEditor(FrameworkElement editor)
        {
            editor.VerticalAlignment = VerticalAlignment.Center;
            editor.HorizontalAlignment = HorizontalAlignment.Left;

            if (editor is Control control)
            {
                control.FontSize = 12;
                control.MinHeight = 22;
                control.Height = 22;
            }

            if (editor is ComboBox comboBox)
            {
                comboBox.MinWidth = EditorMinWidth;
                comboBox.Width = EditorMinWidth;
            }

            if (editor is ExpressionTextBox expressionTextBox)
            {
                expressionTextBox.MinWidth = EditorMinWidth;
                expressionTextBox.Width = EditorMinWidth;
                expressionTextBox.MinHeight = 22;
                expressionTextBox.Height = 22;
                expressionTextBox.MaxHeight = 22;
            }
        }

        private static ExpressionTextBox CreateExpressionTextBox(string pathToArgument, Type expressionType)
        {
            var editor = new ExpressionTextBox
            {
                PathToArgument = pathToArgument,
                ExpressionType = expressionType,
                MinLines = 1,
                MaxLines = 1
            };

            BindingOperations.SetBinding(editor, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(editor, ExpressionTextBox.ExpressionProperty, new Binding("ModelItem." + pathToArgument)
            {
                Mode = BindingMode.TwoWay,
                Converter = new ArgumentToExpressionConverter(),
                ConverterParameter = "In"
            });

            return editor;
        }

        private static bool IsArgumentFilled(ModelItem modelItem, string propertyName, ExpressionTextBox editor)
        {
            var property = modelItem?.Properties[propertyName];
            if (property == null)
                return HasEditorInput(editor);

            if (property.IsSet || property.ComputedValue != null)
                return true;

            if (property.Value == null)
                return HasEditorInput(editor);

            var propertyValueText = property.Value.ToString();
            if (!string.IsNullOrWhiteSpace(propertyValueText) &&
                !string.Equals(propertyValueText, "null", StringComparison.OrdinalIgnoreCase))
                return true;

            var expressionProperty = property.Value.Properties["Expression"];
            if (expressionProperty?.ComputedValue is string text)
                return !string.IsNullOrWhiteSpace(text);

            return HasEditorInput(editor);
        }

        private static bool HasEditorInput(ExpressionTextBox editor)
        {
            return editor != null && editor.Expression != null;
        }

        private static void SetRequiredBorder(Border border, bool required, bool filled)
        {
            if (border == null)
                return;

            if (!required || filled)
            {
                border.BorderBrush = Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
                return;
            }

            border.BorderBrush = Brushes.Red;
            border.BorderThickness = new Thickness(1);
        }

        private static void BindExpressionOwner(DependencyObject current, ModelItem owner)
        {
            if (current == null || owner == null)
                return;

            if (current is ExpressionTextBox expressionTextBox)
                expressionTextBox.OwnerActivity = owner;

            var childrenCount = VisualTreeHelper.GetChildrenCount(current);
            for (var i = 0; i < childrenCount; i++)
                BindExpressionOwner(VisualTreeHelper.GetChild(current, i), owner);
        }
    }
}
