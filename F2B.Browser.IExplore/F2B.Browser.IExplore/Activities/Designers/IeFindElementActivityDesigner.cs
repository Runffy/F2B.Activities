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

namespace F2B.Browser.IExplore
{
    public sealed class IeFindElementActivityDesigner : ActivityDesigner
    {
        private const double RowLabelMinWidth = 78;
        private const double EditorMinWidth = 190;
        private const double RowSpacing = 4;

        private readonly Border _rootPanel;
        private readonly Border _ieWindowEditorBorder;
        private readonly Border _parentObjectEditorBorder;
        private readonly Border _selectorEditorBorder;
        private readonly Border _framePathEditorBorder;
        private readonly ExpressionTextBox _ieWindowExpressionBox;
        private readonly ExpressionTextBox _parentObjectExpressionBox;
        private readonly ExpressionTextBox _selectorExpressionBox;
        private readonly ExpressionTextBox _framePathExpressionBox;
        private readonly FrameworkElement _baseOnRow;
        private readonly FrameworkElement _parentObjectRow;
        private readonly ComboBox _baseOnComboBox;
        private bool _isSyncingBaseOn;

        public IeFindElementActivityDesigner()
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

            _ieWindowExpressionBox = CreateExpressionTextBox("InputWindow", typeof(EmbeddedIEWindow));
            body.Children.Add(CreateRow("IE Window", _ieWindowExpressionBox, out _ieWindowEditorBorder));

            _baseOnComboBox = new ComboBox
            {
                MinWidth = EditorMinWidth,
                Width = EditorMinWidth,
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center
            };
            _baseOnComboBox.Items.Add(nameof(IeBaseOn.Window));
            _baseOnComboBox.Items.Add(nameof(IeBaseOn.Element));
            _baseOnComboBox.SelectionChanged += OnBaseOnSelectionChanged;
            _baseOnRow = CreateRow("Base On", _baseOnComboBox, out _);
            body.Children.Add(_baseOnRow);

            _parentObjectExpressionBox = CreateExpressionTextBox("ParentObject", typeof(IEHtmlElement));
            _parentObjectRow = CreateRow("Parent Object", _parentObjectExpressionBox, out _parentObjectEditorBorder, RowSpacing);
            body.Children.Add(_parentObjectRow);

            _selectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            body.Children.Add(CreateRow("Selector (Json String)", _selectorExpressionBox, out _selectorEditorBorder, RowSpacing));

            _framePathExpressionBox = CreateExpressionTextBox("FramePath", typeof(string));
            body.Children.Add(CreateRow("Frame (Json String)", _framePathExpressionBox, out _framePathEditorBorder, RowSpacing));

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
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;

            var baseOn = ReadBaseOn(ModelItem);
            SyncBaseOnCombo(baseOn);
            RefreshBaseOnRows(baseOn);
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "BaseOn")
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    RefreshBaseOnRows(ReadBaseOn(ModelItem));
                    RefreshRequiredBorders();
                }), DispatcherPriority.Background);
            }
            else
            {
                Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
            }
        }

        private void OnBaseOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingBaseOn || ModelItem == null)
                return;

            var baseOn = ParseBaseOn(_baseOnComboBox.SelectedItem as string);
            ModelItem.Properties[nameof(FindElementActivity.BaseOn)].SetValue(baseOn);
            RefreshBaseOnRows(baseOn);
            RefreshRequiredBorders();
        }

        private void RefreshBaseOnRows(IeBaseOn baseOn)
        {
            _parentObjectRow.Visibility = baseOn == IeBaseOn.Element ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
                return;

            SetRequiredBorder(_ieWindowEditorBorder, IsArgumentFilled(ModelItem, "InputWindow", _ieWindowExpressionBox));
            SetRequiredBorder(_selectorEditorBorder, IsArgumentFilled(ModelItem, "Selector", _selectorExpressionBox));

            if (ReadBaseOn(ModelItem) == IeBaseOn.Element)
                SetOptionalBorder(_parentObjectEditorBorder);
            else
                SetOptionalBorder(_parentObjectEditorBorder);

            SetOptionalBorder(_framePathEditorBorder);
        }

        private static IeBaseOn ReadBaseOn(ModelItem modelItem)
        {
            var modelProperty = modelItem?.Properties["BaseOn"];
            if (modelProperty?.ComputedValue is IeBaseOn value)
                return value;
            return IeBaseOn.Window;
        }

        private void SyncBaseOnCombo(IeBaseOn baseOn)
        {
            _isSyncingBaseOn = true;
            try
            {
                _baseOnComboBox.SelectedItem = baseOn.ToString();
            }
            finally
            {
                _isSyncingBaseOn = false;
            }
        }

        private static IeBaseOn ParseBaseOn(string text)
        {
            if (string.Equals(text, nameof(IeBaseOn.Element), StringComparison.Ordinal))
                return IeBaseOn.Element;
            return IeBaseOn.Window;
        }

        private static FrameworkElement CreateRow(string label, FrameworkElement editor, out Border editorBorder, double top = 0)
        {
            NormalizeEditor(editor);

            var row = new Grid { Margin = new Thickness(0, top, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto,
                MinWidth = RowLabelMinWidth,
                SharedSizeGroup = "IeFindElementLabelColumn"
            });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelTextBlock = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetColumn(labelTextBlock, 0);
            row.Children.Add(labelTextBlock);

            Border host;
            if (editor is Border existingBorder)
            {
                host = existingBorder;
            }
            else
            {
                host = new Border
                {
                    Margin = new Thickness(4, 0, 0, 0),
                    MinWidth = EditorMinWidth,
                    Height = 22,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Child = editor
                };
            }

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

            if (editor is ExpressionTextBox expressionTextBox)
            {
                expressionTextBox.MinWidth = EditorMinWidth;
                expressionTextBox.Width = EditorMinWidth;
                expressionTextBox.MinHeight = 22;
                expressionTextBox.Height = 22;
                expressionTextBox.MaxHeight = 22;
                expressionTextBox.MinLines = 1;
                expressionTextBox.MaxLines = 1;
            }
        }

        private static ExpressionTextBox CreateExpressionTextBox(string pathToArgument, Type expressionType)
        {
            var editor = new ExpressionTextBox
            {
                PathToArgument = pathToArgument,
                ExpressionType = expressionType
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

        private static void BindExpressionOwner(DependencyObject parent, ModelItem owner)
        {
            if (parent is ExpressionTextBox expressionTextBox)
                expressionTextBox.OwnerActivity = owner;

            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childCount; i++)
                BindExpressionOwner(VisualTreeHelper.GetChild(parent, i), owner);
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
                var expressionText = expressionProperty.Value.ToString();
                return !string.IsNullOrWhiteSpace(expressionText);
            }

            return HasEditorInput(editor);
        }

        private static bool HasEditorInput(ExpressionTextBox editor) =>
            editor != null && editor.Expression != null;

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

        private static void SetOptionalBorder(Border border)
        {
            if (border == null)
                return;

            border.BorderBrush = Brushes.Transparent;
            border.BorderThickness = new Thickness(0);
        }
    }
}
