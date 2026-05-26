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

namespace F2B.Terminal.PCOMM
{
    public sealed class PcommCanvasFieldsActivityDesigner : ActivityDesigner
    {
        private const double RowLabelMinWidth = 78;
        private const double EditorMinWidth = 190;
        private const double RowSpacing = 4;

        private readonly Border _rootPanel;
        private readonly StackPanel _host;
        private readonly ExpressionTextBox _sessionExpressionBox;
        private readonly ExpressionTextBox _sessionNameExpressionBox;
        private readonly ExpressionTextBox _wsFilePathExpressionBox;
        private readonly ExpressionTextBox _rowExpressionBox;
        private readonly ExpressionTextBox _textExpressionBox;
        private readonly ExpressionTextBox _xExpressionBox;
        private readonly ExpressionTextBox _yExpressionBox;
        private readonly FrameworkElement _sessionRow;
        private readonly FrameworkElement _sessionNameRow;
        private readonly FrameworkElement _wsFilePathRow;
        private readonly FrameworkElement _rowRow;
        private readonly FrameworkElement _textRow;
        private readonly FrameworkElement _xRow;
        private readonly FrameworkElement _yRow;
        private readonly FrameworkElement _keyRow;
        private readonly ComboBox _keyComboBox;
        private readonly Border _sessionEditorBorder;
        private readonly Border _sessionNameEditorBorder;
        private readonly Border _wsFilePathEditorBorder;
        private readonly Border _rowEditorBorder;
        private readonly Border _textEditorBorder;
        private readonly Border _xEditorBorder;
        private readonly Border _yEditorBorder;

        private DesignerMode _designerMode = DesignerMode.None;
        private bool _isSyncingKey;

        public PcommCanvasFieldsActivityDesigner()
        {
            _host = new StackPanel
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

            var body = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            Grid.SetIsSharedSizeScope(body, true);

            _sessionExpressionBox = CreateExpressionTextBox("Session", typeof(PcommSession));
            _sessionNameExpressionBox = CreateExpressionTextBox("SessionName", typeof(string));
            _wsFilePathExpressionBox = CreateExpressionTextBox("WsFilePath", typeof(string));
            _rowExpressionBox = CreateExpressionTextBox("Row", typeof(int));
            _textExpressionBox = CreateExpressionTextBox("Text", typeof(string));
            _xExpressionBox = CreateExpressionTextBox("X", typeof(int));
            _yExpressionBox = CreateExpressionTextBox("Y", typeof(int));

            _sessionNameRow = CreateRow("Session Name", _sessionNameExpressionBox, out _sessionNameEditorBorder);
            _wsFilePathRow = CreateRow("WS File Path", _wsFilePathExpressionBox, out _wsFilePathEditorBorder);
            _sessionRow = CreateRow("Session", _sessionExpressionBox, out _sessionEditorBorder);
            _rowRow = CreateRow("Row", _rowExpressionBox, out _rowEditorBorder, RowSpacing);
            _textRow = CreateRow("Text", _textExpressionBox, out _textEditorBorder, RowSpacing);
            _yRow = CreateRow("Row Index", _yExpressionBox, out _yEditorBorder, RowSpacing);
            _xRow = CreateRow("Column Index", _xExpressionBox, out _xEditorBorder, RowSpacing);

            _keyComboBox = BuildPcommKeyComboBox();
            _keyComboBox.SelectionChanged += OnKeySelectionChanged;
            _keyRow = CreateRow("Key", _keyComboBox, out _, RowSpacing);

            body.Children.Add(_sessionNameRow);
            body.Children.Add(_wsFilePathRow);
            body.Children.Add(_sessionRow);
            body.Children.Add(_textRow);
            body.Children.Add(_rowRow);
            body.Children.Add(_yRow);
            body.Children.Add(_xRow);
            body.Children.Add(_keyRow);

            _rootPanel.Child = body;
            _host.Children.Add(_rootPanel);
            Content = _host;

            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            BindExpressionOwner(_rootPanel, ModelItem);
            BindKnownExpressionOwners(ModelItem);
            _designerMode = ResolveMode(ModelItem);
            RefreshModeRows();
            if (_designerMode == DesignerMode.SendKey)
            {
                SyncKeyCombo(ReadKey(ModelItem));
            }

            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void RefreshModeRows()
        {
            _sessionNameRow.Visibility = _designerMode == DesignerMode.ConnectToTerminal ? Visibility.Visible : Visibility.Collapsed;
            _wsFilePathRow.Visibility = _designerMode == DesignerMode.OpenWs ? Visibility.Visible : Visibility.Collapsed;
            _sessionRow.Visibility = IsSessionMode(_designerMode) ? Visibility.Visible : Visibility.Collapsed;
            _textRow.Visibility = IsTextMode(_designerMode) ? Visibility.Visible : Visibility.Collapsed;
            _rowRow.Visibility = IsRowMode(_designerMode) ? Visibility.Visible : Visibility.Collapsed;
            _xRow.Visibility = IsCoordinateMode(_designerMode) ? Visibility.Visible : Visibility.Collapsed;
            _yRow.Visibility = IsCoordinateMode(_designerMode) ? Visibility.Visible : Visibility.Collapsed;
            _keyRow.Visibility = _designerMode == DesignerMode.SendKey ? Visibility.Visible : Visibility.Collapsed;
            RefreshCanvasBodyVisibility();
        }

        private void OnKeySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingKey || ModelItem == null || _designerMode != DesignerMode.SendKey)
            {
                return;
            }

