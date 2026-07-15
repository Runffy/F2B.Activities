using System;
using System.Activities;
using System.Activities.Presentation;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace F2B.Microsoft.Word
{
    public sealed class AppendImageActivityDesigner : ActivityDesigner
    {
        private const string LabelColumn = "WordAppendImageLabelColumn";
        private const double RowLabelMinWidth = 90;
        private const double EditorMinWidth = 190;
        private const double RowSpacing = 4;

        private readonly Border _rootPanel;
        private readonly FrameworkElement _wordFilePathRow;
        private readonly Border _wordFilePathEditorBorder;
        private readonly ExpressionTextBox _wordFilePathExpressionBox;
        private readonly FrameworkElement _imagePathRow;
        private readonly Border _imagePathEditorBorder;
        private readonly ExpressionTextBox _imagePathExpressionBox;
        private readonly FrameworkElement _sizeModeRow;
        private readonly ComboBox _sizeModeComboBox;
        private readonly FrameworkElement _widthRow;
        private readonly Border _widthEditorBorder;
        private readonly ExpressionTextBox _widthExpressionBox;
        private readonly FrameworkElement _heightRow;
        private readonly Border _heightEditorBorder;
        private readonly ExpressionTextBox _heightExpressionBox;
        private readonly FrameworkElement _unitRow;
        private readonly ComboBox _unitComboBox;
        private readonly FrameworkElement _visibleRow;
        private readonly Border _visibleEditorBorder;
        private readonly ExpressionTextBox _visibleExpressionBox;

        private bool _isSyncingSizeMode;
        private bool _isSyncingUnit;

        public AppendImageActivityDesigner()
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

            _wordFilePathExpressionBox = CreateInExpressionTextBox("WordFilePath", typeof(string));
            _wordFilePathRow = CreateRow("Word File Path", _wordFilePathExpressionBox, out _wordFilePathEditorBorder);
            body.Children.Add(_wordFilePathRow);

            _imagePathExpressionBox = CreateInExpressionTextBox("ImagePath", typeof(string));
            _imagePathRow = CreateRow("Image Path", _imagePathExpressionBox, out _imagePathEditorBorder, RowSpacing);
            body.Children.Add(_imagePathRow);

            _sizeModeComboBox = BuildDescriptionComboBox<WordImageSizeMode>();
            _sizeModeComboBox.SelectionChanged += OnSizeModeSelectionChanged;
            _sizeModeRow = CreateRow("Size Mode", _sizeModeComboBox, RowSpacing);
            body.Children.Add(_sizeModeRow);

            _widthExpressionBox = CreateInExpressionTextBox("Width", typeof(double));
            _widthRow = CreateRow("Width", _widthExpressionBox, out _widthEditorBorder, RowSpacing);
            body.Children.Add(_widthRow);

            _heightExpressionBox = CreateInExpressionTextBox("Height", typeof(double));
            _heightRow = CreateRow("Height", _heightExpressionBox, out _heightEditorBorder, RowSpacing);
            body.Children.Add(_heightRow);

            _unitComboBox = BuildDescriptionComboBox<WordImageUnit>();
            _unitComboBox.SelectionChanged += OnUnitSelectionChanged;
            _unitRow = CreateRow("Unit", _unitComboBox, RowSpacing);
            body.Children.Add(_unitRow);

            _visibleExpressionBox = CreateInExpressionTextBox("Visible", typeof(bool));
            _visibleRow = CreateRow("Visible", _visibleExpressionBox, out _visibleEditorBorder, RowSpacing);
            body.Children.Add(_visibleRow);

            _rootPanel.Child = body;
            host.Children.Add(_rootPanel);
            Content = host;
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            BindExpressionOwner(_rootPanel, ModelItem);
            SyncSizeModeCombo(ReadEnum(ModelItem, "SizeMode", WordImageSizeMode.RegularSize));
            SyncUnitCombo(ReadEnum(ModelItem, "Unit", WordImageUnit.Cm));
            RefreshCustomRows();
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "SizeMode", StringComparison.Ordinal))
            {
                SyncSizeModeCombo(ReadEnum(ModelItem, "SizeMode", WordImageSizeMode.RegularSize));
                RefreshCustomRows();
            }
            else if (string.Equals(e.PropertyName, "Unit", StringComparison.Ordinal))
            {
                SyncUnitCombo(ReadEnum(ModelItem, "Unit", WordImageUnit.Cm));
            }

            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void OnSizeModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingSizeMode || ModelItem == null)
            {
                return;
            }

            var sizeMode = ReadSelectedEnum(_sizeModeComboBox, WordImageSizeMode.RegularSize);
            ModelItem.Properties["SizeMode"].SetValue(sizeMode);
            RefreshCustomRows();
            RefreshRequiredBorders();
        }

        private void OnUnitSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingUnit || ModelItem == null)
            {
                return;
            }

            var unit = ReadSelectedEnum(_unitComboBox, WordImageUnit.Cm);
            ModelItem.Properties["Unit"].SetValue(unit);
        }

        private void RefreshCustomRows()
        {
            var isCustom = ReadEnum(ModelItem, "SizeMode", WordImageSizeMode.RegularSize) == WordImageSizeMode.Custom;
            var visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            _widthRow.Visibility = visibility;
            _heightRow.Visibility = visibility;
            _unitRow.Visibility = visibility;
        }

        private void SyncSizeModeCombo(WordImageSizeMode sizeMode)
        {
            _isSyncingSizeMode = true;
            SelectEnumItem(_sizeModeComboBox, sizeMode);
            _isSyncingSizeMode = false;
        }

        private void SyncUnitCombo(WordImageUnit unit)
        {
            _isSyncingUnit = true;
            SelectEnumItem(_unitComboBox, unit);
            _isSyncingUnit = false;
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
            {
                return;
            }

            SetRequiredBorder(_wordFilePathEditorBorder, IsArgumentFilled(ModelItem, "WordFilePath", _wordFilePathExpressionBox));
            SetRequiredBorder(_imagePathEditorBorder, IsArgumentFilled(ModelItem, "ImagePath", _imagePathExpressionBox));

            var isCustom = ReadEnum(ModelItem, "SizeMode", WordImageSizeMode.RegularSize) == WordImageSizeMode.Custom;
            if (isCustom)
            {
                SetRequiredBorder(_widthEditorBorder, IsArgumentFilled(ModelItem, "Width", _widthExpressionBox));
                SetRequiredBorder(_heightEditorBorder, IsArgumentFilled(ModelItem, "Height", _heightExpressionBox));
            }
            else
            {
                SetRequiredBorder(_widthEditorBorder, true);
                SetRequiredBorder(_heightEditorBorder, true);
            }
        }

        private static ComboBox BuildDescriptionComboBox<TEnum>() where TEnum : struct
        {
            var comboBox = new ComboBox
            {
                IsEditable = false,
                DisplayMemberPath = "Display",
                SelectedValuePath = "Value",
                MinWidth = EditorMinWidth,
                Width = EditorMinWidth
            };

            foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
            {
                comboBox.Items.Add(new EnumDisplayItem
                {
                    Value = value,
                    Display = GetEnumDescription(value)
                });
            }

            return comboBox;
        }

        private static string GetEnumDescription<TEnum>(TEnum value) where TEnum : struct
        {
            var name = value.ToString();
            var field = typeof(TEnum).GetField(name);
            var description = field?.GetCustomAttribute<DescriptionAttribute>();
            return description != null && !string.IsNullOrWhiteSpace(description.Description)
                ? description.Description
                : name;
        }

        private static void SelectEnumItem(ComboBox comboBox, object value)
        {
            foreach (EnumDisplayItem item in comboBox.Items)
            {
                if (Equals(item.Value, value))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
            {
                comboBox.SelectedIndex = 0;
            }
        }

        private static TEnum ReadSelectedEnum<TEnum>(ComboBox comboBox, TEnum fallback) where TEnum : struct
        {
            if (comboBox.SelectedItem is EnumDisplayItem item && item.Value is TEnum value)
            {
                return value;
            }

            return fallback;
        }

        private static TEnum ReadEnum<TEnum>(ModelItem modelItem, string propertyName, TEnum fallback) where TEnum : struct
        {
            if (modelItem?.Properties[propertyName]?.ComputedValue is TEnum value)
            {
                return value;
            }

            return fallback;
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
                SharedSizeGroup = LabelColumn
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

        private static ExpressionTextBox CreateInExpressionTextBox(string pathToArgument, Type expressionType)
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
            {
                return editor != null && editor.Expression != null;
            }

            if (property.IsSet || property.ComputedValue != null)
            {
                return true;
            }

            if (property.Value == null)
            {
                return editor != null && editor.Expression != null;
            }

            var propertyValueText = property.Value.ToString();
            if (!string.IsNullOrWhiteSpace(propertyValueText) &&
                !string.Equals(propertyValueText, "null", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var expressionProperty = property.Value.Properties["Expression"];
            if (expressionProperty == null)
            {
                return editor != null && editor.Expression != null;
            }

            if (expressionProperty.ComputedValue is string text)
            {
                return !string.IsNullOrWhiteSpace(text);
            }

            if (expressionProperty.Value != null)
            {
                return !string.IsNullOrWhiteSpace(expressionProperty.Value.ToString());
            }

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

        private static void BindExpressionOwner(DependencyObject parent, ModelItem owner)
        {
            if (parent is ExpressionTextBox expressionTextBox)
            {
                expressionTextBox.OwnerActivity = owner;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childCount; i++)
            {
                BindExpressionOwner(VisualTreeHelper.GetChild(parent, i), owner);
            }
        }

        private sealed class EnumDisplayItem
        {
            public object Value { get; set; }

            public string Display { get; set; }
        }
    }
}
