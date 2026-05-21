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
    public sealed class IeRunJsActivityDesigner : ActivityDesigner
    {
        private const double RowLabelMinWidth = 78;
        private const double EditorMinWidth = 190;
        private const double RowSpacing = 4;

        private readonly Border _rootPanel;
        private readonly Border _ieWindowEditorBorder;
        private readonly Border _elementEditorBorder;
        private readonly Border _elementJsonEditorBorder;
        private readonly Border _framePathEditorBorder;
        private readonly Border _scriptEditorBorder;
        private readonly Border _argsJsonEditorBorder;
        private readonly Border _timeoutEditorBorder;
        private readonly ExpressionTextBox _ieWindowExpressionBox;
        private readonly ExpressionTextBox _elementExpressionBox;
        private readonly ExpressionTextBox _elementJsonExpressionBox;
        private readonly ExpressionTextBox _framePathExpressionBox;
        private readonly ExpressionTextBox _scriptExpressionBox;
        private readonly ExpressionTextBox _argsJsonExpressionBox;
        private readonly ExpressionTextBox _timeoutExpressionBox;
        private readonly FrameworkElement _baseOnRow;
        private readonly FrameworkElement _elementRow;
        private readonly FrameworkElement _elementJsonRow;
        private readonly FrameworkElement _timeoutRow;
        private readonly ComboBox _baseOnComboBox;
        private bool _isSyncingBaseOn;

        public IeRunJsActivityDesigner()
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

            _scriptExpressionBox = CreateExpressionTextBox("Script", typeof(string));
            body.Children.Add(CreateRow("Script", _scriptExpressionBox, out _scriptEditorBorder, RowSpacing));

            _framePathExpressionBox = CreateExpressionTextBox("FramePath", typeof(string));
            body.Children.Add(CreateRow("Frame (Json String)", _framePathExpressionBox, out _framePathEditorBorder, RowSpacing));

            _elementExpressionBox = CreateExpressionTextBox("Element", typeof(IEHtmlElement));
            _elementRow = CreateRow("Element", _elementExpressionBox, out _elementEditorBorder, RowSpacing);
            body.Children.Add(_elementRow);

            _elementJsonExpressionBox = CreateExpressionTextBox("ElementJson", typeof(string));
            _elementJsonRow = CreateRow("Element (Json String)", _elementJsonExpressionBox, out _elementJsonEditorBorder, RowSpacing);
            body.Children.Add(_elementJsonRow);

            _argsJsonExpressionBox = CreateExpressionTextBox("ArgsJson", typeof(string));
            body.Children.Add(CreateRow("Args (Json String)", _argsJsonExpressionBox, out _argsJsonEditorBorder, RowSpacing));

            _timeoutExpressionBox = CreateExpressionTextBox("Timeout", typeof(int));
            _timeoutRow = CreateRow("Timeout (ms)", _timeoutExpressionBox, out _timeoutEditorBorder, RowSpacing);
            body.Children.Add(_timeoutRow);

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
            ModelItem.Properties["BaseOn"].SetValue(baseOn);
            RefreshBaseOnRows(baseOn);
            RefreshRequiredBorders();
        }

        private void RefreshBaseOnRows(IeBaseOn baseOn)
        {
            var isElement = baseOn == IeBaseOn.Element;
            _elementRow.Visibility = isElement ? Visibility.Visible : Visibility.Collapsed;
            _elementJsonRow.Visibility = isElement ? Visibility.Visible : Visibility.Collapsed;
            _timeoutRow.Visibility = isElement ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
                return;

            SetRequiredBorder(_ieWindowEditorBorder, IsArgumentFilled(ModelItem, "InputWindow", _ieWindowExpressionBox));
            SetRequiredBorder(_scriptEditorBorder, IsArgumentFilled(ModelItem, "Script", _scriptExpressionBox));

            var baseOn = ReadBaseOn(ModelItem);
            if (baseOn == IeBaseOn.Element)
            {
                var hasElement = IsArgumentFilled(ModelItem, "Element", _elementExpressionBox);
                var hasJson = IsArgumentFilled(ModelItem, "ElementJson", _elementJsonExpressionBox);
                SetRequiredBorder(_elementEditorBorder, hasElement);
                SetRequiredBorder(_elementJsonEditorBorder, hasJson && !hasElement);
                if (!hasElement && !hasJson)
                {
                    SetRequiredBorder(_elementEditorBorder, false);
                    SetRequiredBorder(_elementJsonEditorBorder, false);
                }
            }
            else
            {
                SetOptionalBorder(_elementEditorBorder);
                SetOptionalBorder(_elementJsonEditorBorder);
            }

            SetOptionalBorder(_framePathEditorBorder);
            SetOptionalBorder(_argsJsonEditorBorder);
            SetOptionalBorder(_timeoutEditorBorder);
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
                SharedSizeGroup = "IeRunJsLabelColumn"
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

        private static void SetOptionalBorder(Border border) => SetRequiredBorder(border, true);
    }
}
