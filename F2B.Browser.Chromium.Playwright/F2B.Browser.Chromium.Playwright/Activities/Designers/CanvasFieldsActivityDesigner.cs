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

namespace F2B.Browser.Chromium.Playwright
{
    public sealed class CanvasFieldsActivityDesigner : ActivityDesigner
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

        private DesignerMode _designerMode = DesignerMode.None;
        private bool _isSyncingSwitchByType;
        private bool _isUpdatingSwitchTabArguments;
        private bool _isSyncingGetCookiesBaseOn;
        private bool _isSyncingGetStorageScope;
        private bool _isUpdatingGetCookiesArguments;
        private bool _isSyncingFindElementBaseOn;
        private bool _isUpdatingFindElementArguments;
        private bool _isSyncingRunJsBaseOn;
        private bool _isSyncingRunJsTargetType;
        private bool _isUpdatingRunJsArguments;
        private bool _isSyncingSendKeysBaseOn;
        private bool _isSyncingSendKeysTargetType;
        private bool _isUpdatingSendKeysArguments;

        public CanvasFieldsActivityDesigner()
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

            _browserExpressionBox = CreateExpressionTextBox("Browser", typeof(PwBrowser));
            _tabExpressionBox = CreateExpressionTextBox("Tab", typeof(PwTab));
            _findElementTabExpressionBox = CreateExpressionTextBox("Tab", typeof(PwTab));
            _elementFindElementExpressionBox = CreateExpressionTextBox("Element", typeof(PwElement));
            _elementFindSelectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _tabExistsSelectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _tabFindSelectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _tabNavigateUrlExpressionBox = CreateExpressionTextBox("Url", typeof(string));
            _runJsInputTabExpressionBox = CreateExpressionTextBox("InputTab", typeof(PwTab));
            _runJsInputElementExpressionBox = CreateExpressionTextBox("InputElement", typeof(PwElement));
            _runJsSelectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _runJsScriptExpressionBox = CreateExpressionTextBox("Script", typeof(string));
            _sendKeysInputTabExpressionBox = CreateExpressionTextBox("InputTab", typeof(PwTab));
            _sendKeysInputElementExpressionBox = CreateExpressionTextBox("InputElement", typeof(PwElement));
            _sendKeysSelectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _sendKeysKeysExpressionBox = CreateExpressionTextBox("Keys", typeof(string));
            _switchTabIndexExpressionBox = CreateExpressionTextBox("Index", typeof(int?));
            _switchTabTitleExpressionBox = CreateExpressionTextBox("Title", typeof(string));
            _switchTabTitleReExpressionBox = CreateExpressionTextBox("TitleRe", typeof(string));
            _switchTabUrlExpressionBox = CreateExpressionTextBox("Url", typeof(string));
            _switchTabUrlReExpressionBox = CreateExpressionTextBox("UrlRe", typeof(string));
            _switchTabTabExpressionBox = CreateExpressionTextBox("InputTab", typeof(PwTab));
            _getCookiesInputBrowserExpressionBox = CreateExpressionTextBox("InputBrowser", typeof(PwBrowser));
            _getCookiesInputTabExpressionBox = CreateExpressionTextBox("InputTab", typeof(PwTab));

