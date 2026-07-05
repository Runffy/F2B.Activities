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
using F2B.OpenRpa.Design;

namespace F2B.Browser.Chromium.Bridge
{
    public sealed class BridgeCanvasFieldsActivityDesigner : ActivityDesigner
    {
        private const double RowLabelMinWidth = 78;
        private const double EditorMinWidth = 190;
        private const double RowSpacing = 4;

        private readonly Border _rootPanel;
        private readonly ExpressionTextBox _browserExpressionBox;
        private readonly ExpressionTextBox _tabExpressionBox;
        private readonly ExpressionTextBox _findElementTabExpressionBox;
        private readonly ExpressionTextBox _elementFindElementExpressionBox;
        private readonly ExpressionTextBox _elementFindSelectorExpressionBox;
        private readonly ExpressionTextBox _tabExistsSelectorExpressionBox;
        private readonly ExpressionTextBox _tabFindSelectorExpressionBox;
        private readonly ExpressionTextBox _tabNavigateSelectorExpressionBox;
        private readonly ExpressionTextBox _tabNavigateUrlExpressionBox;
        private readonly ExpressionTextBox _runJsInputTabExpressionBox;
        private readonly ExpressionTextBox _runJsInputElementExpressionBox;
        private readonly ExpressionTextBox _runJsSelectorExpressionBox;
        private readonly ExpressionTextBox _runJsScriptExpressionBox;
        private readonly ExpressionTextBox _sendKeysInputTabExpressionBox;
        private readonly ExpressionTextBox _sendKeysInputElementExpressionBox;
        private readonly ExpressionTextBox _sendKeysSelectorExpressionBox;
        private readonly ExpressionTextBox _sendKeysKeysExpressionBox;
        private readonly ExpressionTextBox _switchTabIndexExpressionBox;
        private readonly ExpressionTextBox _switchTabTitleExpressionBox;
        private readonly ExpressionTextBox _switchTabTitleReExpressionBox;
        private readonly ExpressionTextBox _switchTabUrlExpressionBox;
        private readonly ExpressionTextBox _switchTabUrlReExpressionBox;
        private readonly ExpressionTextBox _switchTabTabExpressionBox;
        private readonly ExpressionTextBox _getCookiesInputBrowserExpressionBox;
        private readonly ExpressionTextBox _getCookiesInputTabExpressionBox;
        private readonly FrameworkElement _findElementBaseOnRow;
        private readonly FrameworkElement _findElementTabRow;
        private readonly FrameworkElement _browserRow;
        private readonly FrameworkElement _tabRow;
        private readonly FrameworkElement _elementFindElementRow;
        private readonly FrameworkElement _elementFindSelectorRow;
        private readonly FrameworkElement _tabExistsSelectorRow;
        private readonly FrameworkElement _tabFindSelectorRow;
        private readonly FrameworkElement _tabNavigateSelectorRow;
        private readonly FrameworkElement _tabNavigateUrlRow;
        private readonly FrameworkElement _runJsBaseOnRow;
        private readonly FrameworkElement _runJsTargetTypeRow;
        private readonly FrameworkElement _runJsInputTabRow;
        private readonly FrameworkElement _runJsInputElementRow;
        private readonly FrameworkElement _runJsSelectorRow;
        private readonly FrameworkElement _runJsScriptRow;
        private readonly FrameworkElement _sendKeysBaseOnRow;
        private readonly FrameworkElement _sendKeysTargetTypeRow;
        private readonly FrameworkElement _sendKeysInputTabRow;
        private readonly FrameworkElement _sendKeysInputElementRow;
        private readonly FrameworkElement _sendKeysSelectorRow;
        private readonly FrameworkElement _sendKeysKeysRow;
        private readonly FrameworkElement _switchTabByTypeRow;
        private readonly FrameworkElement _switchTabIndexRow;
        private readonly FrameworkElement _switchTabTitleRow;
        private readonly FrameworkElement _switchTabTitleReRow;
        private readonly FrameworkElement _switchTabUrlRow;
        private readonly FrameworkElement _switchTabUrlReRow;
        private readonly FrameworkElement _switchTabTabRow;
        private readonly FrameworkElement _getCookiesBaseOnRow;
        private readonly FrameworkElement _getCookiesInputBrowserRow;
        private readonly FrameworkElement _getCookiesInputTabRow;
        private readonly FrameworkElement _getStorageScopeRow;
        private readonly Border _elementFindElementEditorBorder;
        private readonly Border _elementFindSelectorEditorBorder;
        private readonly Border _browserEditorBorder;
        private readonly Border _tabEditorBorder;
        private readonly Border _findElementTabEditorBorder;
        private readonly Border _tabExistsSelectorEditorBorder;
        private readonly Border _tabFindSelectorEditorBorder;
        private readonly Border _tabNavigateSelectorEditorBorder;
        private readonly Border _tabNavigateUrlEditorBorder;
        private readonly Border _runJsInputTabEditorBorder;
        private readonly Border _runJsInputElementEditorBorder;
        private readonly Border _runJsSelectorEditorBorder;
        private readonly Border _runJsScriptEditorBorder;
        private readonly Border _sendKeysInputTabEditorBorder;
        private readonly Border _sendKeysInputElementEditorBorder;
        private readonly Border _sendKeysSelectorEditorBorder;
        private readonly Border _sendKeysKeysEditorBorder;
        private readonly Border _switchTabIndexEditorBorder;
        private readonly Border _switchTabTitleEditorBorder;
        private readonly Border _switchTabTitleReEditorBorder;
        private readonly Border _switchTabUrlEditorBorder;
        private readonly Border _switchTabUrlReEditorBorder;
        private readonly Border _switchTabTabEditorBorder;
        private readonly Border _getCookiesInputBrowserEditorBorder;
        private readonly Border _getCookiesInputTabEditorBorder;
        private readonly ComboBox _findElementBaseOnComboBox;
        private readonly ComboBox _runJsBaseOnComboBox;
        private readonly ComboBox _runJsTargetTypeComboBox;
        private readonly ComboBox _sendKeysBaseOnComboBox;
        private readonly ComboBox _sendKeysTargetTypeComboBox;
        private readonly ComboBox _switchTabByTypeComboBox;
        private readonly ComboBox _getCookiesBaseOnComboBox;
        private readonly ComboBox _getStorageScopeComboBox;
        private readonly FrameworkElement _takeScreenshotBaseOnRow;
        private readonly FrameworkElement _takeScreenshotPathRow;
        private readonly Border _takeScreenshotPathEditorBorder;
        private readonly ExpressionTextBox _takeScreenshotPathExpressionBox;
        private readonly ComboBox _takeScreenshotBaseOnComboBox;

        private DesignerMode _designerMode = DesignerMode.None;
        private bool _isSyncingSwitchByType;
        private bool _isUpdatingSwitchTabArguments;
        private bool _isSyncingBridgeGetCookiesBaseOn;
        private bool _isSyncingBridgeStorageScope;
        private bool _isUpdatingGetCookiesArguments;
        private bool _isSyncingBridgeFindElementBaseOn;
        private bool _isUpdatingFindElementArguments;
        private bool _isSyncingBridgeRunJsBaseOn;
        private bool _isSyncingRunJsTargetType;
        private bool _isUpdatingRunJsArguments;
        private bool _isSyncingBridgeSendKeysBaseOn;
        private bool _isSyncingSendKeysTargetType;
        private bool _isUpdatingSendKeysArguments;
        private bool _isSyncingBridgeTakeScreenshotBaseOn;
        private bool _isUpdatingTakeScreenshotArguments;

        public BridgeCanvasFieldsActivityDesigner()
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

            var body = new StackPanel
            {
                Orientation = Orientation.Vertical
            };
            Grid.SetIsSharedSizeScope(body, true);

            _browserExpressionBox = CreateExpressionTextBox("Browser", typeof(BwBrowser));
            _tabExpressionBox = CreateExpressionTextBox("Tab", typeof(BwTab));
            _findElementTabExpressionBox = CreateExpressionTextBox("Tab", typeof(BwTab));
            _elementFindElementExpressionBox = CreateExpressionTextBox("Element", typeof(BwElement));
            _elementFindSelectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _tabExistsSelectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _tabFindSelectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _tabNavigateSelectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _tabNavigateUrlExpressionBox = CreateExpressionTextBox("Url", typeof(string));
            _runJsInputTabExpressionBox = CreateExpressionTextBox("InputTab", typeof(BwTab));
            _runJsInputElementExpressionBox = CreateExpressionTextBox("InputElement", typeof(BwElement));
            _runJsSelectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _runJsScriptExpressionBox = CreateExpressionTextBox("Script", typeof(string));
            _sendKeysInputTabExpressionBox = CreateExpressionTextBox("InputTab", typeof(BwTab));
            _sendKeysInputElementExpressionBox = CreateExpressionTextBox("InputElement", typeof(BwElement));
            _sendKeysSelectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _sendKeysKeysExpressionBox = CreateExpressionTextBox("Keys", typeof(string));
            _switchTabIndexExpressionBox = CreateExpressionTextBox("Index", typeof(int?));
            _switchTabTitleExpressionBox = CreateExpressionTextBox("Title", typeof(string));
            _switchTabTitleReExpressionBox = CreateExpressionTextBox("TitleRe", typeof(string));
            _switchTabUrlExpressionBox = CreateExpressionTextBox("Url", typeof(string));
            _switchTabUrlReExpressionBox = CreateExpressionTextBox("UrlRe", typeof(string));
            _switchTabTabExpressionBox = CreateExpressionTextBox("InputTab", typeof(BwTab));
            _getCookiesInputBrowserExpressionBox = CreateExpressionTextBox("InputBrowser", typeof(BwBrowser));
            _getCookiesInputTabExpressionBox = CreateExpressionTextBox("InputTab", typeof(BwTab));

