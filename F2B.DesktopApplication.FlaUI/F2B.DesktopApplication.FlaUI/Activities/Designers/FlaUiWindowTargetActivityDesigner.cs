using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.View;
using F2B.OpenRpa.Design;

namespace F2B.DesktopApplication.FlaUI
{
    public sealed class FlaUiWindowTargetActivityDesigner : ActivityDesigner
    {
        private const double RowLabelMinWidth = 78;
        private const double EditorMinWidth = 190;
        private const double RowSpacing = 4;

        private readonly Border _rootPanel;
        private readonly ComboBox _targetTypeComboBox;
        private readonly FrameworkElement _targetTypeRow;
        private readonly FrameworkElement _selectorRow;
        private readonly FrameworkElement _inputWindowRow;
        private readonly Border _selectorEditorBorder;
        private readonly Border _inputWindowEditorBorder;
        private readonly ExpressionTextBox _selectorExpressionBox;
        private readonly ExpressionTextBox _inputWindowExpressionBox;
        private bool _isSyncingTargetType;

        public FlaUiWindowTargetActivityDesigner()
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

            var body = new StackPanel { Orientation = Orientation.Vertical };
            Grid.SetIsSharedSizeScope(body, true);

            _targetTypeComboBox = BuildTargetTypeComboBox();
            _targetTypeComboBox.SelectionChanged += OnTargetTypeSelectionChanged;
            _targetTypeRow = CreateRow("TargetType", _targetTypeComboBox);
            body.Children.Add(_targetTypeRow);

            _selectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _selectorRow = SelectorDesignerSupport.CreateSelectorRow(
                "Selector (XML)",
                _selectorExpressionBox,
                "Selector",
                () => ModelItem,
                "FlaUiWindowTargetLabelColumn",
                EditorMinWidth,
                out _selectorEditorBorder,
                RowSpacing);
            body.Children.Add(_selectorRow);

            _inputWindowExpressionBox = CreateExpressionTextBox("InputWindow", typeof(UiWindow));
            _inputWindowRow = CreateRow("Input Window", _inputWindowExpressionBox, out _inputWindowEditorBorder, RowSpacing);
            body.Children.Add(_inputWindowRow);

            _rootPanel.Child = body;
            host.Children.Add(_rootPanel);
            Content = host;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
                return;

            BindExpressionOwner(_rootPanel, ModelItem);

            var targetType = ReadTargetType(ModelItem);
            SyncTargetTypeCombo(targetType);
            RefreshTargetRows(targetType);
            RefreshRequiredBorders();

            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
        }

        private void OnTargetTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingTargetType || ModelItem == null)
                return;

            if (_targetTypeComboBox.SelectedItem is WindowTargetType targetType)
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

        private void RefreshTargetRows(WindowTargetType targetType)
        {
            var isWindow = targetType == WindowTargetType.Window;
            _selectorRow.Visibility = isWindow ? Visibility.Collapsed : Visibility.Visible;
            _inputWindowRow.Visibility = isWindow ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
                return;

            var targetType = ReadTargetType(ModelItem);
            var isWindow = targetType == WindowTargetType.Window;

            SetRequiredBorder(_inputWindowEditorBorder, isWindow,
                IsArgumentFilled(ModelItem, "InputWindow", _inputWindowExpressionBox));
            SetRequiredBorder(_selectorEditorBorder, !isWindow,
                IsArgumentFilled(ModelItem, "Selector", _selectorExpressionBox));
        }

        private static WindowTargetType ReadTargetType(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["TargetType"];
            if (modelProperty?.ComputedValue is WindowTargetType value)
                return value;

            return WindowTargetType.Selector;
        }

        private void SyncTargetTypeCombo(WindowTargetType targetType)
        {
            _isSyncingTargetType = true;
            _targetTypeComboBox.SelectedItem = targetType;
            _isSyncingTargetType = false;
        }

        private static ComboBox BuildTargetTypeComboBox()
        {
            var comboBox = new ComboBox { IsEditable = false };
            comboBox.Items.Add(WindowTargetType.Selector);
            comboBox.Items.Add(WindowTargetType.Window);
            return comboBox;
        }

        private static FrameworkElement CreateRow(string label, FrameworkElement editor, out Border editorBorder, double top = 0)
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

            var row = new Grid { Margin = new Thickness(0, top, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto,
                MinWidth = RowLabelMinWidth,
                SharedSizeGroup = "FlaUiWindowTargetLabelColumn"
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

        private static FrameworkElement CreateRow(string label, FrameworkElement editor, double top = 0)
        {
            return CreateRow(label, editor, out _, top);
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
                return editor?.Expression != null;

            if (property.IsSet || property.ComputedValue != null)
                return true;

            return editor?.Expression != null;
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
