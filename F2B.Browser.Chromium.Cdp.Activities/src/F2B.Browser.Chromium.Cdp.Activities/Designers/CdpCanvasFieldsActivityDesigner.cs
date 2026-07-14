using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.OpenRpa.Design;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    public sealed class CdpCanvasFieldsActivityDesigner : ActivityDesigner
    {
        private const string LabelColumn = "CdpCanvasFieldLabelColumn";

        private readonly Border _rootPanel;

        private readonly FrameworkElement _windowStateRow;
        private readonly ComboBox _windowStateComboBox;

        private readonly FrameworkElement _parentObjectRow;
        private readonly Border _parentObjectEditorBorder;
        private readonly ExpressionTextBox _parentObjectExpressionBox;

        private readonly FrameworkElement _targetRow;
        private readonly Border _targetEditorBorder;
        private readonly ExpressionTextBox _targetExpressionBox;

        private readonly FrameworkElement _findSelectorRow;
        private readonly Border _findSelectorEditorBorder;
        private readonly ExpressionTextBox _findSelectorExpressionBox;

        private readonly FrameworkElement _compositeSelectorRow;
        private readonly Border _compositeSelectorEditorBorder;
        private readonly ExpressionTextBox _compositeSelectorExpressionBox;

        private readonly FrameworkElement _elementExistsSelectorRow;
        private readonly Border _elementExistsSelectorEditorBorder;
        private readonly ExpressionTextBox _elementExistsSelectorExpressionBox;

        private readonly FrameworkElement _scriptRow;
        private readonly Border _scriptEditorBorder;
        private readonly ExpressionTextBox _scriptExpressionBox;

        private readonly FrameworkElement _saveFilePathRow;
        private readonly Border _saveFilePathEditorBorder;
        private readonly ExpressionTextBox _saveFilePathExpressionBox;

        private readonly FrameworkElement _directionRow;
        private readonly ComboBox _directionComboBox;
        private readonly FrameworkElement _pixelsRow;
        private readonly Border _pixelsEditorBorder;
        private readonly ExpressionTextBox _pixelsExpressionBox;

        private readonly FrameworkElement _navigateTypeRow;
        private readonly ComboBox _navigateTypeComboBox;
        private readonly FrameworkElement _navigateUrlRow;
        private readonly Border _navigateUrlEditorBorder;
        private readonly ExpressionTextBox _navigateUrlExpressionBox;

        private readonly FrameworkElement _tabGetSpecificSelectorRow;
        private readonly Border _tabGetSpecificSelectorEditorBorder;
        private readonly ExpressionTextBox _tabGetSpecificSelectorExpressionBox;

        private readonly FrameworkElement _requestUrlRow;
        private readonly Border _requestUrlEditorBorder;
        private readonly ExpressionTextBox _requestUrlExpressionBox;
        private readonly FrameworkElement _postDataRow;
        private readonly Border _postDataEditorBorder;
        private readonly ExpressionTextBox _postDataExpressionBox;
        private readonly FrameworkElement _postDictRow;
        private readonly Border _postDictEditorBorder;
        private readonly ExpressionTextBox _postDictExpressionBox;

        private DesignerMode _designerMode = DesignerMode.Empty;
        private bool _isSyncingNavigateType;
        private bool _isUpdatingNavigateArguments;
        private bool _isSyncingWindowState;
        private bool _isSyncingDirection;

        public CdpCanvasFieldsActivityDesigner()
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

            _windowStateComboBox = CdpDesignerShared.BuildEnumComboBox<CdpBrowserWindowStateOption>();
            _windowStateComboBox.SelectionChanged += OnWindowStateSelectionChanged;
            _windowStateRow = CdpDesignerShared.CreateRow("WindowState", _windowStateComboBox, LabelColumn);
            body.Children.Add(_windowStateRow);

            _parentObjectExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("ParentObject", typeof(CdpBase));
            _parentObjectRow = CdpDesignerShared.CreateRow(
                "Parent Object",
                _parentObjectExpressionBox,
                LabelColumn,
                out _parentObjectEditorBorder);
            body.Children.Add(_parentObjectRow);

            _findSelectorExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Selector", typeof(string));
            _findSelectorRow = SelectorDesignerSupport.CreateSelectorRow(
                "Selector",
                _findSelectorExpressionBox,
                "Selector",
                () => ModelItem,
                LabelColumn,
                CdpDesignerShared.EditorMinWidth,
                out _findSelectorEditorBorder,
                CdpDesignerShared.RowSpacing);
            body.Children.Add(_findSelectorRow);

            _elementExistsSelectorExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Selector", typeof(string));
            _elementExistsSelectorRow = SelectorDesignerSupport.CreateSelectorRow(
                "Selector",
                _elementExistsSelectorExpressionBox,
                "Selector",
                () => ModelItem,
                LabelColumn,
                CdpDesignerShared.EditorMinWidth,
                out _elementExistsSelectorEditorBorder,
                CdpDesignerShared.RowSpacing);
            body.Children.Add(_elementExistsSelectorRow);

            _targetExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Target", typeof(CdpBase));
            _targetRow = CdpDesignerShared.CreateRow(
                "Target",
                _targetExpressionBox,
                LabelColumn,
                out _targetEditorBorder);
            body.Children.Add(_targetRow);

            _compositeSelectorExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Selector", typeof(string));
            _compositeSelectorRow = SelectorDesignerSupport.CreateSelectorRow(
                "Selector",
                _compositeSelectorExpressionBox,
                "Selector",
                () => ModelItem,
                LabelColumn,
                CdpDesignerShared.EditorMinWidth,
                out _compositeSelectorEditorBorder,
                CdpDesignerShared.RowSpacing);
            body.Children.Add(_compositeSelectorRow);

            _scriptExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Script", typeof(string));
            _scriptRow = CdpDesignerShared.CreateRow(
                "Script",
                _scriptExpressionBox,
                LabelColumn,
                out _scriptEditorBorder,
                CdpDesignerShared.RowSpacing);
            body.Children.Add(_scriptRow);

            _saveFilePathExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("SaveFilePath", typeof(string));
            _saveFilePathRow = CdpDesignerShared.CreateRow(
                "SaveFilePath",
                _saveFilePathExpressionBox,
                LabelColumn,
                out _saveFilePathEditorBorder,
                CdpDesignerShared.RowSpacing);
            body.Children.Add(_saveFilePathRow);

            _directionComboBox = CdpDesignerShared.BuildEnumComboBox<CdpScrollDirection>();
            _directionComboBox.SelectionChanged += OnDirectionSelectionChanged;
            _directionRow = CdpDesignerShared.CreateRow(
                "Direction",
                _directionComboBox,
                LabelColumn,
                CdpDesignerShared.RowSpacing);
            body.Children.Add(_directionRow);

            _pixelsExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Pixels", typeof(int));
            _pixelsRow = CdpDesignerShared.CreateRow(
                "Pixels",
                _pixelsExpressionBox,
                LabelColumn,
                out _pixelsEditorBorder,
                CdpDesignerShared.RowSpacing);
            body.Children.Add(_pixelsRow);

            _navigateTypeComboBox = CdpDesignerShared.BuildEnumComboBox<CdpNavigateType>();
            _navigateTypeComboBox.SelectionChanged += OnNavigateTypeSelectionChanged;
            _navigateTypeRow = CdpDesignerShared.CreateRow("Type", _navigateTypeComboBox, LabelColumn);
            body.Children.Add(_navigateTypeRow);

            _navigateUrlExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Url", typeof(string));
            _navigateUrlRow = CdpDesignerShared.CreateRow(
                "Url",
                _navigateUrlExpressionBox,
                LabelColumn,
                out _navigateUrlEditorBorder,
                CdpDesignerShared.RowSpacing);
            body.Children.Add(_navigateUrlRow);

            _tabGetSpecificSelectorExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Selector", typeof(string));
            _tabGetSpecificSelectorRow = SelectorDesignerSupport.CreateSelectorRow(
                "Selector",
                _tabGetSpecificSelectorExpressionBox,
                "Selector",
                () => ModelItem,
                LabelColumn,
                CdpDesignerShared.EditorMinWidth,
                out _tabGetSpecificSelectorEditorBorder);
            body.Children.Add(_tabGetSpecificSelectorRow);

            _requestUrlExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Url", typeof(string));
            _requestUrlRow = CdpDesignerShared.CreateRow(
                "Url",
                _requestUrlExpressionBox,
                LabelColumn,
                out _requestUrlEditorBorder);
            body.Children.Add(_requestUrlRow);

            _postDataExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Data", typeof(string));
            _postDataRow = CdpDesignerShared.CreateRow(
                "Data",
                _postDataExpressionBox,
                LabelColumn,
                out _postDataEditorBorder,
                CdpDesignerShared.RowSpacing);
            body.Children.Add(_postDataRow);

            _postDictExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Dict", typeof(Dictionary<string, object>));
            _postDictRow = CdpDesignerShared.CreateRow(
                "Dict",
                _postDictExpressionBox,
                LabelColumn,
                out _postDictEditorBorder,
                CdpDesignerShared.RowSpacing);
            body.Children.Add(_postDictRow);

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
            _designerMode = ResolveMode(ModelItem);
            RefreshModeRows();
            RefreshModeState();
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void RefreshModeState(bool clearInactiveArguments = true)
        {
            if (ModelItem == null)
            {
                return;
            }

            switch (_designerMode)
            {
                case DesignerMode.BrowserChangeWindowState:
                    SyncWindowStateCombo(ReadWindowState(ModelItem));
                    break;
                case DesignerMode.Scroll:
                    SyncDirectionCombo(ReadDirection(ModelItem));
                    break;
                case DesignerMode.TabNavigate:
                    var navigateType = ReadNavigateType(ModelItem);
                    SyncNavigateTypeCombo(navigateType);
                    RefreshNavigateRows(navigateType);
                    if (clearInactiveArguments && !_isUpdatingNavigateArguments)
                    {
                        ClearInactiveNavigateArguments(navigateType);
                    }
                    break;
            }
        }

        private void OnWindowStateSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingWindowState || ModelItem == null || _designerMode != DesignerMode.BrowserChangeWindowState)
            {
                return;
            }

            if (_windowStateComboBox.SelectedItem is CdpBrowserWindowStateOption windowState)
            {
                ModelItem.Properties["WindowState"].SetValue(windowState);
                RefreshRequiredBorders();
            }
        }

        private void OnDirectionSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingDirection || ModelItem == null || _designerMode != DesignerMode.Scroll)
            {
                return;
            }

            if (_directionComboBox.SelectedItem is CdpScrollDirection direction)
            {
                ModelItem.Properties["Direction"].SetValue(direction);
                RefreshRequiredBorders();
            }
        }

        private void OnNavigateTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingNavigateType || ModelItem == null || _designerMode != DesignerMode.TabNavigate)
            {
                return;
            }

            if (!(_navigateTypeComboBox.SelectedItem is CdpNavigateType navigateType))
            {
                return;
            }

            ModelItem.Properties["Type"].SetValue(navigateType);
            ClearInactiveNavigateArguments(navigateType);
            RefreshNavigateRows(navigateType);
            RefreshRequiredBorders();
        }

        private void RefreshModeRows()
        {
            var collapsed = Visibility.Collapsed;
            var visible = Visibility.Visible;

            _windowStateRow.Visibility = _designerMode == DesignerMode.BrowserChangeWindowState ? visible : collapsed;

            var findLike = IsFindLikeMode(_designerMode);
            _parentObjectRow.Visibility = findLike || _designerMode == DesignerMode.ElementExists ? visible : collapsed;
            _findSelectorRow.Visibility = findLike ? visible : collapsed;
            _elementExistsSelectorRow.Visibility = _designerMode == DesignerMode.ElementExists ? visible : collapsed;

            var composite = IsCompositeMode(_designerMode);
            _targetRow.Visibility = composite ? visible : collapsed;
            _compositeSelectorRow.Visibility = composite ? visible : collapsed;

            _scriptRow.Visibility = _designerMode == DesignerMode.RunJs ? visible : collapsed;
            _saveFilePathRow.Visibility = _designerMode == DesignerMode.TakeScreenshot ? visible : collapsed;

            _directionRow.Visibility = _designerMode == DesignerMode.Scroll ? visible : collapsed;
            _pixelsRow.Visibility = _designerMode == DesignerMode.Scroll ? visible : collapsed;

            _navigateTypeRow.Visibility = _designerMode == DesignerMode.TabNavigate ? visible : collapsed;
            _navigateUrlRow.Visibility = collapsed;

            _tabGetSpecificSelectorRow.Visibility = _designerMode == DesignerMode.TabGetSpecific ? visible : collapsed;

            _requestUrlRow.Visibility = _designerMode == DesignerMode.TabGetRequest || _designerMode == DesignerMode.TabPostRequest
                ? visible
                : collapsed;
            _postDataRow.Visibility = _designerMode == DesignerMode.TabPostRequest ? visible : collapsed;
            _postDictRow.Visibility = _designerMode == DesignerMode.TabPostRequest ? visible : collapsed;
        }

        private void RefreshNavigateRows(CdpNavigateType navigateType)
        {
            if (_designerMode != DesignerMode.TabNavigate)
            {
                return;
            }

            _navigateUrlRow.Visibility = navigateType == CdpNavigateType.Navigate ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SyncWindowStateCombo(CdpBrowserWindowStateOption windowState)
        {
            CdpDesignerShared.SyncCombo(_windowStateComboBox, windowState, ref _isSyncingWindowState);
        }

        private void SyncDirectionCombo(CdpScrollDirection direction)
        {
            CdpDesignerShared.SyncCombo(_directionComboBox, direction, ref _isSyncingDirection);
        }

        private void SyncNavigateTypeCombo(CdpNavigateType navigateType)
        {
            CdpDesignerShared.SyncCombo(_navigateTypeComboBox, navigateType, ref _isSyncingNavigateType);
        }

        private void ClearInactiveNavigateArguments(CdpNavigateType navigateType)
        {
            if (ModelItem == null || _designerMode != DesignerMode.TabNavigate)
            {
                return;
            }

            _isUpdatingNavigateArguments = true;
            try
            {
                CdpDesignerShared.ClearArgumentIfHidden(ModelItem, "Url", navigateType == CdpNavigateType.Navigate);
            }
            finally
            {
                _isUpdatingNavigateArguments = false;
            }
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (ModelItem == null)
                {
                    return;
                }

                RefreshModeState(clearInactiveArguments: false);
                RefreshRequiredBorders();
            }), DispatcherPriority.Background);
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
            {
                return;
            }

            var navigateType = ReadNavigateType(ModelItem);
            var findLike = IsFindLikeMode(_designerMode);
            var findNeedsParent = findLike && !CdpDesignerShared.SelectorHasWnd(ModelItem);
            var existsNeedsParent = _designerMode == DesignerMode.ElementExists &&
                                    !CdpDesignerShared.SelectorHasWnd(ModelItem);
            var composite = IsCompositeMode(_designerMode);
            var compositeNeedsTarget = composite && !CdpDesignerShared.SelectorHasWnd(ModelItem);

            CdpDesignerShared.SetRequiredBorder(
                _parentObjectEditorBorder,
                findNeedsParent || existsNeedsParent,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "ParentObject", _parentObjectExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _findSelectorEditorBorder,
                findLike,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "Selector", _findSelectorExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _elementExistsSelectorEditorBorder,
                _designerMode == DesignerMode.ElementExists,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "Selector", _elementExistsSelectorExpressionBox));

            CdpDesignerShared.SetRequiredBorder(
                _targetEditorBorder,
                compositeNeedsTarget,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "Target", _targetExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _compositeSelectorEditorBorder,
                false,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "Selector", _compositeSelectorExpressionBox));

            CdpDesignerShared.SetRequiredBorder(
                _scriptEditorBorder,
                _designerMode == DesignerMode.RunJs,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "Script", _scriptExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _saveFilePathEditorBorder,
                _designerMode == DesignerMode.TakeScreenshot,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "SaveFilePath", _saveFilePathExpressionBox));

            CdpDesignerShared.SetRequiredBorder(
                _navigateUrlEditorBorder,
                _designerMode == DesignerMode.TabNavigate && navigateType == CdpNavigateType.Navigate,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "Url", _navigateUrlExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _tabGetSpecificSelectorEditorBorder,
                _designerMode == DesignerMode.TabGetSpecific,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "Selector", _tabGetSpecificSelectorExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _requestUrlEditorBorder,
                _designerMode == DesignerMode.TabGetRequest || _designerMode == DesignerMode.TabPostRequest,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "Url", _requestUrlExpressionBox));
        }

        private static DesignerMode ResolveMode(ModelItem modelItem)
        {
            var activityType = modelItem?.ItemType;
            if (activityType == null)
            {
                return DesignerMode.Empty;
            }

            switch (activityType.Name)
            {
                case nameof(BrowserChangeWindowStateActivity):
                    return DesignerMode.BrowserChangeWindowState;
                case nameof(FindElementActivity):
                    return DesignerMode.FindElement;
                case nameof(FindElementsActivity):
                    return DesignerMode.FindElements;
                case nameof(FindFrameActivity):
                    return DesignerMode.FindFrame;
                case nameof(ElementExistsActivity):
                    return DesignerMode.ElementExists;
                case nameof(RunJsActivity):
                    return DesignerMode.RunJs;
                case nameof(TakeScreenshotActivity):
                    return DesignerMode.TakeScreenshot;
                case nameof(ScrollActivity):
                    return DesignerMode.Scroll;
                case nameof(TabNavigateActivity):
                    return DesignerMode.TabNavigate;
                case nameof(TabGetSpecificActivity):
                    return DesignerMode.TabGetSpecific;
                case nameof(TabGetRequestActivity):
                    return DesignerMode.TabGetRequest;
                case nameof(TabPostRequestActivity):
                    return DesignerMode.TabPostRequest;
                default:
                    return DesignerMode.Empty;
            }
        }

        private static bool IsFindLikeMode(DesignerMode mode)
        {
            return mode == DesignerMode.FindElement ||
                   mode == DesignerMode.FindElements ||
                   mode == DesignerMode.FindFrame;
        }

        private static bool IsCompositeMode(DesignerMode mode)
        {
            return mode == DesignerMode.RunJs || mode == DesignerMode.TakeScreenshot || mode == DesignerMode.Scroll;
        }

        private static CdpBrowserWindowStateOption ReadWindowState(ModelItem modelItem)
        {
            if (modelItem?.Properties["WindowState"]?.ComputedValue is CdpBrowserWindowStateOption value)
            {
                return value;
            }

            return CdpBrowserWindowStateOption.Maximize;
        }

        private static CdpNavigateType ReadNavigateType(ModelItem modelItem)
        {
            if (modelItem?.Properties["Type"]?.ComputedValue is CdpNavigateType value)
            {
                return value;
            }

            return CdpNavigateType.Navigate;
        }

        private static CdpScrollDirection ReadDirection(ModelItem modelItem)
        {
            if (modelItem?.Properties["Direction"]?.ComputedValue is CdpScrollDirection value)
            {
                return value;
            }

            return CdpScrollDirection.Down;
        }

        private enum DesignerMode
        {
            Empty,
            BrowserChangeWindowState,
            FindElement,
            FindElements,
            FindFrame,
            ElementExists,
            RunJs,
            TakeScreenshot,
            Scroll,
            TabNavigate,
            TabGetSpecific,
            TabGetRequest,
            TabPostRequest
        }
    }
}
