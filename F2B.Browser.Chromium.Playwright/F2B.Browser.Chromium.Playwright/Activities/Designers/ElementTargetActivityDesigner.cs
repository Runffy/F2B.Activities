using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Converters;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace F2B.Browser.Chromium.Playwright
{
    public sealed class ElementTargetActivityDesigner : ActivityDesigner
    {
        private static readonly bool ShowMetrics = false;
        private const double RowLabelMinWidth = 78;
        private const double EditorMinWidth = 190;
        private const double RowSpacing = 4;

        private readonly FrameworkElement _elementRow;
        private readonly FrameworkElement _tabRow;
        private readonly FrameworkElement _selectorRow;
        private readonly FrameworkElement _saveAsPathRow;
        private readonly FrameworkElement _childSelectorRow;
        private readonly FrameworkElement _valueRow;
        private readonly FrameworkElement _keysRow;
        private readonly FrameworkElement _nameRow;
        private readonly FrameworkElement _pathRow;
        private readonly FrameworkElement _scriptRow;
        private readonly FrameworkElement _valTypeRow;
        private readonly FrameworkElement _textsRow;
        private readonly FrameworkElement _valuesRow;
        private readonly FrameworkElement _indicesRow;
        private readonly Border _rootPanel;
        private readonly Border _elementEditorBorder;
        private readonly Border _tabEditorBorder;
        private readonly Border _selectorEditorBorder;
        private readonly Border _saveAsPathEditorBorder;
        private readonly Border _childSelectorEditorBorder;
        private readonly Border _valueEditorBorder;
        private readonly Border _keysEditorBorder;
        private readonly Border _nameEditorBorder;
        private readonly Border _pathEditorBorder;
        private readonly Border _scriptEditorBorder;
        private readonly Border _textsEditorBorder;
        private readonly Border _valuesEditorBorder;
        private readonly Border _indicesEditorBorder;
        private readonly ExpressionTextBox _elementExpressionBox;
        private readonly ExpressionTextBox _tabExpressionBox;
        private readonly ExpressionTextBox _selectorExpressionBox;
        private readonly ExpressionTextBox _saveAsPathExpressionBox;
        private readonly ExpressionTextBox _childSelectorExpressionBox;
        private readonly ExpressionTextBox _valueExpressionBox;
        private readonly ExpressionTextBox _keysExpressionBox;
        private readonly ExpressionTextBox _nameExpressionBox;
        private readonly ExpressionTextBox _pathExpressionBox;
        private readonly ExpressionTextBox _scriptExpressionBox;
        private readonly ExpressionTextBox _textsExpressionBox;
        private readonly ExpressionTextBox _valuesExpressionBox;
        private readonly ExpressionTextBox _indicesExpressionBox;
        private readonly ComboBox _targetTypeComboBox;
        private readonly ComboBox _valTypeComboBox;
        private readonly TextBlock _metricsText;

        private DesignerMode _designerMode = DesignerMode.General;
        private bool _isSyncingTargetType;
        private bool _isSyncingValType;

        public ElementTargetActivityDesigner()
        {
            var host = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(4)
            };
            _metricsText = BuildMetricsText();
            if (_metricsText != null)
            {
                host.Children.Add(_metricsText);
            }

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

            _targetTypeComboBox = BuildTargetTypeComboBox();
            _targetTypeComboBox.SelectionChanged += OnTargetTypeSelectionChanged;
            body.Children.Add(CreateRow("TargetType", _targetTypeComboBox));
            _elementExpressionBox = CreateExpressionTextBox("Element", typeof(PwElement));
            _elementRow = CreateRow("Element", _elementExpressionBox, out _elementEditorBorder, RowSpacing);
            _tabExpressionBox = CreateExpressionTextBox("InputTab", typeof(PwTab));
            _tabRow = CreateRow("Tab", _tabExpressionBox, out _tabEditorBorder, RowSpacing);
            _selectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _selectorRow = CreateRow("Selector", _selectorExpressionBox, out _selectorEditorBorder, RowSpacing);
            body.Children.Add(_elementRow);
            body.Children.Add(_tabRow);
            body.Children.Add(_selectorRow);

            _saveAsPathExpressionBox = CreateExpressionTextBox("SaveAsPath", typeof(string));
            _childSelectorExpressionBox = CreateExpressionTextBox("ChildSelector", typeof(string));
            _valueExpressionBox = CreateExpressionTextBox("Value", typeof(string));
            _keysExpressionBox = CreateExpressionTextBox("Keys", typeof(string));
            _nameExpressionBox = CreateExpressionTextBox("Name", typeof(string));
            _pathExpressionBox = CreateExpressionTextBox("Path", typeof(string));
            _scriptExpressionBox = CreateExpressionTextBox("Script", typeof(string));

            _saveAsPathRow = CreateRow("SaveAsPath", _saveAsPathExpressionBox, out _saveAsPathEditorBorder, RowSpacing);
            _childSelectorRow = CreateRow("ChildSelector", _childSelectorExpressionBox, out _childSelectorEditorBorder, RowSpacing);
            _valueRow = CreateRow("Value", _valueExpressionBox, out _valueEditorBorder, RowSpacing);
            _keysRow = CreateRow("Keys", _keysExpressionBox, out _keysEditorBorder, RowSpacing);
            _nameRow = CreateRow("Name", _nameExpressionBox, out _nameEditorBorder, RowSpacing);
            _pathRow = CreateRow("Path", _pathExpressionBox, out _pathEditorBorder, RowSpacing);
            _scriptRow = CreateRow("Script", _scriptExpressionBox, out _scriptEditorBorder, RowSpacing);

            _valTypeComboBox = BuildValTypeComboBox();
            _valTypeComboBox.SelectionChanged += OnValTypeSelectionChanged;
            _valTypeRow = CreateRow("ValType", _valTypeComboBox, RowSpacing);
            _textsExpressionBox = CreateExpressionTextBox("Texts", typeof(string[]));
            _valuesExpressionBox = CreateExpressionTextBox("Values", typeof(string[]));
            _indicesExpressionBox = CreateExpressionTextBox("Indices", typeof(int[]));
            _textsRow = CreateRow("Texts", _textsExpressionBox, out _textsEditorBorder, RowSpacing);
            _valuesRow = CreateRow("Values", _valuesExpressionBox, out _valuesEditorBorder, RowSpacing);
            _indicesRow = CreateRow("Indices", _indicesExpressionBox, out _indicesEditorBorder, RowSpacing);

            body.Children.Add(_saveAsPathRow);
            body.Children.Add(_childSelectorRow);
            body.Children.Add(_valueRow);
            body.Children.Add(_keysRow);
            body.Children.Add(_nameRow);
            body.Children.Add(_pathRow);
            body.Children.Add(_scriptRow);
            body.Children.Add(_valTypeRow);
            body.Children.Add(_textsRow);
            body.Children.Add(_valuesRow);
            body.Children.Add(_indicesRow);

            _rootPanel.Child = body;
            host.Children.Add(_rootPanel);

            Content = host;
            Loaded += OnLoaded;
        }

        private static TextBlock BuildMetricsText()
        {
            if (!ShowMetrics)
            {
                return null;
            }

            return new TextBlock
            {
                FontSize = 10,
                Foreground = Brushes.DarkRed,
                Margin = new Thickness(4, 0, 0, 4),
                Text = "metrics: waiting"
            };
        }

        private static FrameworkElement CreateRow(string label, FrameworkElement editor, double top = 0)
        {
            return CreateRow(label, editor, out _, top);
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
                SharedSizeGroup = "ElementTargetLabelColumn"
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

        private static ComboBox BuildValTypeComboBox()
        {
            var comboBox = new ComboBox
            {
                IsEditable = false
            };
            comboBox.Items.Add(SelectValType.Text);
            comboBox.Items.Add(SelectValType.Value);
            comboBox.Items.Add(SelectValType.Index);
            return comboBox;
        }

        private static ComboBox BuildTargetTypeComboBox()
        {
            var comboBox = new ComboBox
            {
                IsEditable = false
            };
            comboBox.Items.Add(ElementTargetType.Selector);
            comboBox.Items.Add(ElementTargetType.Element);
            return comboBox;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (ModelItem == null)
            {
                return;
            }

            BindExpressionOwner(_rootPanel, ModelItem);

            var current = ReadTargetType(ModelItem);
            SyncTargetTypeCombo(current);
            RefreshTargetRows(current);

            _designerMode = ReadDesignerMode(ModelItem);
            RefreshSpecificRows();

            if (_designerMode == DesignerMode.Select)
            {
                var valType = ReadValType(ModelItem);
                SyncValTypeCombo(valType);
                RefreshSelectRows(valType);
            }

            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();

            if (ShowMetrics)
            {
                _rootPanel.SizeChanged += (_, __) => UpdateMetrics();
                Dispatcher.BeginInvoke(new Action(UpdateMetrics), DispatcherPriority.Loaded);
            }
        }

        private void OnTargetTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingTargetType || ModelItem == null)
            {
                return;
            }

            if (_targetTypeComboBox.SelectedItem is ElementTargetType targetType)
            {
                ModelItem.Properties["TargetType"].SetValue(targetType);
                RefreshTargetRows(targetType);
                RefreshRequiredBorders();
            }
        }

        private void OnValTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingValType || ModelItem == null || _designerMode != DesignerMode.Select)
            {
                return;
            }

            if (_valTypeComboBox.SelectedItem is SelectValType valType)
            {
                ModelItem.Properties["ValType"].SetValue(valType);
                RefreshSelectRows(valType);
                RefreshRequiredBorders();
            }
        }

        private void SyncTargetTypeCombo(ElementTargetType targetType)
        {
            _isSyncingTargetType = true;
            _targetTypeComboBox.SelectedItem = targetType;
            _isSyncingTargetType = false;
        }

        private void SyncValTypeCombo(SelectValType valType)
        {
            _isSyncingValType = true;
            _valTypeComboBox.SelectedItem = valType;
            _isSyncingValType = false;
        }

        private void RefreshTargetRows(ElementTargetType targetType)
        {
            var isElement = targetType == ElementTargetType.Element;
            _elementRow.Visibility = isElement ? Visibility.Visible : Visibility.Collapsed;
            _tabRow.Visibility = isElement ? Visibility.Collapsed : Visibility.Visible;
            _selectorRow.Visibility = isElement ? Visibility.Collapsed : Visibility.Visible;
        }

        private void RefreshSpecificRows()
        {
            _saveAsPathRow.Visibility = _designerMode == DesignerMode.ClickForDownload ? Visibility.Visible : Visibility.Collapsed;
            _childSelectorRow.Visibility = _designerMode == DesignerMode.GetChildren ? Visibility.Visible : Visibility.Collapsed;
            _valueRow.Visibility = _designerMode == DesignerMode.Input || _designerMode == DesignerMode.SetAttribute ? Visibility.Visible : Visibility.Collapsed;
            _keysRow.Visibility = _designerMode == DesignerMode.SendKeys ? Visibility.Visible : Visibility.Collapsed;
            _nameRow.Visibility = _designerMode == DesignerMode.SetAttribute ? Visibility.Visible : Visibility.Collapsed;
            _pathRow.Visibility = _designerMode == DesignerMode.TakeScreenshot ? Visibility.Visible : Visibility.Collapsed;
            _scriptRow.Visibility = _designerMode == DesignerMode.RunJs ? Visibility.Visible : Visibility.Collapsed;
            _valTypeRow.Visibility = _designerMode == DesignerMode.Select ? Visibility.Visible : Visibility.Collapsed;

            if (_designerMode != DesignerMode.Select)
            {
                _textsRow.Visibility = Visibility.Collapsed;
                _valuesRow.Visibility = Visibility.Collapsed;
                _indicesRow.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshSelectRows(SelectValType valType)
        {
            if (_designerMode != DesignerMode.Select)
            {
                return;
            }

            _textsRow.Visibility = valType == SelectValType.Text ? Visibility.Visible : Visibility.Collapsed;
            _valuesRow.Visibility = valType == SelectValType.Value ? Visibility.Visible : Visibility.Collapsed;
            _indicesRow.Visibility = valType == SelectValType.Index ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ModelItem == null)
                {
                    return;
                }

                var targetType = ReadTargetType(ModelItem);
                SyncTargetTypeCombo(targetType);
                RefreshTargetRows(targetType);

                if (_designerMode == DesignerMode.Select)
                {
                    var valType = ReadValType(ModelItem);
                    SyncValTypeCombo(valType);
                    RefreshSelectRows(valType);
                }

                RefreshRequiredBorders();
            }), DispatcherPriority.Background);
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
            {
                return;
            }

            var targetType = ReadTargetType(ModelItem);
            var valType = ReadValType(ModelItem);

            SetRequiredBorder(_elementEditorBorder, targetType == ElementTargetType.Element, IsArgumentFilled(ModelItem, "Element", _elementExpressionBox));
            SetRequiredBorder(_tabEditorBorder, targetType == ElementTargetType.Selector, IsArgumentFilled(ModelItem, "InputTab", _tabExpressionBox));
            SetRequiredBorder(_selectorEditorBorder, targetType == ElementTargetType.Selector, IsArgumentFilled(ModelItem, "Selector", _selectorExpressionBox));

            SetRequiredBorder(_saveAsPathEditorBorder, _designerMode == DesignerMode.ClickForDownload, IsArgumentFilled(ModelItem, "SaveAsPath", _saveAsPathExpressionBox));
            SetRequiredBorder(_childSelectorEditorBorder, _designerMode == DesignerMode.GetChildren, IsArgumentFilled(ModelItem, "ChildSelector", _childSelectorExpressionBox));
            SetRequiredBorder(_valueEditorBorder, _designerMode == DesignerMode.Input || _designerMode == DesignerMode.SetAttribute, IsArgumentFilled(ModelItem, "Value", _valueExpressionBox));
            SetRequiredBorder(_keysEditorBorder, _designerMode == DesignerMode.SendKeys, IsArgumentFilled(ModelItem, "Keys", _keysExpressionBox));
            SetRequiredBorder(_nameEditorBorder, _designerMode == DesignerMode.SetAttribute, IsArgumentFilled(ModelItem, "Name", _nameExpressionBox));
            SetRequiredBorder(_pathEditorBorder, _designerMode == DesignerMode.TakeScreenshot, IsArgumentFilled(ModelItem, "Path", _pathExpressionBox));
            SetRequiredBorder(_scriptEditorBorder, _designerMode == DesignerMode.RunJs, IsArgumentFilled(ModelItem, "Script", _scriptExpressionBox));

            SetRequiredBorder(_textsEditorBorder, _designerMode == DesignerMode.Select && valType == SelectValType.Text, IsArgumentFilled(ModelItem, "Texts", _textsExpressionBox));
            SetRequiredBorder(_valuesEditorBorder, _designerMode == DesignerMode.Select && valType == SelectValType.Value, IsArgumentFilled(ModelItem, "Values", _valuesExpressionBox));
            SetRequiredBorder(_indicesEditorBorder, _designerMode == DesignerMode.Select && valType == SelectValType.Index, IsArgumentFilled(ModelItem, "Indices", _indicesExpressionBox));
        }

        private static bool IsArgumentFilled(ModelItem modelItem, string propertyName, ExpressionTextBox editor)
        {
            var property = modelItem?.Properties[propertyName];
            if (property == null)
            {
                return HasEditorInput(editor);
            }

            // In the WF designer, IsSet is the most reliable flag that the user explicitly assigned a value.
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
                var expressionText = expressionProperty.Value.ToString();
                return !string.IsNullOrWhiteSpace(expressionText);
            }

            return HasEditorInput(editor);
        }

        private static bool HasEditorInput(ExpressionTextBox editor)
        {
            if (editor == null)
            {
                return false;
            }

            return editor.Expression != null;
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

        private static ElementTargetType ReadTargetType(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["TargetType"];
            if (modelProperty?.ComputedValue is ElementTargetType value)
            {
                return value;
            }

            return ElementTargetType.Selector;
        }

        private static SelectValType ReadValType(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["ValType"];
            if (modelProperty?.ComputedValue is SelectValType value)
            {
                return value;
            }

            return SelectValType.Text;
        }

        private static DesignerMode ReadDesignerMode(ModelItem modelItem)
        {
            var activityType = modelItem?.ItemType;
            if (activityType == null)
            {
                return DesignerMode.General;
            }

            var typeName = activityType.Name;
            switch (typeName)
            {
                case nameof(ClickForDownloadActivity):
                    return DesignerMode.ClickForDownload;
                case nameof(GetChildrenActivity):
                    return DesignerMode.GetChildren;
                case nameof(InputActivity):
                    return DesignerMode.Input;
                case nameof(SendkeysActivity):
                    return DesignerMode.SendKeys;
                case nameof(SetAttributeActivity):
                    return DesignerMode.SetAttribute;
                case nameof(TakeScreenshotActivity):
                    return DesignerMode.TakeScreenshot;
                case nameof(RunJsActivity):
                    return DesignerMode.RunJs;
                case nameof(SelectActivity):
                    return DesignerMode.Select;
                default:
                    return DesignerMode.General;
            }
        }

        private void UpdateMetrics()
        {
            if (!ShowMetrics || _metricsText == null)
            {
                return;
            }

            var switchW = _targetTypeComboBox == null ? 0 : _targetTypeComboBox.ActualWidth;
            var tabW = _tabExpressionBox == null ? 0 : _tabExpressionBox.ActualWidth;
            var selectorW = _selectorExpressionBox == null ? 0 : _selectorExpressionBox.ActualWidth;
            var text = $"metrics: switch={switchW:0.##}, tabBox={tabW:0.##}, selectorBox={selectorW:0.##}";
            _metricsText.Text = text;
            Debug.WriteLine("[ElementTargetActivityDesigner] " + text);
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

        private enum DesignerMode
        {
            General,
            ClickForDownload,
            GetChildren,
            Input,
            SendKeys,
            SetAttribute,
            TakeScreenshot,
            RunJs,
            Select
        }
    }
}