            if (PcommKeyHelper.TryParseDisplayName(_keyComboBox.SelectedItem as string, out var key))
            {
                ModelItem.Properties["Key"].SetValue(key);
            }
        }

        private void RefreshCanvasBodyVisibility()
        {
            var hasVisibleRows =
                _sessionNameRow.Visibility == Visibility.Visible ||
                _wsFilePathRow.Visibility == Visibility.Visible ||
                _sessionRow.Visibility == Visibility.Visible ||
                _textRow.Visibility == Visibility.Visible ||
                _rowRow.Visibility == Visibility.Visible ||
                _xRow.Visibility == Visibility.Visible ||
                _yRow.Visibility == Visibility.Visible ||
                _keyRow.Visibility == Visibility.Visible;

            _rootPanel.Visibility = hasVisibleRows ? Visibility.Visible : Visibility.Collapsed;
            _host.Margin = hasVisibleRows ? new Thickness(4) : new Thickness(0);
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (ModelItem != null && _designerMode == DesignerMode.SendKey)
                    {
                        SyncKeyCombo(ReadKey(ModelItem));
                    }

                    RefreshRequiredBorders();
                }),
                DispatcherPriority.Background);
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
            {
                return;
            }

            SetRequiredBorder(
                _sessionNameEditorBorder,
                _designerMode == DesignerMode.ConnectToTerminal,
                IsArgumentFilled(ModelItem, "SessionName", _sessionNameExpressionBox));
            SetRequiredBorder(
                _wsFilePathEditorBorder,
                _designerMode == DesignerMode.OpenWs,
                IsArgumentFilled(ModelItem, "WsFilePath", _wsFilePathExpressionBox));
            SetRequiredBorder(
                _sessionEditorBorder,
                IsSessionMode(_designerMode),
                IsArgumentFilled(ModelItem, "Session", _sessionExpressionBox));
            SetRequiredBorder(
                _textEditorBorder,
                IsTextMode(_designerMode),
                IsArgumentFilled(ModelItem, "Text", _textExpressionBox));
            SetRequiredBorder(
                _rowEditorBorder,
                IsRowMode(_designerMode),
                IsArgumentFilled(ModelItem, "Row", _rowExpressionBox));
            SetRequiredBorder(
                _xEditorBorder,
                IsCoordinateMode(_designerMode),
                IsArgumentFilled(ModelItem, "X", _xExpressionBox));
            SetRequiredBorder(
                _yEditorBorder,
                IsCoordinateMode(_designerMode),
                IsArgumentFilled(ModelItem, "Y", _yExpressionBox));
        }

        private static FrameworkElement CreateRow(string label, FrameworkElement editor, out Border editorBorder, double top = 0)
        {
            NormalizeEditor(editor);

            var row = new Grid
            {
                Margin = new Thickness(0, top, 0, 0)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto,
                MinWidth = RowLabelMinWidth,
                SharedSizeGroup = "PcommCanvasFieldLabelColumn"
            });
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });

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
                Width = EditorMinWidth,
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
            }

            if (editor is ComboBox comboBox)
            {
                comboBox.MinWidth = EditorMinWidth;
                comboBox.Width = EditorMinWidth;
                comboBox.IsEditable = false;
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

        private static ComboBox BuildPcommKeyComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.Items.Add("ENTER");
            comboBox.Items.Add("TAB");
            comboBox.Items.Add("BACKSPACE");
            comboBox.Items.Add("F1");
            comboBox.Items.Add("F2");
            comboBox.Items.Add("F3");
            comboBox.Items.Add("F4");
            comboBox.Items.Add("F5");
            comboBox.Items.Add("F6");
            comboBox.Items.Add("F7");
            comboBox.Items.Add("F8");
            comboBox.Items.Add("F9");
            comboBox.Items.Add("F10");
            comboBox.Items.Add("F11");
            comboBox.Items.Add("F12");
            return comboBox;
        }

        private static PcommKey ReadKey(ModelItem modelItem)
        {
            var property = modelItem?.Properties["Key"];
            if (property?.ComputedValue is PcommKey computedKey)
            {
                return computedKey;
            }

            return PcommKey.Enter;
        }

        private void SyncKeyCombo(PcommKey key)
        {
            _isSyncingKey = true;
            _keyComboBox.SelectedItem = PcommKeyHelper.ToDisplayName(key);
            _isSyncingKey = false;
        }

        private static DesignerMode ResolveMode(ModelItem modelItem)
        {
            var activityType = modelItem?.ItemType;
            if (activityType == null)
            {
                return DesignerMode.None;
            }

            switch (activityType.Name)
            {
                case nameof(ConnectToTerminalActivity):
                    return DesignerMode.ConnectToTerminal;
                case nameof(OpenWsActivity):
                    return DesignerMode.OpenWs;
                case nameof(CloseTerminalActivity):
                    return DesignerMode.CloseTerminal;
                case nameof(ReadSingleRowActivity):
                    return DesignerMode.ReadSingleRow;
                case nameof(ReadAllRowActivity):
                    return DesignerMode.ReadAllRow;
                case nameof(SetCursorPosActivity):
                    return DesignerMode.SetCursorPos;
                case nameof(InputTextActivity):
                    return DesignerMode.InputText;
                case nameof(WaitForTextActivity):
                    return DesignerMode.WaitForText;
                case nameof(WaitForTextInRowActivity):
                    return DesignerMode.WaitForTextInRow;
                case nameof(SendKeyActivity):
                    return DesignerMode.SendKey;
                default:
                    return DesignerMode.None;
            }
        }

        private static bool IsSessionMode(DesignerMode mode)
        {
            switch (mode)
            {
                case DesignerMode.ReadSingleRow:
                case DesignerMode.ReadAllRow:
                case DesignerMode.SetCursorPos:
                case DesignerMode.InputText:
                case DesignerMode.WaitForText:
                case DesignerMode.WaitForTextInRow:
                case DesignerMode.SendKey:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsTextMode(DesignerMode mode)
        {
            switch (mode)
            {
                case DesignerMode.InputText:
                case DesignerMode.WaitForText:
                case DesignerMode.WaitForTextInRow:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsRowMode(DesignerMode mode)
        {
            switch (mode)
            {
                case DesignerMode.ReadSingleRow:
                case DesignerMode.WaitForTextInRow:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsCoordinateMode(DesignerMode mode)
        {
            switch (mode)
            {
                case DesignerMode.SetCursorPos:
                case DesignerMode.InputText:
                    return true;
                default:
                    return false;
            }
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

        private void BindKnownExpressionOwners(ModelItem owner)
        {
            if (owner == null)
            {
                return;
            }

            _sessionExpressionBox.OwnerActivity = owner;
            _sessionNameExpressionBox.OwnerActivity = owner;
            _wsFilePathExpressionBox.OwnerActivity = owner;
            _rowExpressionBox.OwnerActivity = owner;
            _textExpressionBox.OwnerActivity = owner;
            _xExpressionBox.OwnerActivity = owner;
            _yExpressionBox.OwnerActivity = owner;
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

            if (property.ComputedValue != null)
            {
                return true;
            }

            if (property.Value == null)
            {
                return HasEditorInput(editor);
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
                var expressionText = expressionProperty.Value.ToString();
                return !string.IsNullOrWhiteSpace(expressionText);
            }

            return HasEditorInput(editor);
        }

        private static void SetRequiredBorder(Border border, bool required, bool filled)
        {
            if (border == null)
            {
                return;
            }

            if (!required || filled)
            {
                border.BorderBrush = Brushes.Transparent;
                border.BorderThickness = new Thickness(0);
                return;
            }

            border.BorderBrush = Brushes.Red;
            border.BorderThickness = new Thickness(1);
        }

        private static bool HasEditorInput(ExpressionTextBox editor)
        {
            return editor?.Expression != null;
        }

        private enum DesignerMode
        {
            None,
            ConnectToTerminal,
            OpenWs,
            CloseTerminal,
            ReadSingleRow,
            ReadAllRow,
            SetCursorPos,
            InputText,
            WaitForText,
            WaitForTextInRow,
            SendKey
        }
    }
}