            _findElementBaseOnComboBox = BuildBridgeFindElementBaseOnComboBox();
            _findElementBaseOnComboBox.SelectionChanged += OnBridgeFindElementBaseOnSelectionChanged;
            _findElementBaseOnRow = CreateRow("BaseOn", _findElementBaseOnComboBox);
            _findElementTabRow = CreateRow("Tab", _findElementTabExpressionBox, out _findElementTabEditorBorder, RowSpacing);
            _browserRow = CreateRow("Browser", _browserExpressionBox, out _browserEditorBorder);
            _tabRow = CreateRow("Tab", _tabExpressionBox, out _tabEditorBorder);
            _elementFindElementRow = CreateRow("Element", _elementFindElementExpressionBox, out _elementFindElementEditorBorder);
            _elementFindSelectorRow = SelectorDesignerSupport.CreateSelectorRow("Selector", _elementFindSelectorExpressionBox, "Selector", () => ModelItem, "CanvasFieldLabelColumn", EditorMinWidth, out _elementFindSelectorEditorBorder, RowSpacing);
            _tabExistsSelectorRow = SelectorDesignerSupport.CreateSelectorRow("Selector", _tabExistsSelectorExpressionBox, "Selector", () => ModelItem, "CanvasFieldLabelColumn", EditorMinWidth, out _tabExistsSelectorEditorBorder);
            _tabFindSelectorRow = SelectorDesignerSupport.CreateSelectorRow("Selector", _tabFindSelectorExpressionBox, "Selector", () => ModelItem, "CanvasFieldLabelColumn", EditorMinWidth, out _tabFindSelectorEditorBorder);
            _tabNavigateSelectorRow = SelectorDesignerSupport.CreateSelectorRow("Selector", _tabNavigateSelectorExpressionBox, "Selector", () => ModelItem, "CanvasFieldLabelColumn", EditorMinWidth, out _tabNavigateSelectorEditorBorder);
            _tabNavigateUrlRow = CreateRow("Url", _tabNavigateUrlExpressionBox, out _tabNavigateUrlEditorBorder, RowSpacing);
            _runJsBaseOnComboBox = BuildBridgeRunJsBaseOnComboBox();
            _runJsBaseOnComboBox.SelectionChanged += OnBridgeRunJsBaseOnSelectionChanged;
            _runJsBaseOnRow = CreateRow("BaseOn", _runJsBaseOnComboBox);
            _runJsTargetTypeComboBox = BuildRunJsTargetTypeComboBox();
            _runJsTargetTypeComboBox.SelectionChanged += OnRunJsTargetTypeSelectionChanged;
            _runJsTargetTypeRow = CreateRow("TargetType", _runJsTargetTypeComboBox, RowSpacing);
            _runJsInputTabRow = CreateRow("InputTab", _runJsInputTabExpressionBox, out _runJsInputTabEditorBorder, RowSpacing);
            _runJsInputElementRow = CreateRow("InputElement", _runJsInputElementExpressionBox, out _runJsInputElementEditorBorder, RowSpacing);
            _runJsSelectorRow = SelectorDesignerSupport.CreateSelectorRow("Selector", _runJsSelectorExpressionBox, "Selector", () => ModelItem, "CanvasFieldLabelColumn", EditorMinWidth, out _runJsSelectorEditorBorder, RowSpacing);
            _runJsScriptRow = CreateRow("Script", _runJsScriptExpressionBox, out _runJsScriptEditorBorder, RowSpacing);
            _sendKeysBaseOnComboBox = BuildBridgeSendKeysBaseOnComboBox();
            _sendKeysBaseOnComboBox.SelectionChanged += OnBridgeSendKeysBaseOnSelectionChanged;
            _sendKeysBaseOnRow = CreateRow("BaseOn", _sendKeysBaseOnComboBox);
            _sendKeysTargetTypeComboBox = BuildSendKeysTargetTypeComboBox();
            _sendKeysTargetTypeComboBox.SelectionChanged += OnSendKeysTargetTypeSelectionChanged;
            _sendKeysTargetTypeRow = CreateRow("TargetType", _sendKeysTargetTypeComboBox, RowSpacing);
            _sendKeysInputTabRow = CreateRow("InputTab", _sendKeysInputTabExpressionBox, out _sendKeysInputTabEditorBorder, RowSpacing);
            _sendKeysInputElementRow = CreateRow("InputElement", _sendKeysInputElementExpressionBox, out _sendKeysInputElementEditorBorder, RowSpacing);
            _sendKeysSelectorRow = SelectorDesignerSupport.CreateSelectorRow("Selector", _sendKeysSelectorExpressionBox, "Selector", () => ModelItem, "CanvasFieldLabelColumn", EditorMinWidth, out _sendKeysSelectorEditorBorder, RowSpacing);
            _sendKeysKeysRow = CreateRow("Keys", _sendKeysKeysExpressionBox, out _sendKeysKeysEditorBorder, RowSpacing);

            _switchTabByTypeComboBox = BuildSwitchTabByTypeComboBox();
            _switchTabByTypeComboBox.SelectionChanged += OnSwitchTabByTypeSelectionChanged;
            _switchTabByTypeRow = CreateRow("ByType", _switchTabByTypeComboBox);
            _switchTabIndexRow = CreateRow("Index", _switchTabIndexExpressionBox, out _switchTabIndexEditorBorder, RowSpacing);
            _switchTabTitleRow = CreateRow("Title", _switchTabTitleExpressionBox, out _switchTabTitleEditorBorder, RowSpacing);
            _switchTabTitleReRow = CreateRow("TitleRe", _switchTabTitleReExpressionBox, out _switchTabTitleReEditorBorder, RowSpacing);
            _switchTabUrlRow = CreateRow("Url", _switchTabUrlExpressionBox, out _switchTabUrlEditorBorder, RowSpacing);
            _switchTabUrlReRow = CreateRow("UrlRe", _switchTabUrlReExpressionBox, out _switchTabUrlReEditorBorder, RowSpacing);
            _switchTabTabRow = CreateRow("Tab", _switchTabTabExpressionBox, out _switchTabTabEditorBorder, RowSpacing);
            _getCookiesBaseOnComboBox = BuildBridgeGetCookiesBaseOnComboBox();
            _getCookiesBaseOnComboBox.SelectionChanged += OnBridgeGetCookiesBaseOnSelectionChanged;
            _getCookiesBaseOnRow = CreateRow("BaseOn", _getCookiesBaseOnComboBox);
            _getCookiesInputBrowserRow = CreateRow("InputBrowser", _getCookiesInputBrowserExpressionBox, out _getCookiesInputBrowserEditorBorder, RowSpacing);
            _getCookiesInputTabRow = CreateRow("InputTab", _getCookiesInputTabExpressionBox, out _getCookiesInputTabEditorBorder, RowSpacing);
            _getStorageScopeComboBox = BuildBridgeStorageScopeComboBox();
            _getStorageScopeComboBox.SelectionChanged += OnBridgeStorageScopeSelectionChanged;
            _getStorageScopeRow = CreateRow("Scope", _getStorageScopeComboBox, RowSpacing);
            _takeScreenshotPathExpressionBox = CreateExpressionTextBox("Path", typeof(string));
            _takeScreenshotBaseOnComboBox = BuildBridgeTakeScreenshotBaseOnComboBox();
            _takeScreenshotBaseOnComboBox.SelectionChanged += OnBridgeTakeScreenshotBaseOnSelectionChanged;
            _takeScreenshotBaseOnRow = CreateRow("BaseOn", _takeScreenshotBaseOnComboBox);
            _takeScreenshotPathRow = CreateRow("Path", _takeScreenshotPathExpressionBox, out _takeScreenshotPathEditorBorder, RowSpacing);

            body.Children.Add(_takeScreenshotBaseOnRow);
            body.Children.Add(_browserRow);
            body.Children.Add(_tabRow);
            body.Children.Add(_findElementBaseOnRow);
            body.Children.Add(_findElementTabRow);
            body.Children.Add(_elementFindElementRow);
            body.Children.Add(_elementFindSelectorRow);
            body.Children.Add(_tabExistsSelectorRow);
            body.Children.Add(_tabFindSelectorRow);
            body.Children.Add(_tabNavigateSelectorRow);
            body.Children.Add(_tabNavigateUrlRow);
            body.Children.Add(_runJsBaseOnRow);
            body.Children.Add(_runJsTargetTypeRow);
            body.Children.Add(_runJsInputTabRow);
            body.Children.Add(_runJsInputElementRow);
            body.Children.Add(_runJsSelectorRow);
            body.Children.Add(_takeScreenshotPathRow);
            body.Children.Add(_runJsScriptRow);
            body.Children.Add(_sendKeysBaseOnRow);
            body.Children.Add(_sendKeysTargetTypeRow);
            body.Children.Add(_sendKeysInputTabRow);
            body.Children.Add(_sendKeysInputElementRow);
            body.Children.Add(_sendKeysSelectorRow);
            body.Children.Add(_sendKeysKeysRow);
            body.Children.Add(_switchTabByTypeRow);
            body.Children.Add(_switchTabIndexRow);
            body.Children.Add(_switchTabTitleRow);
            body.Children.Add(_switchTabTitleReRow);
            body.Children.Add(_switchTabUrlRow);
            body.Children.Add(_switchTabUrlReRow);
            body.Children.Add(_switchTabTabRow);
            body.Children.Add(_getCookiesBaseOnRow);
            body.Children.Add(_getCookiesInputBrowserRow);
            body.Children.Add(_getCookiesInputTabRow);
            body.Children.Add(_getStorageScopeRow);

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
            BindKnownExpressionOwners(ModelItem);
            _designerMode = ResolveMode(ModelItem);
            RefreshModeRows();

