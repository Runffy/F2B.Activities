using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.OpenRpa.Design;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    public sealed class CdpElementTargetActivityDesigner : ActivityDesigner
    {
        private const string LabelColumn = "CdpElementTargetLabelColumn";

        private readonly Border _rootPanel;
        private readonly FrameworkElement _targetRow;
        private readonly FrameworkElement _selectorRow;
        private readonly FrameworkElement _parentObjectRow;
        private readonly FrameworkElement _destinationParentObjectRow;
        private readonly FrameworkElement _destinationSelectorRow;
        private readonly FrameworkElement _byRow;
        private readonly FrameworkElement _operationTypeRow;
        private readonly FrameworkElement _textTypeRow;
        private readonly FrameworkElement _valueRow;
        private readonly FrameworkElement _inputValueRow;
        private readonly FrameworkElement _setValueRow;
        private readonly FrameworkElement _outputResultRow;
        private readonly FrameworkElement _keysRow;
        private readonly FrameworkElement _nameRow;
        private readonly FrameworkElement _uploadPathsRow;
        private readonly FrameworkElement _offsetXRow;
        private readonly FrameworkElement _offsetYRow;
        private readonly FrameworkElement _xRow;
        private readonly FrameworkElement _yRow;

        private readonly Border _targetEditorBorder;
        private readonly Border _selectorEditorBorder;
        private readonly Border _parentObjectEditorBorder;
        private readonly Border _destinationParentObjectEditorBorder;
        private readonly Border _destinationSelectorEditorBorder;
        private readonly Border _valueEditorBorder;
        private readonly Border _inputValueEditorBorder;
        private readonly Border _setValueEditorBorder;
        private readonly Border _outputResultEditorBorder;
        private readonly Border _keysEditorBorder;
        private readonly Border _nameEditorBorder;
        private readonly Border _uploadPathsEditorBorder;
        private readonly Border _offsetXEditorBorder;
        private readonly Border _offsetYEditorBorder;
        private readonly Border _xEditorBorder;
        private readonly Border _yEditorBorder;

        private readonly ExpressionTextBox _targetExpressionBox;
        private readonly ExpressionTextBox _selectorExpressionBox;
        private readonly ExpressionTextBox _parentObjectExpressionBox;
        private readonly ExpressionTextBox _destinationParentObjectExpressionBox;
        private readonly ExpressionTextBox _destinationSelectorExpressionBox;
        private readonly ExpressionTextBox _valueExpressionBox;
        private readonly ExpressionTextBox _inputValueExpressionBox;
        private readonly ExpressionTextBox _setValueExpressionBox;
        private readonly ExpressionTextBox _outputResultExpressionBox;
        private readonly ExpressionTextBox _keysExpressionBox;
        private readonly ExpressionTextBox _nameExpressionBox;
        private readonly ExpressionTextBox _uploadPathsExpressionBox;
        private readonly ExpressionTextBox _offsetXExpressionBox;
        private readonly ExpressionTextBox _offsetYExpressionBox;
        private readonly ExpressionTextBox _xExpressionBox;
        private readonly ExpressionTextBox _yExpressionBox;

        private readonly ComboBox _byComboBox;
        private readonly ComboBox _operationTypeComboBox;
        private readonly ComboBox _textTypeComboBox;

        private DesignerMode _designerMode = DesignerMode.General;
        private bool _isSyncingBy;
        private bool _isSyncingOperationType;

        public CdpElementTargetActivityDesigner()
        {
            var host = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(4)
            };

            _rootPanel = new Border
            {
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 5, 6, 5)
            };

            var body = new StackPanel { Orientation = Orientation.Vertical };
            Grid.SetIsSharedSizeScope(body, true);

            _targetExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Target", typeof(CdpBase));
            _targetRow = CdpDesignerShared.CreateRow("Target", _targetExpressionBox, LabelColumn, out _targetEditorBorder);
            body.Children.Add(_targetRow);

            _parentObjectExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("ParentObject", typeof(CdpBase));
            _parentObjectRow = CdpDesignerShared.CreateRow(
                "Parent Object",
                _parentObjectExpressionBox,
                LabelColumn,
                out _parentObjectEditorBorder);
            body.Children.Add(_parentObjectRow);

            _selectorExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Selector", typeof(string));
            _selectorRow = SelectorDesignerSupport.CreateSelectorRow(
                "Selector",
                _selectorExpressionBox,
                "Selector",
                () => ModelItem,
                LabelColumn,
                CdpDesignerShared.EditorMinWidth,
                out _selectorEditorBorder,
                CdpDesignerShared.RowSpacing);
            body.Children.Add(_selectorRow);

            _destinationParentObjectExpressionBox = CdpDesignerShared.CreateInExpressionTextBox(
                "DestinationParentObject",
                typeof(CdpBase));
            _destinationParentObjectRow = CdpDesignerShared.CreateRow(
                "Dest Parent",
                _destinationParentObjectExpressionBox,
                LabelColumn,
                out _destinationParentObjectEditorBorder,
                CdpDesignerShared.RowSpacing);
            body.Children.Add(_destinationParentObjectRow);

            _destinationSelectorExpressionBox = CdpDesignerShared.CreateInExpressionTextBox(
                "DestinationSelector",
                typeof(string));
            _destinationSelectorRow = SelectorDesignerSupport.CreateSelectorRow(
                "Dest Selector",
                _destinationSelectorExpressionBox,
                "DestinationSelector",
                () => ModelItem,
                LabelColumn,
                CdpDesignerShared.EditorMinWidth,
                out _destinationSelectorEditorBorder,
                CdpDesignerShared.RowSpacing);
            body.Children.Add(_destinationSelectorRow);

            _byComboBox = CdpDesignerShared.BuildEnumComboBox<CdpActivitySelectBy>();
            _byComboBox.SelectionChanged += OnBySelectionChanged;
            _byRow = CdpDesignerShared.CreateRow("By", _byComboBox, LabelColumn, CdpDesignerShared.RowSpacing);
            body.Children.Add(_byRow);

            _operationTypeComboBox = new ComboBox { IsEditable = false };
            _operationTypeComboBox.SelectionChanged += OnOperationTypeSelectionChanged;
            _operationTypeRow = CdpDesignerShared.CreateRow("Type", _operationTypeComboBox, LabelColumn, CdpDesignerShared.RowSpacing);
            body.Children.Add(_operationTypeRow);

            _textTypeComboBox = CdpDesignerShared.BuildEnumComboBox<CdpElementTextType>();
            _textTypeRow = CdpDesignerShared.CreateRow("Type", _textTypeComboBox, LabelColumn, CdpDesignerShared.RowSpacing);
            body.Children.Add(_textTypeRow);

            _valueExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Value", typeof(object[]));
            _valueRow = CdpDesignerShared.CreateRow("Value", _valueExpressionBox, LabelColumn, out _valueEditorBorder, CdpDesignerShared.RowSpacing);
            body.Children.Add(_valueRow);

            _inputValueExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Value", typeof(object));
            _inputValueRow = CdpDesignerShared.CreateRow("Value", _inputValueExpressionBox, LabelColumn, out _inputValueEditorBorder, CdpDesignerShared.RowSpacing);
            body.Children.Add(_inputValueRow);

            _setValueExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("SetValue", typeof(string));
            _setValueRow = CdpDesignerShared.CreateRow("Set Value", _setValueExpressionBox, LabelColumn, out _setValueEditorBorder, CdpDesignerShared.RowSpacing);
            body.Children.Add(_setValueRow);

            _outputResultExpressionBox = CdpDesignerShared.CreateOutExpressionTextBox("OutputResult", typeof(string));
            _outputResultRow = CdpDesignerShared.CreateRow("Output Result", _outputResultExpressionBox, LabelColumn, out _outputResultEditorBorder, CdpDesignerShared.RowSpacing);
            body.Children.Add(_outputResultRow);

            _keysExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Keys", typeof(object[]));
            _keysRow = CdpDesignerShared.CreateRow("Keys", _keysExpressionBox, LabelColumn, out _keysEditorBorder, CdpDesignerShared.RowSpacing);
            body.Children.Add(_keysRow);

            _nameExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Name", typeof(string));
            _nameRow = CdpDesignerShared.CreateRow("Name", _nameExpressionBox, LabelColumn, out _nameEditorBorder, CdpDesignerShared.RowSpacing);
            body.Children.Add(_nameRow);

            _uploadPathsExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("UploadFilePaths", typeof(string[]));
            _uploadPathsRow = CdpDesignerShared.CreateRow("Upload Paths", _uploadPathsExpressionBox, LabelColumn, out _uploadPathsEditorBorder, CdpDesignerShared.RowSpacing);
            body.Children.Add(_uploadPathsRow);

            _offsetXExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("OffsetX", typeof(int));
            _offsetXRow = CdpDesignerShared.CreateRow("Offset X", _offsetXExpressionBox, LabelColumn, out _offsetXEditorBorder, CdpDesignerShared.RowSpacing);
            body.Children.Add(_offsetXRow);

            _offsetYExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("OffsetY", typeof(int));
            _offsetYRow = CdpDesignerShared.CreateRow("Offset Y", _offsetYExpressionBox, LabelColumn, out _offsetYEditorBorder, CdpDesignerShared.RowSpacing);
            body.Children.Add(_offsetYRow);

            _xExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("X", typeof(int));
            _xRow = CdpDesignerShared.CreateRow("X", _xExpressionBox, LabelColumn, out _xEditorBorder, CdpDesignerShared.RowSpacing);
            body.Children.Add(_xRow);

            _yExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Y", typeof(int));
            _yRow = CdpDesignerShared.CreateRow("Y", _yExpressionBox, LabelColumn, out _yEditorBorder, CdpDesignerShared.RowSpacing);
            body.Children.Add(_yRow);

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

            CdpDesignerShared.BindExpressionOwner(_rootPanel, ModelItem);
            _designerMode = ReadDesignerMode(ModelItem);
            ConfigureOperationTypeCombo();
            RefreshAllRows();
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void ConfigureOperationTypeCombo()
        {
            _operationTypeComboBox.Items.Clear();
            switch (_designerMode)
            {
                case DesignerMode.Attribute:
                    _operationTypeComboBox.Items.Add(CdpAttributeOperationType.Get);
                    _operationTypeComboBox.Items.Add(CdpAttributeOperationType.Set);
                    _operationTypeComboBox.Items.Add(CdpAttributeOperationType.Remove);
                    break;
                case DesignerMode.Style:
                    _operationTypeComboBox.Items.Add(CdpStyleOperationType.Get);
                    _operationTypeComboBox.Items.Add(CdpStyleOperationType.Set);
                    _operationTypeComboBox.Items.Add(CdpStyleOperationType.Remove);
                    break;
                case DesignerMode.Property:
                    _operationTypeComboBox.Items.Add(CdpPropertyOperationType.Get);
                    _operationTypeComboBox.Items.Add(CdpPropertyOperationType.Set);
                    break;
                case DesignerMode.Class:
                    _operationTypeComboBox.Items.Add(CdpClassOperationType.Exists);
                    _operationTypeComboBox.Items.Add(CdpClassOperationType.Add);
                    _operationTypeComboBox.Items.Add(CdpClassOperationType.Remove);
                    break;
            }
        }

        private void RefreshAllRows()
        {
            RefreshTargetRows();
            RefreshSpecificRows();

            if (_designerMode == DesignerMode.Select || _designerMode == DesignerMode.SelectCancel)
            {
                var by = ReadBy(ModelItem);
                CdpDesignerShared.SyncCombo(_byComboBox, by, ref _isSyncingBy);
            }

            if (_designerMode == DesignerMode.Attribute)
            {
                var type = ReadAttributeType(ModelItem);
                CdpDesignerShared.SyncCombo(_operationTypeComboBox, type, ref _isSyncingOperationType);
                RefreshAttributeValueRow(type);
            }
            else if (_designerMode == DesignerMode.Style)
            {
                var type = ReadStyleType(ModelItem);
                CdpDesignerShared.SyncCombo(_operationTypeComboBox, type, ref _isSyncingOperationType);
                RefreshStyleValueRow(type);
            }
            else if (_designerMode == DesignerMode.Property)
            {
                var type = ReadPropertyType(ModelItem);
                CdpDesignerShared.SyncCombo(_operationTypeComboBox, type, ref _isSyncingOperationType);
                RefreshPropertyValueRow(type);
            }
            else if (_designerMode == DesignerMode.Class)
            {
                var type = ReadClassType(ModelItem);
                CdpDesignerShared.SyncCombo(_operationTypeComboBox, type, ref _isSyncingOperationType);
            }
            else if (_designerMode == DesignerMode.GetText)
            {
                var type = ReadTextType(ModelItem);
                CdpDesignerShared.SyncCombo(_textTypeComboBox, type, ref _isSyncingOperationType);
            }
        }

        private void RefreshTargetRows()
        {
            if (_designerMode == DesignerMode.DragToElement)
            {
                _targetRow.Visibility = Visibility.Collapsed;
                _parentObjectRow.Visibility = Visibility.Visible;
                _selectorRow.Visibility = Visibility.Visible;
                _destinationParentObjectRow.Visibility = Visibility.Visible;
                _destinationSelectorRow.Visibility = Visibility.Visible;
                return;
            }

            _parentObjectRow.Visibility = Visibility.Collapsed;
            _destinationParentObjectRow.Visibility = Visibility.Collapsed;
            _destinationSelectorRow.Visibility = Visibility.Collapsed;
            _targetRow.Visibility = Visibility.Visible;
            _selectorRow.Visibility = Visibility.Visible;
        }

        private void RefreshSpecificRows()
        {
            _byRow.Visibility = _designerMode == DesignerMode.Select || _designerMode == DesignerMode.SelectCancel
                ? Visibility.Visible
                : Visibility.Collapsed;
            _valueRow.Visibility = _designerMode == DesignerMode.Select || _designerMode == DesignerMode.SelectCancel
                ? Visibility.Visible
                : Visibility.Collapsed;
            _inputValueRow.Visibility = _designerMode == DesignerMode.Input ? Visibility.Visible : Visibility.Collapsed;
            _setValueRow.Visibility = Visibility.Collapsed;
            _outputResultRow.Visibility = Visibility.Collapsed;
            _keysRow.Visibility = _designerMode == DesignerMode.SendKeys ? Visibility.Visible : Visibility.Collapsed;
            _nameRow.Visibility = _designerMode == DesignerMode.Attribute ||
                                  _designerMode == DesignerMode.Style ||
                                  _designerMode == DesignerMode.Property ||
                                  _designerMode == DesignerMode.Class
                ? Visibility.Visible
                : Visibility.Collapsed;
            _uploadPathsRow.Visibility = _designerMode == DesignerMode.Upload ? Visibility.Visible : Visibility.Collapsed;
            _offsetXRow.Visibility = _designerMode == DesignerMode.DragOffset ? Visibility.Visible : Visibility.Collapsed;
            _offsetYRow.Visibility = _designerMode == DesignerMode.DragOffset ? Visibility.Visible : Visibility.Collapsed;
            _xRow.Visibility = _designerMode == DesignerMode.DragToLocation ? Visibility.Visible : Visibility.Collapsed;
            _yRow.Visibility = _designerMode == DesignerMode.DragToLocation ? Visibility.Visible : Visibility.Collapsed;
            _textTypeRow.Visibility = _designerMode == DesignerMode.GetText ? Visibility.Visible : Visibility.Collapsed;

            var showOperationType = _designerMode == DesignerMode.Attribute ||
                                    _designerMode == DesignerMode.Style ||
                                    _designerMode == DesignerMode.Property ||
                                    _designerMode == DesignerMode.Class;
            _operationTypeRow.Visibility = showOperationType ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshAttributeValueRow(CdpAttributeOperationType type)
        {
            _setValueRow.Visibility = type == CdpAttributeOperationType.Set ? Visibility.Visible : Visibility.Collapsed;
            _outputResultRow.Visibility = type == CdpAttributeOperationType.Get ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshStyleValueRow(CdpStyleOperationType type)
        {
            _setValueRow.Visibility = type == CdpStyleOperationType.Set ? Visibility.Visible : Visibility.Collapsed;
            _outputResultRow.Visibility = type == CdpStyleOperationType.Get ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshPropertyValueRow(CdpPropertyOperationType type)
        {
            _setValueRow.Visibility = type == CdpPropertyOperationType.Set ? Visibility.Visible : Visibility.Collapsed;
            _outputResultRow.Visibility = type == CdpPropertyOperationType.Get ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnBySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingBy || ModelItem == null)
            {
                return;
            }

            if (_byComboBox.SelectedItem is CdpActivitySelectBy by)
            {
                ModelItem.Properties["By"].SetValue(by);
                RefreshRequiredBorders();
            }
        }

        private void OnOperationTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingOperationType || ModelItem == null)
            {
                return;
            }

            if (_designerMode == DesignerMode.Attribute && _operationTypeComboBox.SelectedItem is CdpAttributeOperationType attributeType)
            {
                ModelItem.Properties["Type"].SetValue(attributeType);
                RefreshAttributeValueRow(attributeType);
            }
            else if (_designerMode == DesignerMode.Style && _operationTypeComboBox.SelectedItem is CdpStyleOperationType styleType)
            {
                ModelItem.Properties["Type"].SetValue(styleType);
                RefreshStyleValueRow(styleType);
            }
            else if (_designerMode == DesignerMode.Property && _operationTypeComboBox.SelectedItem is CdpPropertyOperationType propertyType)
            {
                ModelItem.Properties["Type"].SetValue(propertyType);
                RefreshPropertyValueRow(propertyType);
            }
            else if (_designerMode == DesignerMode.Class && _operationTypeComboBox.SelectedItem is CdpClassOperationType classType)
            {
                ModelItem.Properties["Type"].SetValue(classType);
            }

            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ModelItem == null)
                {
                    return;
                }

                RefreshAllRows();
                RefreshRequiredBorders();
            }), DispatcherPriority.Background);
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
            {
                return;
            }

            if (_designerMode == DesignerMode.DragToElement)
            {
                // Positive combos: Element+empty | Tab/Frame+relative | null+wnd(+ctrl).
                // Parent is required only for a relative Selector; Selector is required only when Parent is empty.
                var hasParent = CdpDesignerShared.IsArgumentFilled(
                    ModelItem,
                    "ParentObject",
                    _parentObjectExpressionBox);
                var hasSelector = CdpDesignerShared.IsArgumentFilled(ModelItem, "Selector", _selectorExpressionBox);
                var hasDestParent = CdpDesignerShared.IsArgumentFilled(
                    ModelItem,
                    "DestinationParentObject",
                    _destinationParentObjectExpressionBox);
                var hasDestSelector = CdpDesignerShared.IsArgumentFilled(
                    ModelItem,
                    "DestinationSelector",
                    _destinationSelectorExpressionBox);
                var sourceNeedsParent = hasSelector && !CdpDesignerShared.SelectorHasWnd(ModelItem);
                var destNeedsParent = hasDestSelector &&
                                      !CdpDesignerShared.SelectorHasWnd(ModelItem, "DestinationSelector");
                var sourceNeedsSelector = !hasParent;
                var destNeedsSelector = !hasDestParent;

                CdpDesignerShared.SetRequiredBorder(_parentObjectEditorBorder, sourceNeedsParent, hasParent);
                CdpDesignerShared.SetRequiredBorder(_selectorEditorBorder, sourceNeedsSelector, hasSelector);
                CdpDesignerShared.SetRequiredBorder(
                    _destinationParentObjectEditorBorder,
                    destNeedsParent,
                    hasDestParent);
                CdpDesignerShared.SetRequiredBorder(
                    _destinationSelectorEditorBorder,
                    destNeedsSelector,
                    hasDestSelector);
            }
            else
            {
                var needsTarget = !CdpDesignerShared.SelectorHasWnd(ModelItem);
                CdpDesignerShared.SetRequiredBorder(
                    _targetEditorBorder,
                    needsTarget,
                    CdpDesignerShared.IsArgumentFilled(ModelItem, "Target", _targetExpressionBox));
                CdpDesignerShared.SetRequiredBorder(
                    _selectorEditorBorder,
                    false,
                    CdpDesignerShared.IsArgumentFilled(ModelItem, "Selector", _selectorExpressionBox));
            }

            CdpDesignerShared.SetRequiredBorder(
                _valueEditorBorder,
                _valueRow.Visibility == Visibility.Visible,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "Value", _valueExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _inputValueEditorBorder,
                _inputValueRow.Visibility == Visibility.Visible,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "Value", _inputValueExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _setValueEditorBorder,
                _setValueRow.Visibility == Visibility.Visible,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "SetValue", _setValueExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _outputResultEditorBorder,
                _outputResultRow.Visibility == Visibility.Visible,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "OutputResult", _outputResultExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _keysEditorBorder,
                _designerMode == DesignerMode.SendKeys,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "Keys", _keysExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _nameEditorBorder,
                _nameRow.Visibility == Visibility.Visible,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "Name", _nameExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _uploadPathsEditorBorder,
                _designerMode == DesignerMode.Upload,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "UploadFilePaths", _uploadPathsExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _offsetXEditorBorder,
                _designerMode == DesignerMode.DragOffset,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "OffsetX", _offsetXExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _offsetYEditorBorder,
                _designerMode == DesignerMode.DragOffset,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "OffsetY", _offsetYExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _xEditorBorder,
                _designerMode == DesignerMode.DragToLocation,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "X", _xExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _yEditorBorder,
                _designerMode == DesignerMode.DragToLocation,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "Y", _yExpressionBox));
        }

        private static DesignerMode ReadDesignerMode(ModelItem modelItem)
        {
            var typeName = modelItem?.ItemType?.Name;
            switch (typeName)
            {
                case nameof(ElementInputActivity):
                    return DesignerMode.Input;
                case nameof(ElementSendKeysActivity):
                    return DesignerMode.SendKeys;
                case nameof(ElementSelectActivity):
                    return DesignerMode.Select;
                case nameof(ElementSelectCancelActivity):
                    return DesignerMode.SelectCancel;
                case nameof(ElementAttributeActivity):
                    return DesignerMode.Attribute;
                case nameof(ElementStyleActivity):
                    return DesignerMode.Style;
                case nameof(ElementPropertyActivity):
                    return DesignerMode.Property;
                case nameof(ElementClassActivity):
                    return DesignerMode.Class;
                case nameof(ElementGetTextActivity):
                    return DesignerMode.GetText;
                case nameof(ElementClickForUploadActivity):
                    return DesignerMode.Upload;
                case nameof(ElementDragToElementActivity):
                    return DesignerMode.DragToElement;
                case nameof(ElementDragOffsetActivity):
                    return DesignerMode.DragOffset;
                case nameof(ElementDragToLocationActivity):
                    return DesignerMode.DragToLocation;
                default:
                    return DesignerMode.General;
            }
        }

        private static CdpActivitySelectBy ReadBy(ModelItem modelItem)
        {
            if (modelItem?.Properties["By"]?.ComputedValue is CdpActivitySelectBy value)
            {
                return value;
            }

            return CdpActivitySelectBy.Text;
        }

        private static CdpAttributeOperationType ReadAttributeType(ModelItem modelItem)
        {
            if (modelItem?.Properties["Type"]?.ComputedValue is CdpAttributeOperationType value)
            {
                return value;
            }

            return CdpAttributeOperationType.Get;
        }

        private static CdpStyleOperationType ReadStyleType(ModelItem modelItem)
        {
            if (modelItem?.Properties["Type"]?.ComputedValue is CdpStyleOperationType value)
            {
                return value;
            }

            return CdpStyleOperationType.Get;
        }

        private static CdpPropertyOperationType ReadPropertyType(ModelItem modelItem)
        {
            if (modelItem?.Properties["Type"]?.ComputedValue is CdpPropertyOperationType value)
            {
                return value;
            }

            return CdpPropertyOperationType.Get;
        }

        private static CdpClassOperationType ReadClassType(ModelItem modelItem)
        {
            if (modelItem?.Properties["Type"]?.ComputedValue is CdpClassOperationType value)
            {
                return value;
            }

            return CdpClassOperationType.Exists;
        }

        private static CdpElementTextType ReadTextType(ModelItem modelItem)
        {
            if (modelItem?.Properties["Type"]?.ComputedValue is CdpElementTextType value)
            {
                return value;
            }

            return CdpElementTextType.InnerText;
        }

        private enum DesignerMode
        {
            General,
            Input,
            SendKeys,
            Select,
            SelectCancel,
            Attribute,
            Style,
            Property,
            Class,
            GetText,
            Upload,
            DragToElement,
            DragOffset,
            DragToLocation
        }
    }
}