            _findElementBaseOnComboBox = BuildFindElementBaseOnComboBox();
            _findElementBaseOnComboBox.SelectionChanged += OnFindElementBaseOnSelectionChanged;
            _findElementBaseOnRow = CreateRow("BaseOn", _findElementBaseOnComboBox);
            _findElementTabRow = CreateRow("Tab", _findElementTabExpressionBox, out _findElementTabEditorBorder, RowSpacing);
            _browserRow = CreateRow("Browser", _browserExpressionBox, out _browserEditorBorder);
            _tabRow = CreateRow("Tab", _tabExpressionBox, out _tabEditorBorder);
            _elementFindElementRow = CreateRow("Element", _elementFindElementExpressionBox, out _elementFindElementEditorBorder);
            _elementFindSelectorRow = CreateRow("Selector", _elementFindSelectorExpressionBox, out _elementFindSelectorEditorBorder, RowSpacing);
            _tabExistsSelectorRow = CreateRow("Selector", _tabExistsSelectorExpressionBox, out _tabExistsSelectorEditorBorder);
            _tabFindSelectorRow = CreateRow("Selector", _tabFindSelectorExpressionBox, out _tabFindSelectorEditorBorder);
            _tabNavigateUrlRow = CreateRow("Url", _tabNavigateUrlExpressionBox, out _tabNavigateUrlEditorBorder);
            _runJsBaseOnComboBox = BuildRunJsBaseOnComboBox();
            _runJsBaseOnComboBox.SelectionChanged += OnRunJsBaseOnSelectionChanged;
            _runJsBaseOnRow = CreateRow("BaseOn", _runJsBaseOnComboBox);
            _runJsTargetTypeComboBox = BuildRunJsTargetTypeComboBox();
            _runJsTargetTypeComboBox.SelectionChanged += OnRunJsTargetTypeSelectionChanged;
            _runJsTargetTypeRow = CreateRow("TargetType", _runJsTargetTypeComboBox, RowSpacing);
            _runJsInputTabRow = CreateRow("InputTab", _runJsInputTabExpressionBox, out _runJsInputTabEditorBorder, RowSpacing);
            _runJsInputElementRow = CreateRow("InputElement", _runJsInputElementExpressionBox, out _runJsInputElementEditorBorder, RowSpacing);
            _runJsSelectorRow = CreateRow("Selector", _runJsSelectorExpressionBox, out _runJsSelectorEditorBorder, RowSpacing);
            _runJsScriptRow = CreateRow("Script", _runJsScriptExpressionBox, out _runJsScriptEditorBorder, RowSpacing);
            _sendKeysBaseOnComboBox = BuildSendKeysBaseOnComboBox();
            _sendKeysBaseOnComboBox.SelectionChanged += OnSendKeysBaseOnSelectionChanged;
            _sendKeysBaseOnRow = CreateRow("BaseOn", _sendKeysBaseOnComboBox);
            _sendKeysTargetTypeComboBox = BuildSendKeysTargetTypeComboBox();
            _sendKeysTargetTypeComboBox.SelectionChanged += OnSendKeysTargetTypeSelectionChanged;
            _sendKeysTargetTypeRow = CreateRow("TargetType", _sendKeysTargetTypeComboBox, RowSpacing);
            _sendKeysInputTabRow = CreateRow("InputTab", _sendKeysInputTabExpressionBox, out _sendKeysInputTabEditorBorder, RowSpacing);
            _sendKeysInputElementRow = CreateRow("InputElement", _sendKeysInputElementExpressionBox, out _sendKeysInputElementEditorBorder, RowSpacing);
            _sendKeysSelectorRow = CreateRow("Selector", _sendKeysSelectorExpressionBox, out _sendKeysSelectorEditorBorder, RowSpacing);
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
            _getCookiesBaseOnComboBox = BuildGetCookiesBaseOnComboBox();
            _getCookiesBaseOnComboBox.SelectionChanged += OnGetCookiesBaseOnSelectionChanged;
            _getCookiesBaseOnRow = CreateRow("BaseOn", _getCookiesBaseOnComboBox);
            _getCookiesInputBrowserRow = CreateRow("InputBrowser", _getCookiesInputBrowserExpressionBox, out _getCookiesInputBrowserEditorBorder, RowSpacing);
            _getCookiesInputTabRow = CreateRow("InputTab", _getCookiesInputTabExpressionBox, out _getCookiesInputTabEditorBorder, RowSpacing);
            _getStorageScopeComboBox = BuildGetStorageScopeComboBox();
            _getStorageScopeComboBox.SelectionChanged += OnGetStorageScopeSelectionChanged;
            _getStorageScopeRow = CreateRow("Scope", _getStorageScopeComboBox, RowSpacing);

            body.Children.Add(_browserRow);
            body.Children.Add(_tabRow);
            body.Children.Add(_findElementBaseOnRow);
            body.Children.Add(_findElementTabRow);
            body.Children.Add(_elementFindElementRow);
            body.Children.Add(_elementFindSelectorRow);
            body.Children.Add(_tabExistsSelectorRow);
            body.Children.Add(_tabFindSelectorRow);
            body.Children.Add(_tabNavigateUrlRow);
            body.Children.Add(_runJsBaseOnRow);
            body.Children.Add(_runJsTargetTypeRow);
            body.Children.Add(_runJsInputTabRow);
            body.Children.Add(_runJsInputElementRow);
            body.Children.Add(_runJsSelectorRow);
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
            else if (_designerMode == DesignerMode.FindElement)
            {
                var baseOn = ReadFindElementBaseOn(ModelItem);
                SyncFindElementBaseOnCombo(baseOn);
                ClearInactiveFindElementArguments(baseOn);
                RefreshFindElementRows(baseOn);
            }
            else if (_designerMode == DesignerMode.GetCookies || _designerMode == DesignerMode.GetStorage)
            {
                var baseOn = ReadGetCookiesBaseOn(ModelItem);
                SyncGetCookiesBaseOnCombo(baseOn);
                ClearInactiveGetCookiesArguments(baseOn);
                RefreshGetCookiesRows(baseOn);
                if (_designerMode == DesignerMode.GetStorage)
                {
                    var scope = ReadGetStorageScope(ModelItem);
                    SyncGetStorageScopeCombo(scope);
                }
            }
            else if (_designerMode == DesignerMode.RunJs)
            {
                var runJsBaseOn = ReadRunJsBaseOn(ModelItem);
                var runJsTargetType = ReadRunJsTargetType(ModelItem);
                SyncRunJsBaseOnCombo(runJsBaseOn);
                SyncRunJsTargetTypeCombo(runJsTargetType);
                ClearInactiveRunJsArguments(runJsBaseOn, runJsTargetType);
                RefreshRunJsRows(runJsBaseOn, runJsTargetType);
            }
            else if (_designerMode == DesignerMode.SendKeys)
            {
                var sendKeysBaseOn = ReadSendKeysBaseOn(ModelItem);
                var sendKeysTargetType = ReadSendKeysTargetType(ModelItem);
                SyncSendKeysBaseOnCombo(sendKeysBaseOn);
                SyncSendKeysTargetTypeCombo(sendKeysTargetType);
                ClearInactiveSendKeysArguments(sendKeysBaseOn, sendKeysTargetType);
                RefreshSendKeysRows(sendKeysBaseOn, sendKeysTargetType);
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

        private void OnFindElementBaseOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingFindElementBaseOn || ModelItem == null || _designerMode != DesignerMode.FindElement)
            {
                return;
            }