            if (_designerMode == DesignerMode.BrowserSwitchTab)
            {
                var byType = ReadSwitchTabByType(ModelItem);
                SyncSwitchTabByTypeCombo(byType);
                ClearInactiveSwitchTabArguments(byType);
                RefreshSwitchTabRows(byType);
            }
            else if (IsFindElementLikeMode(_designerMode))
            {
                var baseOn = ReadBridgeFindElementBaseOn(ModelItem);
                SyncBridgeFindElementBaseOnCombo(baseOn);
                ClearInactiveFindElementArguments(baseOn);
                RefreshFindElementRows(baseOn);
            }
            else if (_designerMode == DesignerMode.GetCookies || _designerMode == DesignerMode.GetStorage)
            {
                var baseOn = ReadBridgeGetCookiesBaseOn(ModelItem);
                SyncBridgeGetCookiesBaseOnCombo(baseOn);
                ClearInactiveGetCookiesArguments(baseOn);
                RefreshGetCookiesRows(baseOn);
                if (_designerMode == DesignerMode.GetStorage)
                {
                    var scope = ReadBridgeStorageScope(ModelItem);
                    SyncBridgeStorageScopeCombo(scope);
                }
            }
            else if (_designerMode == DesignerMode.RunJs)
            {
                var runJsBaseOn = ReadBridgeRunJsBaseOn(ModelItem);
                var runJsTargetType = ReadRunJsTargetType(ModelItem);
                SyncBridgeRunJsBaseOnCombo(runJsBaseOn);
                SyncRunJsTargetTypeCombo(runJsTargetType);
                ClearInactiveRunJsArguments(runJsBaseOn, runJsTargetType);
                RefreshRunJsRows(runJsBaseOn, runJsTargetType);
            }
            else if (_designerMode == DesignerMode.SendKeys)
            {
                var sendKeysBaseOn = ReadBridgeSendKeysBaseOn(ModelItem);
                var sendKeysTargetType = ReadSendKeysTargetType(ModelItem);
                SyncBridgeSendKeysBaseOnCombo(sendKeysBaseOn);
                SyncSendKeysTargetTypeCombo(sendKeysTargetType);
                ClearInactiveSendKeysArguments(sendKeysBaseOn, sendKeysTargetType);
                RefreshSendKeysRows(sendKeysBaseOn, sendKeysTargetType);
            }
            else if (_designerMode == DesignerMode.TakeScreenshot)
            {
                var baseOn = ReadBridgeTakeScreenshotBaseOn(ModelItem);
                var targetType = ReadRunJsTargetType(ModelItem);
                SyncBridgeTakeScreenshotBaseOnCombo(baseOn);
                SyncRunJsTargetTypeCombo(targetType);
                ClearInactiveTakeScreenshotArguments(baseOn, targetType);
                RefreshTakeScreenshotRows(baseOn, targetType);
            }

            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void OnSwitchTabByTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingSwitchByType || ModelItem == null || _designerMode != DesignerMode.BrowserSwitchTab)
            {
                return;
            }

            var byType = ParseSwitchTabByType(_switchTabByTypeComboBox.SelectedItem as string);
            ModelItem.Properties["ByType"].SetValue(byType);
            ClearInactiveSwitchTabArguments(byType);
            RefreshSwitchTabRows(byType);
            RefreshRequiredBorders();
        }

        private void OnBridgeFindElementBaseOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingBridgeFindElementBaseOn || ModelItem == null || !IsFindElementLikeMode(_designerMode))
            {
                return;
            }

            var baseOn = ParseBridgeFindElementBaseOn(_findElementBaseOnComboBox.SelectedItem as string);
            ModelItem.Properties["BaseOn"].SetValue(baseOn);
            ClearInactiveFindElementArguments(baseOn);
            RefreshFindElementRows(baseOn);
            RefreshRequiredBorders();
        }

        private void OnBridgeGetCookiesBaseOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingBridgeGetCookiesBaseOn || ModelItem == null || (_designerMode != DesignerMode.GetCookies && _designerMode != DesignerMode.GetStorage))
            {
                return;
            }

            var baseOn = ParseBridgeGetCookiesBaseOn(_getCookiesBaseOnComboBox.SelectedItem as string);
            ModelItem.Properties["BaseOn"].SetValue(baseOn);
            ClearInactiveGetCookiesArguments(baseOn);
            RefreshGetCookiesRows(baseOn);
            RefreshRequiredBorders();
        }

        private void OnBridgeStorageScopeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_designerMode != DesignerMode.GetStorage || _isSyncingBridgeStorageScope || ModelItem == null)
            {
                return;
            }

            var scope = ParseBridgeStorageScope(_getStorageScopeComboBox.SelectedItem as string);
            ModelItem.Properties["Scope"].SetValue(scope);
            RefreshRequiredBorders();
        }

        private void OnBridgeRunJsBaseOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingBridgeRunJsBaseOn || ModelItem == null || _designerMode != DesignerMode.RunJs)
            {
                return;
            }

            var runJsBaseOn = ParseBridgeRunJsBaseOn(_runJsBaseOnComboBox.SelectedItem as string);
            var runJsTargetType = ReadRunJsTargetType(ModelItem);
            ModelItem.Properties["BaseOn"].SetValue(runJsBaseOn);
            ClearInactiveRunJsArguments(runJsBaseOn, runJsTargetType);
            RefreshRunJsRows(runJsBaseOn, runJsTargetType);
            RefreshRequiredBorders();
        }

        private void OnRunJsTargetTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingRunJsTargetType || ModelItem == null)
            {
                return;
            }

            if (_designerMode == DesignerMode.RunJs)
            {
                var runJsBaseOn = ReadBridgeRunJsBaseOn(ModelItem);
                var runJsTargetType = ParseRunJsTargetType(_runJsTargetTypeComboBox.SelectedItem as string);
                ModelItem.Properties["TargetType"].SetValue(runJsTargetType);
                ClearInactiveRunJsArguments(runJsBaseOn, runJsTargetType);
                RefreshRunJsRows(runJsBaseOn, runJsTargetType);
                RefreshRequiredBorders();
                return;
            }

            if (_designerMode == DesignerMode.TakeScreenshot)
            {
                var baseOn = ReadBridgeTakeScreenshotBaseOn(ModelItem);
                var targetType = ParseRunJsTargetType(_runJsTargetTypeComboBox.SelectedItem as string);
                ModelItem.Properties["TargetType"].SetValue(targetType);
                if (ModelItem.GetCurrentValue() != null)
                {
                    TypeDescriptor.Refresh(ModelItem.GetCurrentValue());
                }

                ClearInactiveTakeScreenshotArguments(baseOn, targetType);
                RefreshTakeScreenshotRows(baseOn, targetType);
                RefreshRequiredBorders();
            }
        }

        private void OnBridgeTakeScreenshotBaseOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingBridgeTakeScreenshotBaseOn || ModelItem == null || _designerMode != DesignerMode.TakeScreenshot)
            {
                return;
            }

            var baseOn = ParseBridgeTakeScreenshotBaseOn(_takeScreenshotBaseOnComboBox.SelectedItem as string);
            var targetType = ReadRunJsTargetType(ModelItem);
            ModelItem.Properties["BaseOn"].SetValue(baseOn);
            if (ModelItem.GetCurrentValue() != null)
            {
                TypeDescriptor.Refresh(ModelItem.GetCurrentValue());
            }

            ClearInactiveTakeScreenshotArguments(baseOn, targetType);
            RefreshTakeScreenshotRows(baseOn, targetType);
            RefreshRequiredBorders();
        }

        private void OnBridgeSendKeysBaseOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingBridgeSendKeysBaseOn || ModelItem == null || _designerMode != DesignerMode.SendKeys)
            {
                return;
            }

            var sendKeysBaseOn = ParseBridgeSendKeysBaseOn(_sendKeysBaseOnComboBox.SelectedItem as string);
            var sendKeysTargetType = ReadSendKeysTargetType(ModelItem);
            ModelItem.Properties["BaseOn"].SetValue(sendKeysBaseOn);
            ClearInactiveSendKeysArguments(sendKeysBaseOn, sendKeysTargetType);
            RefreshSendKeysRows(sendKeysBaseOn, sendKeysTargetType);
            RefreshRequiredBorders();
        }

        private void OnSendKeysTargetTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingSendKeysTargetType || ModelItem == null || _designerMode != DesignerMode.SendKeys)
            {
                return;
            }

            var sendKeysBaseOn = ReadBridgeSendKeysBaseOn(ModelItem);
            var sendKeysTargetType = ParseSendKeysTargetType(_sendKeysTargetTypeComboBox.SelectedItem as string);
            ModelItem.Properties["TargetType"].SetValue(sendKeysTargetType);
            ClearInactiveSendKeysArguments(sendKeysBaseOn, sendKeysTargetType);
            RefreshSendKeysRows(sendKeysBaseOn, sendKeysTargetType);
            RefreshRequiredBorders();
        }

        private void RefreshModeRows()
        {
            _browserRow.Visibility = IsBrowserMode(_designerMode) && _designerMode != DesignerMode.GetCookies ? Visibility.Visible : Visibility.Collapsed;
            _tabRow.Visibility = ResolveGeneralTabRowVisibility();
            _findElementBaseOnRow.Visibility = IsFindElementLikeMode(_designerMode) ? Visibility.Visible : Visibility.Collapsed;
            _findElementTabRow.Visibility = Visibility.Collapsed;
            _elementFindElementRow.Visibility = Visibility.Collapsed;
            _elementFindSelectorRow.Visibility = IsFindElementLikeMode(_designerMode) ? Visibility.Visible : Visibility.Collapsed;
            _tabExistsSelectorRow.Visibility = _designerMode == DesignerMode.TabElementExists ? Visibility.Visible : Visibility.Collapsed;
            _tabFindSelectorRow.Visibility = Visibility.Collapsed;
            _tabNavigateSelectorRow.Visibility = _designerMode == DesignerMode.TabNavigateUrl ? Visibility.Visible : Visibility.Collapsed;
            _tabNavigateUrlRow.Visibility = _designerMode == DesignerMode.TabNavigateUrl ? Visibility.Visible : Visibility.Collapsed;
            _runJsBaseOnRow.Visibility = _designerMode == DesignerMode.RunJs ? Visibility.Visible : Visibility.Collapsed;
            _runJsScriptRow.Visibility = _designerMode == DesignerMode.RunJs ? Visibility.Visible : Visibility.Collapsed;
            _sendKeysBaseOnRow.Visibility = _designerMode == DesignerMode.SendKeys ? Visibility.Visible : Visibility.Collapsed;
            _sendKeysTargetTypeRow.Visibility = _designerMode == DesignerMode.SendKeys ? Visibility.Visible : Visibility.Collapsed;
            _sendKeysKeysRow.Visibility = _designerMode == DesignerMode.SendKeys ? Visibility.Visible : Visibility.Collapsed;
            _switchTabByTypeRow.Visibility = _designerMode == DesignerMode.BrowserSwitchTab ? Visibility.Visible : Visibility.Collapsed;
            _getCookiesBaseOnRow.Visibility = (_designerMode == DesignerMode.GetCookies || _designerMode == DesignerMode.GetStorage) ? Visibility.Visible : Visibility.Collapsed;
            _getStorageScopeRow.Visibility = _designerMode == DesignerMode.GetStorage ? Visibility.Visible : Visibility.Collapsed;
            _takeScreenshotBaseOnRow.Visibility = _designerMode == DesignerMode.TakeScreenshot ? Visibility.Visible : Visibility.Collapsed;
            _takeScreenshotPathRow.Visibility = _designerMode == DesignerMode.TakeScreenshot ? Visibility.Visible : Visibility.Collapsed;
            _runJsTargetTypeRow.Visibility = (_designerMode == DesignerMode.RunJs || _designerMode == DesignerMode.TakeScreenshot) ? Visibility.Visible : Visibility.Collapsed;

            if (_designerMode != DesignerMode.BrowserSwitchTab)
            {
                _switchTabIndexRow.Visibility = Visibility.Collapsed;
                _switchTabTitleRow.Visibility = Visibility.Collapsed;
                _switchTabTitleReRow.Visibility = Visibility.Collapsed;
                _switchTabUrlRow.Visibility = Visibility.Collapsed;
                _switchTabUrlReRow.Visibility = Visibility.Collapsed;
                _switchTabTabRow.Visibility = Visibility.Collapsed;
            }

            if (_designerMode != DesignerMode.GetCookies)
            {
                _getCookiesInputBrowserRow.Visibility = Visibility.Collapsed;
                _getCookiesInputTabRow.Visibility = Visibility.Collapsed;
            }
            if (_designerMode != DesignerMode.GetStorage)
            {
                _getStorageScopeRow.Visibility = Visibility.Collapsed;
            }
            if (_designerMode != DesignerMode.RunJs && _designerMode != DesignerMode.TakeScreenshot)
            {
                _runJsInputTabRow.Visibility = Visibility.Collapsed;
                _runJsInputElementRow.Visibility = Visibility.Collapsed;
                _runJsSelectorRow.Visibility = Visibility.Collapsed;
            }
            if (_designerMode != DesignerMode.SendKeys)
            {
                _sendKeysInputTabRow.Visibility = Visibility.Collapsed;
                _sendKeysInputElementRow.Visibility = Visibility.Collapsed;
                _sendKeysSelectorRow.Visibility = Visibility.Collapsed;
            }
            if (!IsFindElementLikeMode(_designerMode))
            {
                _findElementTabRow.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshSwitchTabRows(BridgeSwitchTabByType byType)
        {
            if (_designerMode != DesignerMode.BrowserSwitchTab)
            {
                return;
            }

            _switchTabIndexRow.Visibility = byType == BridgeSwitchTabByType.Index ? Visibility.Visible : Visibility.Collapsed;
            _switchTabTitleRow.Visibility = byType == BridgeSwitchTabByType.Title ? Visibility.Visible : Visibility.Collapsed;
            _switchTabTitleReRow.Visibility = byType == BridgeSwitchTabByType.TitleRegex ? Visibility.Visible : Visibility.Collapsed;
            _switchTabUrlRow.Visibility = byType == BridgeSwitchTabByType.Url ? Visibility.Visible : Visibility.Collapsed;
            _switchTabUrlReRow.Visibility = byType == BridgeSwitchTabByType.UrlRegex ? Visibility.Visible : Visibility.Collapsed;
            _switchTabTabRow.Visibility = byType == BridgeSwitchTabByType.Tab ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SyncSwitchTabByTypeCombo(BridgeSwitchTabByType byType)
        {
            _isSyncingSwitchByType = true;
            _switchTabByTypeComboBox.SelectedItem = ToSwitchTabByTypeText(byType);
            _isSyncingSwitchByType = false;
        }

        private void RefreshFindElementRows(BridgeFindElementBaseOn baseOn)
        {
            if (!IsFindElementLikeMode(_designerMode))
            {
                return;
            }

            _findElementTabRow.Visibility = baseOn == BridgeFindElementBaseOn.Tab &&
                BridgeDesignerSelectorHelper.ShouldShowOptionalInputTab(ModelItem, "Selector")
                ? Visibility.Visible
                : Visibility.Collapsed;
            _elementFindElementRow.Visibility = baseOn == BridgeFindElementBaseOn.Element ? Visibility.Visible : Visibility.Collapsed;
            _elementFindSelectorRow.Visibility = Visibility.Visible;
        }

        private void SyncBridgeFindElementBaseOnCombo(BridgeFindElementBaseOn baseOn)
        {
            _isSyncingBridgeFindElementBaseOn = true;
            _findElementBaseOnComboBox.SelectedItem = ToBridgeFindElementBaseOnText(baseOn);
            _isSyncingBridgeFindElementBaseOn = false;
        }

        private void RefreshGetCookiesRows(BridgeGetCookiesBaseOn baseOn)
        {
            if (_designerMode != DesignerMode.GetCookies && _designerMode != DesignerMode.GetStorage)
            {
                return;
            }

            _getCookiesInputBrowserRow.Visibility = baseOn == BridgeGetCookiesBaseOn.Browser ? Visibility.Visible : Visibility.Collapsed;
            _getCookiesInputTabRow.Visibility = baseOn == BridgeGetCookiesBaseOn.Tab ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SyncBridgeGetCookiesBaseOnCombo(BridgeGetCookiesBaseOn baseOn)
        {
            _isSyncingBridgeGetCookiesBaseOn = true;
            _getCookiesBaseOnComboBox.SelectedItem = ToBridgeGetCookiesBaseOnText(baseOn);
            _isSyncingBridgeGetCookiesBaseOn = false;
        }

        private void RefreshRunJsRows(BridgeRunJsBaseOn baseOn, BridgeElementTargetType targetType)
        {
            if (_designerMode != DesignerMode.RunJs)
            {
                return;
            }

            if (baseOn == BridgeRunJsBaseOn.Tab)
            {
                _runJsTargetTypeRow.Visibility = Visibility.Collapsed;
                _runJsInputTabRow.Visibility = Visibility.Visible;
                _runJsInputElementRow.Visibility = Visibility.Collapsed;
                _runJsSelectorRow.Visibility = Visibility.Collapsed;
                return;
            }

            _runJsTargetTypeRow.Visibility = Visibility.Visible;
            _runJsInputElementRow.Visibility = targetType == BridgeElementTargetType.Element ? Visibility.Visible : Visibility.Collapsed;
            var showRunJsInputTab = targetType == BridgeElementTargetType.Selector &&
                BridgeDesignerSelectorHelper.ShouldShowOptionalInputTab(ModelItem, "Selector");
            _runJsInputTabRow.Visibility = showRunJsInputTab ? Visibility.Visible : Visibility.Collapsed;
            _runJsSelectorRow.Visibility = targetType == BridgeElementTargetType.Selector ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SyncBridgeRunJsBaseOnCombo(BridgeRunJsBaseOn baseOn)
        {
            _isSyncingBridgeRunJsBaseOn = true;
            _runJsBaseOnComboBox.SelectedItem = ToBridgeRunJsBaseOnText(baseOn);
            _isSyncingBridgeRunJsBaseOn = false;
        }

        private void SyncRunJsTargetTypeCombo(BridgeElementTargetType targetType)
        {
            _isSyncingRunJsTargetType = true;
            _runJsTargetTypeComboBox.SelectedItem = ToRunJsTargetTypeText(targetType);
            _isSyncingRunJsTargetType = false;
        }

        private void RefreshSendKeysRows(BridgeSendKeysBaseOn baseOn, BridgeElementTargetType targetType)
        {
            if (_designerMode != DesignerMode.SendKeys)
            {
                return;
            }

            if (baseOn == BridgeSendKeysBaseOn.Tab)
            {
                _sendKeysTargetTypeRow.Visibility = Visibility.Collapsed;
                _sendKeysInputTabRow.Visibility = Visibility.Visible;
                _sendKeysInputElementRow.Visibility = Visibility.Collapsed;
                _sendKeysSelectorRow.Visibility = Visibility.Collapsed;
                return;
            }

            _sendKeysTargetTypeRow.Visibility = Visibility.Visible;
            _sendKeysInputElementRow.Visibility = targetType == BridgeElementTargetType.Element ? Visibility.Visible : Visibility.Collapsed;
            var showSendKeysInputTab = targetType == BridgeElementTargetType.Selector &&
                BridgeDesignerSelectorHelper.ShouldShowOptionalInputTab(ModelItem, "Selector");
            _sendKeysInputTabRow.Visibility = showSendKeysInputTab ? Visibility.Visible : Visibility.Collapsed;
            _sendKeysSelectorRow.Visibility = targetType == BridgeElementTargetType.Selector ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RefreshTakeScreenshotRows(BridgeTakeScreenshotBaseOn baseOn, BridgeElementTargetType targetType)
        {
            if (_designerMode != DesignerMode.TakeScreenshot)
            {
                return;
            }

            if (baseOn == BridgeTakeScreenshotBaseOn.Tab)
            {
                _runJsTargetTypeRow.Visibility = Visibility.Collapsed;
                _runJsInputTabRow.Visibility = Visibility.Visible;
                _runJsInputElementRow.Visibility = Visibility.Collapsed;
                _runJsSelectorRow.Visibility = Visibility.Collapsed;
                return;
            }

            _runJsTargetTypeRow.Visibility = Visibility.Visible;
            _runJsInputElementRow.Visibility = targetType == BridgeElementTargetType.Element ? Visibility.Visible : Visibility.Collapsed;
            var showScreenshotInputTab = targetType == BridgeElementTargetType.Selector &&
                BridgeDesignerSelectorHelper.ShouldShowOptionalInputTab(ModelItem, "Selector");
            _runJsInputTabRow.Visibility = showScreenshotInputTab ? Visibility.Visible : Visibility.Collapsed;
            _runJsSelectorRow.Visibility = targetType == BridgeElementTargetType.Selector ? Visibility.Visible : Visibility.Collapsed;
        }

        private static ComboBox BuildBridgeTakeScreenshotBaseOnComboBox()
        {
            var comboBox = new ComboBox
            {
                IsEditable = false
            };
            comboBox.Items.Add("Tab");
            comboBox.Items.Add("Element");
            return comboBox;
        }

        private static BridgeTakeScreenshotBaseOn ReadBridgeTakeScreenshotBaseOn(ModelItem modelItem)
        {
            var modelProperty = modelItem?.Properties["BaseOn"];
            if (modelProperty?.ComputedValue is BridgeTakeScreenshotBaseOn value)
            {
                return value;
            }

            return BridgeTakeScreenshotBaseOn.Element;
        }

        private static BridgeTakeScreenshotBaseOn ParseBridgeTakeScreenshotBaseOn(string text)
        {
            return string.Equals(text, "Tab", StringComparison.OrdinalIgnoreCase)
                ? BridgeTakeScreenshotBaseOn.Tab
                : BridgeTakeScreenshotBaseOn.Element;
        }

        private void SyncBridgeTakeScreenshotBaseOnCombo(BridgeTakeScreenshotBaseOn baseOn)
        {
            _isSyncingBridgeTakeScreenshotBaseOn = true;
            _takeScreenshotBaseOnComboBox.SelectedItem = baseOn == BridgeTakeScreenshotBaseOn.Tab ? "Tab" : "Element";
            _isSyncingBridgeTakeScreenshotBaseOn = false;
        }

        private void ClearInactiveTakeScreenshotArguments(BridgeTakeScreenshotBaseOn baseOn, BridgeElementTargetType targetType)
        {
            if (ModelItem == null || _designerMode != DesignerMode.TakeScreenshot)
            {
                return;
            }

            _isUpdatingTakeScreenshotArguments = true;
            try
            {
                var keepInputTab = baseOn == BridgeTakeScreenshotBaseOn.Tab ||
                                   (baseOn == BridgeTakeScreenshotBaseOn.Element && targetType == BridgeElementTargetType.Selector);
                var keepInputElement = baseOn == BridgeTakeScreenshotBaseOn.Element && targetType == BridgeElementTargetType.Element;
                var keepSelector = baseOn == BridgeTakeScreenshotBaseOn.Element && targetType == BridgeElementTargetType.Selector;

                ClearRunJsArgumentIfHidden("InputTab", keepInputTab);
                ClearRunJsArgumentIfHidden("InputElement", keepInputElement);
                ClearRunJsArgumentIfHidden("Selector", keepSelector);
            }
            finally
            {
                _isUpdatingTakeScreenshotArguments = false;
            }
        }

        private void SyncBridgeSendKeysBaseOnCombo(BridgeSendKeysBaseOn baseOn)
        {
            _isSyncingBridgeSendKeysBaseOn = true;
            _sendKeysBaseOnComboBox.SelectedItem = ToBridgeSendKeysBaseOnText(baseOn);
            _isSyncingBridgeSendKeysBaseOn = false;
        }

        private void SyncSendKeysTargetTypeCombo(BridgeElementTargetType targetType)
        {
            _isSyncingSendKeysTargetType = true;
            _sendKeysTargetTypeComboBox.SelectedItem = ToSendKeysTargetTypeText(targetType);
            _isSyncingSendKeysTargetType = false;
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
                SharedSizeGroup = "CanvasFieldLabelColumn"
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
                comboBox.IsEditable = false;
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

            // Align with LogMessageDesigner: two-way binding syncs canvas input to ModelItem properties.
            BindingOperations.SetBinding(editor, ExpressionTextBox.OwnerActivityProperty, new Binding("ModelItem"));
            BindingOperations.SetBinding(editor, ExpressionTextBox.ExpressionProperty, new Binding("ModelItem." + pathToArgument)
            {
                Mode = BindingMode.TwoWay,
                Converter = new ArgumentToExpressionConverter(),
                ConverterParameter = "In"
            });

            return editor;
        }

        private static ComboBox BuildSwitchTabByTypeComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.Items.Add("Index");
            comboBox.Items.Add("Title");
            comboBox.Items.Add("Title Regex");
            comboBox.Items.Add("Url");
            comboBox.Items.Add("Url Regex");
            comboBox.Items.Add("Tab");
            return comboBox;
        }

        private static ComboBox BuildBridgeFindElementBaseOnComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.Items.Add("Tab");
            comboBox.Items.Add("Element");
            return comboBox;
        }

        private static ComboBox BuildBridgeRunJsBaseOnComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.Items.Add("Element");
            comboBox.Items.Add("Tab");
            return comboBox;
        }

        private static ComboBox BuildRunJsTargetTypeComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.Items.Add("Selector");
            comboBox.Items.Add("Element");
            return comboBox;
        }

        private static ComboBox BuildBridgeSendKeysBaseOnComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.Items.Add("Tab");
            comboBox.Items.Add("Element");
            return comboBox;
        }

        private static ComboBox BuildSendKeysTargetTypeComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.Items.Add("Selector");
            comboBox.Items.Add("Element");
            return comboBox;
        }

        private static ComboBox BuildBridgeGetCookiesBaseOnComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.Items.Add("Tab");
            comboBox.Items.Add("Browser");
            return comboBox;
        }

        private static ComboBox BuildBridgeStorageScopeComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.Items.Add("Session");
            comboBox.Items.Add("Local");
            return comboBox;
        }

        private static BridgeSwitchTabByType ReadSwitchTabByType(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["ByType"];
            if (modelProperty?.ComputedValue is BridgeSwitchTabByType value)
            {
                return value;
            }

            return BridgeSwitchTabByType.Index;
        }

        private static BridgeGetCookiesBaseOn ReadBridgeGetCookiesBaseOn(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["BaseOn"];
            if (modelProperty?.ComputedValue is BridgeGetCookiesBaseOn value)
            {
                return value;
            }

            return BridgeGetCookiesBaseOn.Tab;
        }

        private static BridgeFindElementBaseOn ReadBridgeFindElementBaseOn(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["BaseOn"];
            if (modelProperty?.ComputedValue is BridgeFindElementBaseOn value)
            {
                return value;
            }

            return BridgeFindElementBaseOn.Tab;
        }

        private static BridgeStorageScope ReadBridgeStorageScope(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["Scope"];
            if (modelProperty?.ComputedValue is BridgeStorageScope value)
            {
                return value;
            }

            return BridgeStorageScope.Session;
        }

        private static BridgeRunJsBaseOn ReadBridgeRunJsBaseOn(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["BaseOn"];
            if (modelProperty?.ComputedValue is BridgeRunJsBaseOn value)
            {
                return value;
            }

            return BridgeRunJsBaseOn.Element;
        }

        private static BridgeElementTargetType ReadRunJsTargetType(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["TargetType"];
            if (modelProperty?.ComputedValue is BridgeElementTargetType value)
            {
                return value;
            }

            return BridgeElementTargetType.Selector;
        }

        private static BridgeSendKeysBaseOn ReadBridgeSendKeysBaseOn(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["BaseOn"];
            if (modelProperty?.ComputedValue is BridgeSendKeysBaseOn value)
            {
                return value;
            }

            return BridgeSendKeysBaseOn.Tab;
        }

        private static BridgeElementTargetType ReadSendKeysTargetType(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["TargetType"];
            if (modelProperty?.ComputedValue is BridgeElementTargetType value)
            {
                return value;
            }

            return BridgeElementTargetType.Selector;
        }

        private static BridgeSwitchTabByType ParseSwitchTabByType(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Title":
                    return BridgeSwitchTabByType.Title;
                case "Title Regex":
                    return BridgeSwitchTabByType.TitleRegex;
                case "Url":
                    return BridgeSwitchTabByType.Url;
                case "Url Regex":
                    return BridgeSwitchTabByType.UrlRegex;
                case "Tab":
                    return BridgeSwitchTabByType.Tab;
                default:
                    return BridgeSwitchTabByType.Index;
            }
        }

        private static BridgeGetCookiesBaseOn ParseBridgeGetCookiesBaseOn(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Browser":
                    return BridgeGetCookiesBaseOn.Browser;
                default:
                    return BridgeGetCookiesBaseOn.Tab;
            }
        }

        private static BridgeFindElementBaseOn ParseBridgeFindElementBaseOn(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Element":
                    return BridgeFindElementBaseOn.Element;
                default:
                    return BridgeFindElementBaseOn.Tab;
            }
        }

        private static BridgeStorageScope ParseBridgeStorageScope(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Local":
                    return BridgeStorageScope.Local;
                default:
                    return BridgeStorageScope.Session;
            }
        }

        private static BridgeRunJsBaseOn ParseBridgeRunJsBaseOn(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Tab":
                    return BridgeRunJsBaseOn.Tab;
                default:
                    return BridgeRunJsBaseOn.Element;
            }
        }

        private static BridgeElementTargetType ParseRunJsTargetType(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Element":
                    return BridgeElementTargetType.Element;
                default:
                    return BridgeElementTargetType.Selector;
            }
        }

        private static BridgeSendKeysBaseOn ParseBridgeSendKeysBaseOn(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Element":
                    return BridgeSendKeysBaseOn.Element;
                default:
                    return BridgeSendKeysBaseOn.Tab;
            }
        }

        private static BridgeElementTargetType ParseSendKeysTargetType(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Element":
                    return BridgeElementTargetType.Element;
                default:
                    return BridgeElementTargetType.Selector;
            }
        }

        private static string ToSwitchTabByTypeText(BridgeSwitchTabByType byType)
        {
            switch (byType)
            {
                case BridgeSwitchTabByType.Title:
                    return "Title";
                case BridgeSwitchTabByType.TitleRegex:
                    return "Title Regex";
                case BridgeSwitchTabByType.Url:
                    return "Url";
                case BridgeSwitchTabByType.UrlRegex:
                    return "Url Regex";
                case BridgeSwitchTabByType.Tab:
                    return "Tab";
                default:
                    return "Index";
            }
        }

        private static string ToBridgeGetCookiesBaseOnText(BridgeGetCookiesBaseOn baseOn)
        {
            return baseOn == BridgeGetCookiesBaseOn.Browser ? "Browser" : "Tab";
        }

        private static string ToBridgeFindElementBaseOnText(BridgeFindElementBaseOn baseOn)
        {
            return baseOn == BridgeFindElementBaseOn.Element ? "Element" : "Tab";
        }

        private static string ToBridgeStorageScopeText(BridgeStorageScope scope)
        {
            return scope == BridgeStorageScope.Local ? "Local" : "Session";
        }

        private static string ToBridgeRunJsBaseOnText(BridgeRunJsBaseOn baseOn)
        {
            return baseOn == BridgeRunJsBaseOn.Tab ? "Tab" : "Element";
        }

        private static string ToRunJsTargetTypeText(BridgeElementTargetType targetType)
        {
            return targetType == BridgeElementTargetType.Element ? "Element" : "Selector";
        }

        private static string ToBridgeSendKeysBaseOnText(BridgeSendKeysBaseOn baseOn)
        {
            return baseOn == BridgeSendKeysBaseOn.Element ? "Element" : "Tab";
        }

        private static string ToSendKeysTargetTypeText(BridgeElementTargetType targetType)
        {
            return targetType == BridgeElementTargetType.Element ? "Element" : "Selector";
        }

        private void SyncBridgeStorageScopeCombo(BridgeStorageScope scope)
        {
            _isSyncingBridgeStorageScope = true;
            _getStorageScopeComboBox.SelectedItem = ToBridgeStorageScopeText(scope);
            _isSyncingBridgeStorageScope = false;
        }

        private static bool IsFindElementLikeMode(DesignerMode mode)
        {
            return mode == DesignerMode.FindElement || mode == DesignerMode.FindElements;
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
                case nameof(FindElementActivity):
                    return DesignerMode.FindElement;
                case nameof(FindElementsActivity):
                    return DesignerMode.FindElements;
                case nameof(BrowserSwitchTabActivity):
                    return DesignerMode.BrowserSwitchTab;
                case nameof(BrowserCloseActivity):
                    return DesignerMode.BrowserClose;
                case nameof(BrowserGetAllTabActivity):
                    return DesignerMode.BrowserGetAllTab;
                case nameof(BrowserGetActivatedTabActivity):
                    return DesignerMode.BrowserGetActivatedTab;
                case nameof(BrowserGetStatusActivity):
                    return DesignerMode.BrowserGetStatus;
                case nameof(GetCookiesActivity):
                    return DesignerMode.GetCookies;
                case nameof(GetStorageActivity):
                    return DesignerMode.GetStorage;
                case nameof(BrowserGetLatestTabActivity):
                    return DesignerMode.BrowserGetLatestTab;
                case nameof(NewTabActivity):
                    return DesignerMode.BrowserNewTab;
                case nameof(BackActivity):
                    return DesignerMode.TabBack;
                case nameof(TabCloseActivity):
                    return DesignerMode.TabClose;
                case nameof(ElementExistsActivity):
                    return DesignerMode.TabElementExists;
                case nameof(ForwardActivity):
                    return DesignerMode.TabForward;
                case nameof(TabGetInfoActivity):
                    return DesignerMode.TabGetInfo;
                case nameof(NavigateUrlActivity):
                    return DesignerMode.TabNavigateUrl;
                case nameof(RefreshActivity):
                    return DesignerMode.TabRefresh;
                case nameof(RunJsActivity):
                    return DesignerMode.RunJs;
                case nameof(SendkeysActivity):
                    return DesignerMode.SendKeys;
                case nameof(TakeScreenshotActivity):
                    return DesignerMode.TakeScreenshot;
                default:
                    return DesignerMode.None;
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

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    if (ModelItem != null && _designerMode == DesignerMode.BrowserSwitchTab)
                    {
                        var byType = ReadSwitchTabByType(ModelItem);
                        SyncSwitchTabByTypeCombo(byType);
                        RefreshSwitchTabRows(byType);
                        if (!_isUpdatingSwitchTabArguments)
                        {
                            ClearInactiveSwitchTabArguments(byType);
                        }
                    }
                    else if (ModelItem != null && IsFindElementLikeMode(_designerMode))
                    {
                        var baseOn = ReadBridgeFindElementBaseOn(ModelItem);
                        SyncBridgeFindElementBaseOnCombo(baseOn);
                        RefreshFindElementRows(baseOn);
                        if (!_isUpdatingFindElementArguments)
                        {
                            ClearInactiveFindElementArguments(baseOn);
                        }
                    }
                    else if (ModelItem != null && (_designerMode == DesignerMode.GetCookies || _designerMode == DesignerMode.GetStorage))
                    {
                        var baseOn = ReadBridgeGetCookiesBaseOn(ModelItem);
                        SyncBridgeGetCookiesBaseOnCombo(baseOn);
                        RefreshGetCookiesRows(baseOn);
                        if (!_isUpdatingGetCookiesArguments)
                        {
                            ClearInactiveGetCookiesArguments(baseOn);
                        }
                        if (_designerMode == DesignerMode.GetStorage)
                        {
                            var scope = ReadBridgeStorageScope(ModelItem);
                            SyncBridgeStorageScopeCombo(scope);
                        }
                    }
                    else if (ModelItem != null && _designerMode == DesignerMode.RunJs)
                    {
                        var runJsBaseOn = ReadBridgeRunJsBaseOn(ModelItem);
                        var runJsTargetType = ReadRunJsTargetType(ModelItem);
                        SyncBridgeRunJsBaseOnCombo(runJsBaseOn);
                        SyncRunJsTargetTypeCombo(runJsTargetType);
                        RefreshRunJsRows(runJsBaseOn, runJsTargetType);
                        if (!_isUpdatingRunJsArguments)
                        {
                            ClearInactiveRunJsArguments(runJsBaseOn, runJsTargetType);
                        }
                    }
                    else if (ModelItem != null && _designerMode == DesignerMode.SendKeys)
                    {
                        var sendKeysBaseOn = ReadBridgeSendKeysBaseOn(ModelItem);
                        var sendKeysTargetType = ReadSendKeysTargetType(ModelItem);
                        SyncBridgeSendKeysBaseOnCombo(sendKeysBaseOn);
                        SyncSendKeysTargetTypeCombo(sendKeysTargetType);
                        RefreshSendKeysRows(sendKeysBaseOn, sendKeysTargetType);
                        if (!_isUpdatingSendKeysArguments)
                        {
                            ClearInactiveSendKeysArguments(sendKeysBaseOn, sendKeysTargetType);
                        }
                    }
                    else if (ModelItem != null && _designerMode == DesignerMode.TakeScreenshot)
                    {
                        var baseOn = ReadBridgeTakeScreenshotBaseOn(ModelItem);
                        var targetType = ReadRunJsTargetType(ModelItem);
                        SyncBridgeTakeScreenshotBaseOnCombo(baseOn);
                        SyncRunJsTargetTypeCombo(targetType);
                        RefreshTakeScreenshotRows(baseOn, targetType);
                        if (!_isUpdatingTakeScreenshotArguments)
                        {
                            ClearInactiveTakeScreenshotArguments(baseOn, targetType);
                        }
                    }

                    if (string.Equals(e.PropertyName, "Selector", StringComparison.Ordinal) && ModelItem != null)
                    {
                        _tabRow.Visibility = ResolveGeneralTabRowVisibility();
                        if (IsFindElementLikeMode(_designerMode))
                        {
                            RefreshFindElementRows(ReadBridgeFindElementBaseOn(ModelItem));
                        }
                        else if (_designerMode == DesignerMode.RunJs)
                        {
                            RefreshRunJsRows(ReadBridgeRunJsBaseOn(ModelItem), ReadRunJsTargetType(ModelItem));
                        }
                        else if (_designerMode == DesignerMode.SendKeys)
                        {
                            RefreshSendKeysRows(ReadBridgeSendKeysBaseOn(ModelItem), ReadSendKeysTargetType(ModelItem));
                        }
                        else if (_designerMode == DesignerMode.TakeScreenshot)
                        {
                            RefreshTakeScreenshotRows(ReadBridgeTakeScreenshotBaseOn(ModelItem), ReadRunJsTargetType(ModelItem));
                        }
                    }

                    RefreshRequiredBorders();
                }),
                DispatcherPriority.Background);
        }

        private void ClearInactiveSwitchTabArguments(BridgeSwitchTabByType activeByType)
        {
            if (ModelItem == null || _designerMode != DesignerMode.BrowserSwitchTab)
            {
                return;
            }

            _isUpdatingSwitchTabArguments = true;
            try
            {
                ClearSwitchTabArgumentIfHidden("Index", activeByType == BridgeSwitchTabByType.Index);
                ClearSwitchTabArgumentIfHidden("Title", activeByType == BridgeSwitchTabByType.Title);
                ClearSwitchTabArgumentIfHidden("TitleRe", activeByType == BridgeSwitchTabByType.TitleRegex);
                ClearSwitchTabArgumentIfHidden("Url", activeByType == BridgeSwitchTabByType.Url);
                ClearSwitchTabArgumentIfHidden("UrlRe", activeByType == BridgeSwitchTabByType.UrlRegex);
                ClearSwitchTabArgumentIfHidden("InputTab", activeByType == BridgeSwitchTabByType.Tab);
            }
            finally
            {
                _isUpdatingSwitchTabArguments = false;
            }
        }

        private void ClearInactiveGetCookiesArguments(BridgeGetCookiesBaseOn baseOn)
        {
            if (ModelItem == null || (_designerMode != DesignerMode.GetCookies && _designerMode != DesignerMode.GetStorage))
            {
                return;
            }

            _isUpdatingGetCookiesArguments = true;
            try
            {
                ClearGetCookiesArgumentIfHidden("InputBrowser", baseOn == BridgeGetCookiesBaseOn.Browser);
                ClearGetCookiesArgumentIfHidden("InputTab", baseOn == BridgeGetCookiesBaseOn.Tab);
            }
            finally
            {
                _isUpdatingGetCookiesArguments = false;
            }
        }

        private void ClearInactiveFindElementArguments(BridgeFindElementBaseOn baseOn)
        {
            if (ModelItem == null || !IsFindElementLikeMode(_designerMode))
            {
                return;
            }

            _isUpdatingFindElementArguments = true;
            try
            {
                ClearRunJsArgumentIfHidden("Tab", baseOn == BridgeFindElementBaseOn.Tab);
                ClearRunJsArgumentIfHidden("Element", baseOn == BridgeFindElementBaseOn.Element);
            }
            finally
            {
                _isUpdatingFindElementArguments = false;
            }
        }

        private void ClearInactiveRunJsArguments(BridgeRunJsBaseOn baseOn, BridgeElementTargetType targetType)
        {
            if (ModelItem == null || _designerMode != DesignerMode.RunJs)
            {
                return;
            }

            _isUpdatingRunJsArguments = true;
            try
            {
                var keepInputTab = baseOn == BridgeRunJsBaseOn.Tab || (baseOn == BridgeRunJsBaseOn.Element && targetType == BridgeElementTargetType.Selector);
                var keepInputElement = baseOn == BridgeRunJsBaseOn.Element && targetType == BridgeElementTargetType.Element;
                var keepSelector = baseOn == BridgeRunJsBaseOn.Element && targetType == BridgeElementTargetType.Selector;

                ClearRunJsArgumentIfHidden("InputTab", keepInputTab);
                ClearRunJsArgumentIfHidden("InputElement", keepInputElement);
                ClearRunJsArgumentIfHidden("Selector", keepSelector);
            }
            finally
            {
                _isUpdatingRunJsArguments = false;
            }
        }

        private void ClearInactiveSendKeysArguments(BridgeSendKeysBaseOn baseOn, BridgeElementTargetType targetType)
        {
            if (ModelItem == null || _designerMode != DesignerMode.SendKeys)
            {
                return;
            }

            _isUpdatingSendKeysArguments = true;
            try
            {
                var keepInputTab = baseOn == BridgeSendKeysBaseOn.Tab || (baseOn == BridgeSendKeysBaseOn.Element && targetType == BridgeElementTargetType.Selector);
                var keepInputElement = baseOn == BridgeSendKeysBaseOn.Element && targetType == BridgeElementTargetType.Element;
                var keepSelector = baseOn == BridgeSendKeysBaseOn.Element && targetType == BridgeElementTargetType.Selector;

                ClearRunJsArgumentIfHidden("InputTab", keepInputTab);
                ClearRunJsArgumentIfHidden("InputElement", keepInputElement);
                ClearRunJsArgumentIfHidden("Selector", keepSelector);
            }
            finally
            {
                _isUpdatingSendKeysArguments = false;
            }
        }

        private void ClearSwitchTabArgumentIfHidden(string propertyName, bool shouldKeep)
        {
            if (shouldKeep || ModelItem == null)
            {
                return;
            }

            var property = ModelItem.Properties[propertyName];
            if (property == null || !property.IsSet)
            {
                return;
            }

            property.SetValue(null);
        }

        private void ClearGetCookiesArgumentIfHidden(string propertyName, bool shouldKeep)
        {
            if (shouldKeep || ModelItem == null)
            {
                return;
            }

            var property = ModelItem.Properties[propertyName];
            if (property == null || !property.IsSet)
            {
                return;
            }

            property.SetValue(null);
        }

        private void ClearRunJsArgumentIfHidden(string propertyName, bool shouldKeep)
        {
            if (shouldKeep || ModelItem == null)
            {
                return;
            }

            var property = ModelItem.Properties[propertyName];
            if (property == null || !property.IsSet)
            {
                return;
            }

            property.SetValue(null);
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
            {
                return;
            }

            var byType = ReadSwitchTabByType(ModelItem);
            var getCookiesBaseOn = ReadBridgeGetCookiesBaseOn(ModelItem);
            var findElementBaseOn = ReadBridgeFindElementBaseOn(ModelItem);
            var runJsBaseOn = ReadBridgeRunJsBaseOn(ModelItem);
            var runJsTargetType = ReadRunJsTargetType(ModelItem);
            var sendKeysBaseOn = ReadBridgeSendKeysBaseOn(ModelItem);
            var sendKeysTargetType = ReadSendKeysTargetType(ModelItem);
            var takeScreenshotBaseOn = ReadBridgeTakeScreenshotBaseOn(ModelItem);
            var takeScreenshotTargetType = ReadRunJsTargetType(ModelItem);

            var navigateUsesWndSelector = _designerMode == DesignerMode.TabNavigateUrl &&
                BridgeDesignerSelectorHelper.SelectorHasWnd(ModelItem, "Selector");

            SetRequiredBorder(_browserEditorBorder, IsBrowserMode(_designerMode), IsArgumentFilled(ModelItem, "Browser", _browserExpressionBox));
            SetRequiredBorder(_tabEditorBorder, IsTabMode(_designerMode) && ResolveGeneralTabRowVisibility() == Visibility.Visible, IsArgumentFilled(ModelItem, "Tab", _tabExpressionBox));
            SetRequiredBorder(_findElementTabEditorBorder, IsFindElementLikeMode(_designerMode) && findElementBaseOn == BridgeFindElementBaseOn.Tab && _findElementTabRow.Visibility == Visibility.Visible, IsArgumentFilled(ModelItem, "Tab", _findElementTabExpressionBox));
            SetRequiredBorder(_elementFindElementEditorBorder, IsFindElementLikeMode(_designerMode) && findElementBaseOn == BridgeFindElementBaseOn.Element, IsArgumentFilled(ModelItem, "Element", _elementFindElementExpressionBox));
            SetRequiredBorder(_elementFindSelectorEditorBorder, IsFindElementLikeMode(_designerMode), IsArgumentFilled(ModelItem, "Selector", _elementFindSelectorExpressionBox));
            SetRequiredBorder(_tabExistsSelectorEditorBorder, _designerMode == DesignerMode.TabElementExists, IsArgumentFilled(ModelItem, "Selector", _tabExistsSelectorExpressionBox));
            SetRequiredBorder(_tabNavigateSelectorEditorBorder, navigateUsesWndSelector, IsArgumentFilled(ModelItem, "Selector", _tabNavigateSelectorExpressionBox));
            SetRequiredBorder(_tabNavigateUrlEditorBorder, _designerMode == DesignerMode.TabNavigateUrl, IsArgumentFilled(ModelItem, "Url", _tabNavigateUrlExpressionBox));
            var isRunJsTab = _designerMode == DesignerMode.RunJs && runJsBaseOn == BridgeRunJsBaseOn.Tab;
            var isRunJsElementByElement = _designerMode == DesignerMode.RunJs && runJsBaseOn == BridgeRunJsBaseOn.Element && runJsTargetType == BridgeElementTargetType.Element;
            var isRunJsElementBySelector = _designerMode == DesignerMode.RunJs && runJsBaseOn == BridgeRunJsBaseOn.Element && runJsTargetType == BridgeElementTargetType.Selector &&
                BridgeDesignerSelectorHelper.ShouldShowOptionalInputTab(ModelItem, "Selector");
            var isSendKeysTab = _designerMode == DesignerMode.SendKeys && sendKeysBaseOn == BridgeSendKeysBaseOn.Tab;
            var isSendKeysElementByElement = _designerMode == DesignerMode.SendKeys && sendKeysBaseOn == BridgeSendKeysBaseOn.Element && sendKeysTargetType == BridgeElementTargetType.Element;
            var isSendKeysElementBySelector = _designerMode == DesignerMode.SendKeys && sendKeysBaseOn == BridgeSendKeysBaseOn.Element && sendKeysTargetType == BridgeElementTargetType.Selector &&
                BridgeDesignerSelectorHelper.ShouldShowOptionalInputTab(ModelItem, "Selector");
            var isTakeScreenshotTab = _designerMode == DesignerMode.TakeScreenshot && takeScreenshotBaseOn == BridgeTakeScreenshotBaseOn.Tab;
            var isTakeScreenshotElementByElement = _designerMode == DesignerMode.TakeScreenshot && takeScreenshotBaseOn == BridgeTakeScreenshotBaseOn.Element && takeScreenshotTargetType == BridgeElementTargetType.Element;
            var isTakeScreenshotElementBySelector = _designerMode == DesignerMode.TakeScreenshot && takeScreenshotBaseOn == BridgeTakeScreenshotBaseOn.Element && takeScreenshotTargetType == BridgeElementTargetType.Selector &&
                BridgeDesignerSelectorHelper.ShouldShowOptionalInputTab(ModelItem, "Selector");

            SetRequiredBorder(_runJsInputTabEditorBorder, isRunJsTab || isRunJsElementBySelector || isSendKeysTab || isSendKeysElementBySelector || isTakeScreenshotTab || isTakeScreenshotElementBySelector, IsArgumentFilled(ModelItem, "InputTab", _runJsInputTabExpressionBox));
            SetRequiredBorder(_runJsInputElementEditorBorder, isRunJsElementByElement || isSendKeysElementByElement || isTakeScreenshotElementByElement, IsArgumentFilled(ModelItem, "InputElement", _runJsInputElementExpressionBox));
            SetRequiredBorder(_runJsSelectorEditorBorder, isRunJsElementBySelector || isSendKeysElementBySelector || isTakeScreenshotElementBySelector, IsArgumentFilled(ModelItem, "Selector", _runJsSelectorExpressionBox));
            SetRequiredBorder(_runJsScriptEditorBorder, _designerMode == DesignerMode.RunJs, IsArgumentFilled(ModelItem, "Script", _runJsScriptExpressionBox));

            SetRequiredBorder(_sendKeysInputTabEditorBorder, isSendKeysTab || isSendKeysElementBySelector, IsArgumentFilled(ModelItem, "InputTab", _sendKeysInputTabExpressionBox));
            SetRequiredBorder(_sendKeysInputElementEditorBorder, isSendKeysElementByElement, IsArgumentFilled(ModelItem, "InputElement", _sendKeysInputElementExpressionBox));
            SetRequiredBorder(_sendKeysSelectorEditorBorder, isSendKeysElementBySelector, IsArgumentFilled(ModelItem, "Selector", _sendKeysSelectorExpressionBox));
            SetRequiredBorder(_sendKeysKeysEditorBorder, _designerMode == DesignerMode.SendKeys, IsArgumentFilled(ModelItem, "Keys", _sendKeysKeysExpressionBox));
            SetRequiredBorder(_takeScreenshotPathEditorBorder, _designerMode == DesignerMode.TakeScreenshot, IsArgumentFilled(ModelItem, "Path", _takeScreenshotPathExpressionBox));

            SetRequiredBorder(_switchTabIndexEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BridgeSwitchTabByType.Index, IsArgumentFilled(ModelItem, "Index", _switchTabIndexExpressionBox));
            SetRequiredBorder(_switchTabTitleEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BridgeSwitchTabByType.Title, IsArgumentFilled(ModelItem, "Title", _switchTabTitleExpressionBox));
            SetRequiredBorder(_switchTabTitleReEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BridgeSwitchTabByType.TitleRegex, IsArgumentFilled(ModelItem, "TitleRe", _switchTabTitleReExpressionBox));
            SetRequiredBorder(_switchTabUrlEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BridgeSwitchTabByType.Url, IsArgumentFilled(ModelItem, "Url", _switchTabUrlExpressionBox));
            SetRequiredBorder(_switchTabUrlReEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BridgeSwitchTabByType.UrlRegex, IsArgumentFilled(ModelItem, "UrlRe", _switchTabUrlReExpressionBox));
            SetRequiredBorder(_switchTabTabEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BridgeSwitchTabByType.Tab, IsArgumentFilled(ModelItem, "InputTab", _switchTabTabExpressionBox));
            var isGetCookiesLikeMode = _designerMode == DesignerMode.GetCookies || _designerMode == DesignerMode.GetStorage;
            SetRequiredBorder(_getCookiesInputBrowserEditorBorder, isGetCookiesLikeMode && getCookiesBaseOn == BridgeGetCookiesBaseOn.Browser, IsArgumentFilled(ModelItem, "InputBrowser", _getCookiesInputBrowserExpressionBox));
            SetRequiredBorder(_getCookiesInputTabEditorBorder, isGetCookiesLikeMode && getCookiesBaseOn == BridgeGetCookiesBaseOn.Tab, IsArgumentFilled(ModelItem, "InputTab", _getCookiesInputTabExpressionBox));
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

            if (property.ComputedValue != null)
            {
                return true;
            }

            if (property.Value == null)
            {
                return HasEditorInput(editor);
            }

            var propertyValueText = property.Value.ToString();
            if (!string.IsNullOrWhiteSpace(propertyValueText) && !string.Equals(propertyValueText, "null", StringComparison.OrdinalIgnoreCase))
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

        private void BindKnownExpressionOwners(ModelItem owner)
        {
            if (owner == null)
            {
                return;
            }

            _browserExpressionBox.OwnerActivity = owner;
            _tabExpressionBox.OwnerActivity = owner;
            _findElementTabExpressionBox.OwnerActivity = owner;
            _elementFindElementExpressionBox.OwnerActivity = owner;
            _elementFindSelectorExpressionBox.OwnerActivity = owner;
            _tabExistsSelectorExpressionBox.OwnerActivity = owner;
            _tabFindSelectorExpressionBox.OwnerActivity = owner;
            _tabNavigateSelectorExpressionBox.OwnerActivity = owner;
            _tabNavigateUrlExpressionBox.OwnerActivity = owner;
            _runJsInputTabExpressionBox.OwnerActivity = owner;
            _runJsInputElementExpressionBox.OwnerActivity = owner;
            _runJsSelectorExpressionBox.OwnerActivity = owner;
            _runJsScriptExpressionBox.OwnerActivity = owner;
            _sendKeysInputTabExpressionBox.OwnerActivity = owner;
            _sendKeysInputElementExpressionBox.OwnerActivity = owner;
            _sendKeysSelectorExpressionBox.OwnerActivity = owner;
            _sendKeysKeysExpressionBox.OwnerActivity = owner;
            _switchTabIndexExpressionBox.OwnerActivity = owner;
            _switchTabTitleExpressionBox.OwnerActivity = owner;
            _switchTabTitleReExpressionBox.OwnerActivity = owner;
            _switchTabUrlExpressionBox.OwnerActivity = owner;
            _switchTabUrlReExpressionBox.OwnerActivity = owner;
            _switchTabTabExpressionBox.OwnerActivity = owner;
            _getCookiesInputBrowserExpressionBox.OwnerActivity = owner;
            _getCookiesInputTabExpressionBox.OwnerActivity = owner;
        }

        private static bool HasEditorInput(ExpressionTextBox editor)
        {
            if (editor == null)
            {
                return false;
            }

            var expression = editor.Expression;
            return expression != null;
        }

        private static bool IsBrowserMode(DesignerMode mode)
        {
            switch (mode)
            {
                case DesignerMode.BrowserClose:
                case DesignerMode.BrowserGetAllTab:
                case DesignerMode.BrowserGetActivatedTab:
                case DesignerMode.BrowserGetStatus:
                case DesignerMode.BrowserGetLatestTab:
                case DesignerMode.BrowserNewTab:
                case DesignerMode.BrowserSwitchTab:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsTabMode(DesignerMode mode)
        {
            switch (mode)
            {
                case DesignerMode.TabBack:
                case DesignerMode.TabClose:
                case DesignerMode.TabElementExists:
                case DesignerMode.TabForward:
                case DesignerMode.TabGetInfo:
                case DesignerMode.TabNavigateUrl:
                case DesignerMode.TabRefresh:
                    return true;
                default:
                    return false;
            }
        }

        private Visibility ResolveGeneralTabRowVisibility()
        {
            if (!IsTabMode(_designerMode) || _designerMode == DesignerMode.GetCookies || IsFindElementLikeMode(_designerMode))
                return Visibility.Collapsed;

            if (_designerMode == DesignerMode.TabElementExists || _designerMode == DesignerMode.TabNavigateUrl)
            {
                return BridgeDesignerSelectorHelper.SelectorHasWnd(ModelItem, "Selector")
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            return Visibility.Visible;
        }

        private enum DesignerMode
        {
            None,
            FindElement,
            FindElements,
            BrowserSwitchTab,
            BrowserClose,
            BrowserGetAllTab,
            BrowserGetActivatedTab,
            BrowserGetStatus,
            GetCookies,
            GetStorage,
            BrowserGetLatestTab,
            BrowserNewTab,
            TabBack,
            TabClose,
            TabElementExists,
            TabForward,
            TabGetInfo,
            TabNavigateUrl,
            TabRefresh,
            RunJs,
            SendKeys,
            TakeScreenshot
        }
    }
}