            var baseOn = ParseFindElementBaseOn(_findElementBaseOnComboBox.SelectedItem as string);
            ModelItem.Properties["BaseOn"].SetValue(baseOn);
            ClearInactiveFindElementArguments(baseOn);
            RefreshFindElementRows(baseOn);
            RefreshRequiredBorders();
        }

        private void OnGetCookiesBaseOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingGetCookiesBaseOn || ModelItem == null || (_designerMode != DesignerMode.GetCookies && _designerMode != DesignerMode.GetStorage))
            {
                return;
            }

            var baseOn = ParseGetCookiesBaseOn(_getCookiesBaseOnComboBox.SelectedItem as string);
            ModelItem.Properties["BaseOn"].SetValue(baseOn);
            ClearInactiveGetCookiesArguments(baseOn);
            RefreshGetCookiesRows(baseOn);
            RefreshRequiredBorders();
        }

        private void OnGetStorageScopeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_designerMode != DesignerMode.GetStorage || _isSyncingGetStorageScope || ModelItem == null)
            {
                return;
            }

            var scope = ParseGetStorageScope(_getStorageScopeComboBox.SelectedItem as string);
            ModelItem.Properties["Scope"].SetValue(scope);
            RefreshRequiredBorders();
        }

        private void OnRunJsBaseOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingRunJsBaseOn || ModelItem == null || _designerMode != DesignerMode.RunJs)
            {
                return;
            }

            var runJsBaseOn = ParseRunJsBaseOn(_runJsBaseOnComboBox.SelectedItem as string);
            var runJsTargetType = ReadRunJsTargetType(ModelItem);
            ModelItem.Properties["BaseOn"].SetValue(runJsBaseOn);
            ClearInactiveRunJsArguments(runJsBaseOn, runJsTargetType);
            RefreshRunJsRows(runJsBaseOn, runJsTargetType);
            RefreshRequiredBorders();
        }

        private void OnRunJsTargetTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingRunJsTargetType || ModelItem == null || _designerMode != DesignerMode.RunJs)
            {
                return;
            }

            var runJsBaseOn = ReadRunJsBaseOn(ModelItem);
            var runJsTargetType = ParseRunJsTargetType(_runJsTargetTypeComboBox.SelectedItem as string);
            ModelItem.Properties["TargetType"].SetValue(runJsTargetType);
            ClearInactiveRunJsArguments(runJsBaseOn, runJsTargetType);
            RefreshRunJsRows(runJsBaseOn, runJsTargetType);
            RefreshRequiredBorders();
        }

        private void OnSendKeysBaseOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingSendKeysBaseOn || ModelItem == null || _designerMode != DesignerMode.SendKeys)
            {
                return;
            }

            var sendKeysBaseOn = ParseSendKeysBaseOn(_sendKeysBaseOnComboBox.SelectedItem as string);
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

            var sendKeysBaseOn = ReadSendKeysBaseOn(ModelItem);
            var sendKeysTargetType = ParseSendKeysTargetType(_sendKeysTargetTypeComboBox.SelectedItem as string);
            ModelItem.Properties["TargetType"].SetValue(sendKeysTargetType);
            ClearInactiveSendKeysArguments(sendKeysBaseOn, sendKeysTargetType);
            RefreshSendKeysRows(sendKeysBaseOn, sendKeysTargetType);
            RefreshRequiredBorders();
        }

        private void RefreshModeRows()
        {
            _browserRow.Visibility = IsBrowserMode(_designerMode) && _designerMode != DesignerMode.GetCookies ? Visibility.Visible : Visibility.Collapsed;
            _tabRow.Visibility = IsTabMode(_designerMode) && _designerMode != DesignerMode.GetCookies && _designerMode != DesignerMode.FindElement ? Visibility.Visible : Visibility.Collapsed;
            _findElementBaseOnRow.Visibility = _designerMode == DesignerMode.FindElement ? Visibility.Visible : Visibility.Collapsed;
            _findElementTabRow.Visibility = _designerMode == DesignerMode.FindElement ? Visibility.Visible : Visibility.Collapsed;
            _elementFindElementRow.Visibility = _designerMode == DesignerMode.FindElement ? Visibility.Visible : Visibility.Collapsed;
            _elementFindSelectorRow.Visibility = _designerMode == DesignerMode.FindElement ? Visibility.Visible : Visibility.Collapsed;
            _tabExistsSelectorRow.Visibility = _designerMode == DesignerMode.TabElementExists ? Visibility.Visible : Visibility.Collapsed;
            _tabFindSelectorRow.Visibility = Visibility.Collapsed;
            _tabNavigateUrlRow.Visibility = _designerMode == DesignerMode.TabNavigateUrl ? Visibility.Visible : Visibility.Collapsed;
            _runJsBaseOnRow.Visibility = _designerMode == DesignerMode.RunJs ? Visibility.Visible : Visibility.Collapsed;
            _runJsTargetTypeRow.Visibility = _designerMode == DesignerMode.RunJs ? Visibility.Visible : Visibility.Collapsed;
            _runJsScriptRow.Visibility = _designerMode == DesignerMode.RunJs ? Visibility.Visible : Visibility.Collapsed;
            _sendKeysBaseOnRow.Visibility = _designerMode == DesignerMode.SendKeys ? Visibility.Visible : Visibility.Collapsed;
            _sendKeysTargetTypeRow.Visibility = _designerMode == DesignerMode.SendKeys ? Visibility.Visible : Visibility.Collapsed;
            _sendKeysKeysRow.Visibility = _designerMode == DesignerMode.SendKeys ? Visibility.Visible : Visibility.Collapsed;
            _switchTabByTypeRow.Visibility = _designerMode == DesignerMode.BrowserSwitchTab ? Visibility.Visible : Visibility.Collapsed;
            _getCookiesBaseOnRow.Visibility = (_designerMode == DesignerMode.GetCookies || _designerMode == DesignerMode.GetStorage) ? Visibility.Visible : Visibility.Collapsed;
            _getStorageScopeRow.Visibility = _designerMode == DesignerMode.GetStorage ? Visibility.Visible : Visibility.Collapsed;

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
            if (_designerMode != DesignerMode.RunJs)
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
            if (_designerMode != DesignerMode.FindElement)
            {
                _findElementTabRow.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshSwitchTabRows(BrowserSwitchTabByType byType)
        {
            if (_designerMode != DesignerMode.BrowserSwitchTab)
            {
                return;
            }

            _switchTabIndexRow.Visibility = byType == BrowserSwitchTabByType.Index ? Visibility.Visible : Visibility.Collapsed;
            _switchTabTitleRow.Visibility = byType == BrowserSwitchTabByType.Title ? Visibility.Visible : Visibility.Collapsed;
            _switchTabTitleReRow.Visibility = byType == BrowserSwitchTabByType.TitleRegex ? Visibility.Visible : Visibility.Collapsed;
            _switchTabUrlRow.Visibility = byType == BrowserSwitchTabByType.Url ? Visibility.Visible : Visibility.Collapsed;
            _switchTabUrlReRow.Visibility = byType == BrowserSwitchTabByType.UrlRegex ? Visibility.Visible : Visibility.Collapsed;
            _switchTabTabRow.Visibility = byType == BrowserSwitchTabByType.Tab ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SyncSwitchTabByTypeCombo(BrowserSwitchTabByType byType)
        {
            _isSyncingSwitchByType = true;
            _switchTabByTypeComboBox.SelectedItem = ToSwitchTabByTypeText(byType);
            _isSyncingSwitchByType = false;
        }

        private void RefreshFindElementRows(FindElementBaseOn baseOn)
        {
            if (_designerMode != DesignerMode.FindElement)
            {
                return;
            }

            _findElementTabRow.Visibility = baseOn == FindElementBaseOn.Tab ? Visibility.Visible : Visibility.Collapsed;
            _elementFindElementRow.Visibility = baseOn == FindElementBaseOn.Element ? Visibility.Visible : Visibility.Collapsed;
            _elementFindSelectorRow.Visibility = Visibility.Visible;
        }

        private void SyncFindElementBaseOnCombo(FindElementBaseOn baseOn)
        {
            _isSyncingFindElementBaseOn = true;
            _findElementBaseOnComboBox.SelectedItem = ToFindElementBaseOnText(baseOn);
            _isSyncingFindElementBaseOn = false;
        }

        private void RefreshGetCookiesRows(GetCookiesBaseOn baseOn)
        {
            if (_designerMode != DesignerMode.GetCookies && _designerMode != DesignerMode.GetStorage)
            {
                return;
            }

            _getCookiesInputBrowserRow.Visibility = baseOn == GetCookiesBaseOn.Browser ? Visibility.Visible : Visibility.Collapsed;
            _getCookiesInputTabRow.Visibility = baseOn == GetCookiesBaseOn.Tab ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SyncGetCookiesBaseOnCombo(GetCookiesBaseOn baseOn)
        {
            _isSyncingGetCookiesBaseOn = true;
            _getCookiesBaseOnComboBox.SelectedItem = ToGetCookiesBaseOnText(baseOn);
            _isSyncingGetCookiesBaseOn = false;
        }

        private void RefreshRunJsRows(RunJsBaseOn baseOn, ElementTargetType targetType)
        {
            if (_designerMode != DesignerMode.RunJs)
            {
                return;
            }

            if (baseOn == RunJsBaseOn.Tab)
            {
                _runJsTargetTypeRow.Visibility = Visibility.Collapsed;
                _runJsInputTabRow.Visibility = Visibility.Visible;
                _runJsInputElementRow.Visibility = Visibility.Collapsed;
                _runJsSelectorRow.Visibility = Visibility.Collapsed;
                return;
            }

            _runJsTargetTypeRow.Visibility = Visibility.Visible;
            _runJsInputElementRow.Visibility = targetType == ElementTargetType.Element ? Visibility.Visible : Visibility.Collapsed;
            _runJsInputTabRow.Visibility = targetType == ElementTargetType.Selector ? Visibility.Visible : Visibility.Collapsed;
            _runJsSelectorRow.Visibility = targetType == ElementTargetType.Selector ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SyncRunJsBaseOnCombo(RunJsBaseOn baseOn)
        {
            _isSyncingRunJsBaseOn = true;
            _runJsBaseOnComboBox.SelectedItem = ToRunJsBaseOnText(baseOn);
            _isSyncingRunJsBaseOn = false;
        }

        private void SyncRunJsTargetTypeCombo(ElementTargetType targetType)
        {
            _isSyncingRunJsTargetType = true;
            _runJsTargetTypeComboBox.SelectedItem = ToRunJsTargetTypeText(targetType);
            _isSyncingRunJsTargetType = false;
        }

        private void RefreshSendKeysRows(SendKeysBaseOn baseOn, ElementTargetType targetType)
        {
            if (_designerMode != DesignerMode.SendKeys)
            {
                return;
            }

            if (baseOn == SendKeysBaseOn.Tab)
            {
                _sendKeysTargetTypeRow.Visibility = Visibility.Collapsed;
                _sendKeysInputTabRow.Visibility = Visibility.Visible;
                _sendKeysInputElementRow.Visibility = Visibility.Collapsed;
                _sendKeysSelectorRow.Visibility = Visibility.Collapsed;
                return;
            }

            _sendKeysTargetTypeRow.Visibility = Visibility.Visible;
            _sendKeysInputElementRow.Visibility = targetType == ElementTargetType.Element ? Visibility.Visible : Visibility.Collapsed;
            _sendKeysInputTabRow.Visibility = targetType == ElementTargetType.Selector ? Visibility.Visible : Visibility.Collapsed;
            _sendKeysSelectorRow.Visibility = targetType == ElementTargetType.Selector ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SyncSendKeysBaseOnCombo(SendKeysBaseOn baseOn)
        {
            _isSyncingSendKeysBaseOn = true;
            _sendKeysBaseOnComboBox.SelectedItem = ToSendKeysBaseOnText(baseOn);
            _isSyncingSendKeysBaseOn = false;
        }

        private void SyncSendKeysTargetTypeCombo(ElementTargetType targetType)
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

        private static ComboBox BuildFindElementBaseOnComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.Items.Add("Tab");
            comboBox.Items.Add("Element");
            return comboBox;
        }

        private static ComboBox BuildRunJsBaseOnComboBox()
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

        private static ComboBox BuildSendKeysBaseOnComboBox()
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

        private static ComboBox BuildGetCookiesBaseOnComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.Items.Add("Tab");
            comboBox.Items.Add("Browser");
            return comboBox;
        }

        private static ComboBox BuildGetStorageScopeComboBox()
        {
            var comboBox = new ComboBox();
            comboBox.Items.Add("Session");
            comboBox.Items.Add("Local");
            return comboBox;
        }

        private static BrowserSwitchTabByType ReadSwitchTabByType(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["ByType"];
            if (modelProperty?.ComputedValue is BrowserSwitchTabByType value)
            {
                return value;
            }

            return BrowserSwitchTabByType.Index;
        }

        private static GetCookiesBaseOn ReadGetCookiesBaseOn(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["BaseOn"];
            if (modelProperty?.ComputedValue is GetCookiesBaseOn value)
            {
                return value;
            }

            return GetCookiesBaseOn.Tab;
        }

        private static FindElementBaseOn ReadFindElementBaseOn(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["BaseOn"];
            if (modelProperty?.ComputedValue is FindElementBaseOn value)
            {
                return value;
            }

            return FindElementBaseOn.Tab;
        }

        private static GetStorageScope ReadGetStorageScope(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["Scope"];
            if (modelProperty?.ComputedValue is GetStorageScope value)
            {
                return value;
            }

            return GetStorageScope.Session;
        }

        private static RunJsBaseOn ReadRunJsBaseOn(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["BaseOn"];
            if (modelProperty?.ComputedValue is RunJsBaseOn value)
            {
                return value;
            }

            return RunJsBaseOn.Element;
        }

        private static ElementTargetType ReadRunJsTargetType(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["TargetType"];
            if (modelProperty?.ComputedValue is ElementTargetType value)
            {
                return value;
            }

            return ElementTargetType.Selector;
        }

        private static SendKeysBaseOn ReadSendKeysBaseOn(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["BaseOn"];
            if (modelProperty?.ComputedValue is SendKeysBaseOn value)
            {
                return value;
            }

            return SendKeysBaseOn.Tab;
        }

        private static ElementTargetType ReadSendKeysTargetType(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["TargetType"];
            if (modelProperty?.ComputedValue is ElementTargetType value)
            {
                return value;
            }

            return ElementTargetType.Selector;
        }

        private static BrowserSwitchTabByType ParseSwitchTabByType(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Title":
                    return BrowserSwitchTabByType.Title;
                case "Title Regex":
                    return BrowserSwitchTabByType.TitleRegex;
                case "Url":
                    return BrowserSwitchTabByType.Url;
                case "Url Regex":
                    return BrowserSwitchTabByType.UrlRegex;
                case "Tab":
                    return BrowserSwitchTabByType.Tab;
                default:
                    return BrowserSwitchTabByType.Index;
            }
        }

        private static GetCookiesBaseOn ParseGetCookiesBaseOn(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Browser":
                    return GetCookiesBaseOn.Browser;
                default:
                    return GetCookiesBaseOn.Tab;
            }
        }

        private static FindElementBaseOn ParseFindElementBaseOn(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Element":
                    return FindElementBaseOn.Element;
                default:
                    return FindElementBaseOn.Tab;
            }
        }

        private static GetStorageScope ParseGetStorageScope(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Local":
                    return GetStorageScope.Local;
                default:
                    return GetStorageScope.Session;
            }
        }

        private static RunJsBaseOn ParseRunJsBaseOn(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Tab":
                    return RunJsBaseOn.Tab;
                default:
                    return RunJsBaseOn.Element;
            }
        }

        private static ElementTargetType ParseRunJsTargetType(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Element":
                    return ElementTargetType.Element;
                default:
                    return ElementTargetType.Selector;
            }
        }

        private static SendKeysBaseOn ParseSendKeysBaseOn(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Element":
                    return SendKeysBaseOn.Element;
                default:
                    return SendKeysBaseOn.Tab;
            }
        }

        private static ElementTargetType ParseSendKeysTargetType(string text)
        {
            switch ((text ?? string.Empty).Trim())
            {
                case "Element":
                    return ElementTargetType.Element;
                default:
                    return ElementTargetType.Selector;
            }
        }

        private static string ToSwitchTabByTypeText(BrowserSwitchTabByType byType)
        {
            switch (byType)
            {
                case BrowserSwitchTabByType.Title:
                    return "Title";
                case BrowserSwitchTabByType.TitleRegex:
                    return "Title Regex";
                case BrowserSwitchTabByType.Url:
                    return "Url";
                case BrowserSwitchTabByType.UrlRegex:
                    return "Url Regex";
                case BrowserSwitchTabByType.Tab:
                    return "Tab";
                default:
                    return "Index";
            }
        }

        private static string ToGetCookiesBaseOnText(GetCookiesBaseOn baseOn)
        {
            return baseOn == GetCookiesBaseOn.Browser ? "Browser" : "Tab";
        }

        private static string ToFindElementBaseOnText(FindElementBaseOn baseOn)
        {
            return baseOn == FindElementBaseOn.Element ? "Element" : "Tab";
        }

        private static string ToGetStorageScopeText(GetStorageScope scope)
        {
            return scope == GetStorageScope.Local ? "Local" : "Session";
        }

        private static string ToRunJsBaseOnText(RunJsBaseOn baseOn)
        {
            return baseOn == RunJsBaseOn.Tab ? "Tab" : "Element";
        }

        private static string ToRunJsTargetTypeText(ElementTargetType targetType)
        {
            return targetType == ElementTargetType.Element ? "Element" : "Selector";
        }

        private static string ToSendKeysBaseOnText(SendKeysBaseOn baseOn)
        {
            return baseOn == SendKeysBaseOn.Element ? "Element" : "Tab";
        }

        private static string ToSendKeysTargetTypeText(ElementTargetType targetType)
        {
            return targetType == ElementTargetType.Element ? "Element" : "Selector";
        }

        private void SyncGetStorageScopeCombo(GetStorageScope scope)
        {
            _isSyncingGetStorageScope = true;
            _getStorageScopeComboBox.SelectedItem = ToGetStorageScopeText(scope);
            _isSyncingGetStorageScope = false;
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
                case nameof(BrowserSwitchTabActivity):
                    return DesignerMode.BrowserSwitchTab;
                case nameof(BrowserCloseActivity):
                    return DesignerMode.BrowserClose;
                case nameof(BrowserGetAllTabActivity):
                    return DesignerMode.BrowserGetAllTab;
                case nameof(BrowserGetActivatedTabActivity):
                    return DesignerMode.BrowserGetActivatedTab;
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
                    else if (ModelItem != null && _designerMode == DesignerMode.FindElement)
                    {
                        var baseOn = ReadFindElementBaseOn(ModelItem);
                        SyncFindElementBaseOnCombo(baseOn);
                        RefreshFindElementRows(baseOn);
                        if (!_isUpdatingFindElementArguments)
                        {
                            ClearInactiveFindElementArguments(baseOn);
                        }
                    }
                    else if (ModelItem != null && (_designerMode == DesignerMode.GetCookies || _designerMode == DesignerMode.GetStorage))
                    {
                        var baseOn = ReadGetCookiesBaseOn(ModelItem);
                        SyncGetCookiesBaseOnCombo(baseOn);
                        RefreshGetCookiesRows(baseOn);
                        if (!_isUpdatingGetCookiesArguments)
                        {
                            ClearInactiveGetCookiesArguments(baseOn);
                        }
                        if (_designerMode == DesignerMode.GetStorage)
                        {
                            var scope = ReadGetStorageScope(ModelItem);
                            SyncGetStorageScopeCombo(scope);
                        }
                    }
                    else if (ModelItem != null && _designerMode == DesignerMode.RunJs)
                    {
                        var runJsBaseOn = ReadRunJsBaseOn(ModelItem);
                        var runJsTargetType = ReadRunJsTargetType(ModelItem);
                        SyncRunJsBaseOnCombo(runJsBaseOn);
                        SyncRunJsTargetTypeCombo(runJsTargetType);
                        RefreshRunJsRows(runJsBaseOn, runJsTargetType);
                        if (!_isUpdatingRunJsArguments)
                        {
                            ClearInactiveRunJsArguments(runJsBaseOn, runJsTargetType);
                        }
                    }
                    else if (ModelItem != null && _designerMode == DesignerMode.SendKeys)
                    {
                        var sendKeysBaseOn = ReadSendKeysBaseOn(ModelItem);
                        var sendKeysTargetType = ReadSendKeysTargetType(ModelItem);
                        SyncSendKeysBaseOnCombo(sendKeysBaseOn);
                        SyncSendKeysTargetTypeCombo(sendKeysTargetType);
                        RefreshSendKeysRows(sendKeysBaseOn, sendKeysTargetType);
                        if (!_isUpdatingSendKeysArguments)
                        {
                            ClearInactiveSendKeysArguments(sendKeysBaseOn, sendKeysTargetType);
                        }
                    }

                    RefreshRequiredBorders();
                }),
                DispatcherPriority.Background);
        }

        private void ClearInactiveSwitchTabArguments(BrowserSwitchTabByType activeByType)
        {
            if (ModelItem == null || _designerMode != DesignerMode.BrowserSwitchTab)
            {
                return;
            }

            _isUpdatingSwitchTabArguments = true;
            try
            {
                ClearSwitchTabArgumentIfHidden("Index", activeByType == BrowserSwitchTabByType.Index);
                ClearSwitchTabArgumentIfHidden("Title", activeByType == BrowserSwitchTabByType.Title);
                ClearSwitchTabArgumentIfHidden("TitleRe", activeByType == BrowserSwitchTabByType.TitleRegex);
                ClearSwitchTabArgumentIfHidden("Url", activeByType == BrowserSwitchTabByType.Url);
                ClearSwitchTabArgumentIfHidden("UrlRe", activeByType == BrowserSwitchTabByType.UrlRegex);
                ClearSwitchTabArgumentIfHidden("InputTab", activeByType == BrowserSwitchTabByType.Tab);
            }
            finally
            {
                _isUpdatingSwitchTabArguments = false;
            }
        }

        private void ClearInactiveGetCookiesArguments(GetCookiesBaseOn baseOn)
        {
            if (ModelItem == null || (_designerMode != DesignerMode.GetCookies && _designerMode != DesignerMode.GetStorage))
            {
                return;
            }

            _isUpdatingGetCookiesArguments = true;
            try
            {
                ClearGetCookiesArgumentIfHidden("InputBrowser", baseOn == GetCookiesBaseOn.Browser);
                ClearGetCookiesArgumentIfHidden("InputTab", baseOn == GetCookiesBaseOn.Tab);
            }
            finally
            {
                _isUpdatingGetCookiesArguments = false;
            }
        }

        private void ClearInactiveFindElementArguments(FindElementBaseOn baseOn)
        {
            if (ModelItem == null || _designerMode != DesignerMode.FindElement)
            {
                return;
            }

            _isUpdatingFindElementArguments = true;
            try
            {
                ClearRunJsArgumentIfHidden("Tab", baseOn == FindElementBaseOn.Tab);
                ClearRunJsArgumentIfHidden("Element", baseOn == FindElementBaseOn.Element);
            }
            finally
            {
                _isUpdatingFindElementArguments = false;
            }
        }

        private void ClearInactiveRunJsArguments(RunJsBaseOn baseOn, ElementTargetType targetType)
        {
            if (ModelItem == null || _designerMode != DesignerMode.RunJs)
            {
                return;
            }

            _isUpdatingRunJsArguments = true;
            try
            {
                var keepInputTab = baseOn == RunJsBaseOn.Tab || (baseOn == RunJsBaseOn.Element && targetType == ElementTargetType.Selector);
                var keepInputElement = baseOn == RunJsBaseOn.Element && targetType == ElementTargetType.Element;
                var keepSelector = baseOn == RunJsBaseOn.Element && targetType == ElementTargetType.Selector;

                ClearRunJsArgumentIfHidden("InputTab", keepInputTab);
                ClearRunJsArgumentIfHidden("InputElement", keepInputElement);
                ClearRunJsArgumentIfHidden("Selector", keepSelector);
            }
            finally
            {
                _isUpdatingRunJsArguments = false;
            }
        }

        private void ClearInactiveSendKeysArguments(SendKeysBaseOn baseOn, ElementTargetType targetType)
        {
            if (ModelItem == null || _designerMode != DesignerMode.SendKeys)
            {
                return;
            }

            _isUpdatingSendKeysArguments = true;
            try
            {
                var keepInputTab = baseOn == SendKeysBaseOn.Tab || (baseOn == SendKeysBaseOn.Element && targetType == ElementTargetType.Selector);
                var keepInputElement = baseOn == SendKeysBaseOn.Element && targetType == ElementTargetType.Element;
                var keepSelector = baseOn == SendKeysBaseOn.Element && targetType == ElementTargetType.Selector;

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
            var getCookiesBaseOn = ReadGetCookiesBaseOn(ModelItem);
            var findElementBaseOn = ReadFindElementBaseOn(ModelItem);
            var runJsBaseOn = ReadRunJsBaseOn(ModelItem);
            var runJsTargetType = ReadRunJsTargetType(ModelItem);
            var sendKeysBaseOn = ReadSendKeysBaseOn(ModelItem);
            var sendKeysTargetType = ReadSendKeysTargetType(ModelItem);

            SetRequiredBorder(_browserEditorBorder, IsBrowserMode(_designerMode), IsArgumentFilled(ModelItem, "Browser", _browserExpressionBox));
            SetRequiredBorder(_tabEditorBorder, IsTabMode(_designerMode), IsArgumentFilled(ModelItem, "Tab", _tabExpressionBox));
            SetRequiredBorder(_findElementTabEditorBorder, _designerMode == DesignerMode.FindElement && findElementBaseOn == FindElementBaseOn.Tab, IsArgumentFilled(ModelItem, "Tab", _findElementTabExpressionBox));
            SetRequiredBorder(_elementFindElementEditorBorder, _designerMode == DesignerMode.FindElement && findElementBaseOn == FindElementBaseOn.Element, IsArgumentFilled(ModelItem, "Element", _elementFindElementExpressionBox));
            SetRequiredBorder(_elementFindSelectorEditorBorder, _designerMode == DesignerMode.FindElement, IsArgumentFilled(ModelItem, "Selector", _elementFindSelectorExpressionBox));
            SetRequiredBorder(_tabExistsSelectorEditorBorder, _designerMode == DesignerMode.TabElementExists, IsArgumentFilled(ModelItem, "Selector", _tabExistsSelectorExpressionBox));
            SetRequiredBorder(_tabNavigateUrlEditorBorder, _designerMode == DesignerMode.TabNavigateUrl, IsArgumentFilled(ModelItem, "Url", _tabNavigateUrlExpressionBox));
            var isRunJsTab = _designerMode == DesignerMode.RunJs && runJsBaseOn == RunJsBaseOn.Tab;
            var isRunJsElementByElement = _designerMode == DesignerMode.RunJs && runJsBaseOn == RunJsBaseOn.Element && runJsTargetType == ElementTargetType.Element;
            var isRunJsElementBySelector = _designerMode == DesignerMode.RunJs && runJsBaseOn == RunJsBaseOn.Element && runJsTargetType == ElementTargetType.Selector;

            SetRequiredBorder(_runJsInputTabEditorBorder, isRunJsTab || isRunJsElementBySelector, IsArgumentFilled(ModelItem, "InputTab", _runJsInputTabExpressionBox));
            SetRequiredBorder(_runJsInputElementEditorBorder, isRunJsElementByElement, IsArgumentFilled(ModelItem, "InputElement", _runJsInputElementExpressionBox));
            SetRequiredBorder(_runJsSelectorEditorBorder, isRunJsElementBySelector, IsArgumentFilled(ModelItem, "Selector", _runJsSelectorExpressionBox));
            SetRequiredBorder(_runJsScriptEditorBorder, _designerMode == DesignerMode.RunJs, IsArgumentFilled(ModelItem, "Script", _runJsScriptExpressionBox));
            var isSendKeysTab = _designerMode == DesignerMode.SendKeys && sendKeysBaseOn == SendKeysBaseOn.Tab;
            var isSendKeysElementByElement = _designerMode == DesignerMode.SendKeys && sendKeysBaseOn == SendKeysBaseOn.Element && sendKeysTargetType == ElementTargetType.Element;
            var isSendKeysElementBySelector = _designerMode == DesignerMode.SendKeys && sendKeysBaseOn == SendKeysBaseOn.Element && sendKeysTargetType == ElementTargetType.Selector;

            SetRequiredBorder(_sendKeysInputTabEditorBorder, isSendKeysTab || isSendKeysElementBySelector, IsArgumentFilled(ModelItem, "InputTab", _sendKeysInputTabExpressionBox));
            SetRequiredBorder(_sendKeysInputElementEditorBorder, isSendKeysElementByElement, IsArgumentFilled(ModelItem, "InputElement", _sendKeysInputElementExpressionBox));
            SetRequiredBorder(_sendKeysSelectorEditorBorder, isSendKeysElementBySelector, IsArgumentFilled(ModelItem, "Selector", _sendKeysSelectorExpressionBox));
            SetRequiredBorder(_sendKeysKeysEditorBorder, _designerMode == DesignerMode.SendKeys, IsArgumentFilled(ModelItem, "Keys", _sendKeysKeysExpressionBox));

            SetRequiredBorder(_switchTabIndexEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BrowserSwitchTabByType.Index, IsArgumentFilled(ModelItem, "Index", _switchTabIndexExpressionBox));
            SetRequiredBorder(_switchTabTitleEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BrowserSwitchTabByType.Title, IsArgumentFilled(ModelItem, "Title", _switchTabTitleExpressionBox));
            SetRequiredBorder(_switchTabTitleReEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BrowserSwitchTabByType.TitleRegex, IsArgumentFilled(ModelItem, "TitleRe", _switchTabTitleReExpressionBox));
            SetRequiredBorder(_switchTabUrlEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BrowserSwitchTabByType.Url, IsArgumentFilled(ModelItem, "Url", _switchTabUrlExpressionBox));
            SetRequiredBorder(_switchTabUrlReEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BrowserSwitchTabByType.UrlRegex, IsArgumentFilled(ModelItem, "UrlRe", _switchTabUrlReExpressionBox));
            SetRequiredBorder(_switchTabTabEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BrowserSwitchTabByType.Tab, IsArgumentFilled(ModelItem, "InputTab", _switchTabTabExpressionBox));
            var isGetCookiesLikeMode = _designerMode == DesignerMode.GetCookies || _designerMode == DesignerMode.GetStorage;
            SetRequiredBorder(_getCookiesInputBrowserEditorBorder, isGetCookiesLikeMode && getCookiesBaseOn == GetCookiesBaseOn.Browser, IsArgumentFilled(ModelItem, "InputBrowser", _getCookiesInputBrowserExpressionBox));
            SetRequiredBorder(_getCookiesInputTabEditorBorder, isGetCookiesLikeMode && getCookiesBaseOn == GetCookiesBaseOn.Tab, IsArgumentFilled(ModelItem, "InputTab", _getCookiesInputTabExpressionBox));
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

        private enum DesignerMode
        {
            None,
            FindElement,
            BrowserSwitchTab,
            BrowserClose,
            BrowserGetAllTab,
            BrowserGetActivatedTab,
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
            SendKeys
        }
    }
}
