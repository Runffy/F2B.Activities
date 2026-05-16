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

namespace Playwright.Activities
{
    public sealed class CanvasFieldsActivityDesigner : ActivityDesigner
    {
        private const double RowLabelWidth = 78;
        private const double EditorMinWidth = 190;
        private const double RowSpacing = 4;

        private readonly Border _rootPanel;
        private readonly ExpressionTextBox _browserExpressionBox;
        private readonly ExpressionTextBox _tabExpressionBox;
        private readonly ExpressionTextBox _elementFindElementExpressionBox;
        private readonly ExpressionTextBox _elementFindSelectorExpressionBox;
        private readonly ExpressionTextBox _tabExistsSelectorExpressionBox;
        private readonly ExpressionTextBox _tabFindSelectorExpressionBox;
        private readonly ExpressionTextBox _tabNavigateUrlExpressionBox;
        private readonly ExpressionTextBox _tabRunJsScriptExpressionBox;
        private readonly ExpressionTextBox _tabSendKeysExpressionBox;
        private readonly ExpressionTextBox _switchTabIndexExpressionBox;
        private readonly ExpressionTextBox _switchTabTitleExpressionBox;
        private readonly ExpressionTextBox _switchTabTitleReExpressionBox;
        private readonly ExpressionTextBox _switchTabUrlExpressionBox;
        private readonly ExpressionTextBox _switchTabUrlReExpressionBox;
        private readonly ExpressionTextBox _switchTabTabExpressionBox;
        private readonly FrameworkElement _browserRow;
        private readonly FrameworkElement _tabRow;
        private readonly FrameworkElement _elementFindElementRow;
        private readonly FrameworkElement _elementFindSelectorRow;
        private readonly FrameworkElement _tabExistsSelectorRow;
        private readonly FrameworkElement _tabFindSelectorRow;
        private readonly FrameworkElement _tabNavigateUrlRow;
        private readonly FrameworkElement _tabRunJsScriptRow;
        private readonly FrameworkElement _tabSendKeysRow;
        private readonly FrameworkElement _switchTabByTypeRow;
        private readonly FrameworkElement _switchTabIndexRow;
        private readonly FrameworkElement _switchTabTitleRow;
        private readonly FrameworkElement _switchTabTitleReRow;
        private readonly FrameworkElement _switchTabUrlRow;
        private readonly FrameworkElement _switchTabUrlReRow;
        private readonly FrameworkElement _switchTabTabRow;
        private readonly Border _elementFindElementEditorBorder;
        private readonly Border _elementFindSelectorEditorBorder;
        private readonly Border _browserEditorBorder;
        private readonly Border _tabEditorBorder;
        private readonly Border _tabExistsSelectorEditorBorder;
        private readonly Border _tabFindSelectorEditorBorder;
        private readonly Border _tabNavigateUrlEditorBorder;
        private readonly Border _tabRunJsScriptEditorBorder;
        private readonly Border _tabSendKeysEditorBorder;
        private readonly Border _switchTabIndexEditorBorder;
        private readonly Border _switchTabTitleEditorBorder;
        private readonly Border _switchTabTitleReEditorBorder;
        private readonly Border _switchTabUrlEditorBorder;
        private readonly Border _switchTabUrlReEditorBorder;
        private readonly Border _switchTabTabEditorBorder;
        private readonly ComboBox _switchTabByTypeComboBox;

        private DesignerMode _designerMode = DesignerMode.None;
        private bool _isSyncingSwitchByType;
        private bool _isUpdatingSwitchTabArguments;

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

            _browserExpressionBox = CreateExpressionTextBox("Browser", typeof(PwBrowser));
            _tabExpressionBox = CreateExpressionTextBox("Tab", typeof(PwTab));
            _elementFindElementExpressionBox = CreateExpressionTextBox("Element", typeof(PwElement));
            _elementFindSelectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _tabExistsSelectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _tabFindSelectorExpressionBox = CreateExpressionTextBox("Selector", typeof(string));
            _tabNavigateUrlExpressionBox = CreateExpressionTextBox("Url", typeof(string));
            _tabRunJsScriptExpressionBox = CreateExpressionTextBox("Script", typeof(string));
            _tabSendKeysExpressionBox = CreateExpressionTextBox("Keys", typeof(string));
            _switchTabIndexExpressionBox = CreateExpressionTextBox("Index", typeof(int?));
            _switchTabTitleExpressionBox = CreateExpressionTextBox("Title", typeof(string));
            _switchTabTitleReExpressionBox = CreateExpressionTextBox("TitleRe", typeof(string));
            _switchTabUrlExpressionBox = CreateExpressionTextBox("Url", typeof(string));
            _switchTabUrlReExpressionBox = CreateExpressionTextBox("UrlRe", typeof(string));
            _switchTabTabExpressionBox = CreateExpressionTextBox("InputTab", typeof(PwTab));

            _browserRow = CreateRow("Browser", _browserExpressionBox, out _browserEditorBorder);
            _tabRow = CreateRow("Tab", _tabExpressionBox, out _tabEditorBorder);
            _elementFindElementRow = CreateRow("Element", _elementFindElementExpressionBox, out _elementFindElementEditorBorder);
            _elementFindSelectorRow = CreateRow("Selector", _elementFindSelectorExpressionBox, out _elementFindSelectorEditorBorder, RowSpacing);
            _tabExistsSelectorRow = CreateRow("Selector", _tabExistsSelectorExpressionBox, out _tabExistsSelectorEditorBorder);
            _tabFindSelectorRow = CreateRow("Selector", _tabFindSelectorExpressionBox, out _tabFindSelectorEditorBorder);
            _tabNavigateUrlRow = CreateRow("Url", _tabNavigateUrlExpressionBox, out _tabNavigateUrlEditorBorder);
            _tabRunJsScriptRow = CreateRow("Script", _tabRunJsScriptExpressionBox, out _tabRunJsScriptEditorBorder);
            _tabSendKeysRow = CreateRow("Keys", _tabSendKeysExpressionBox, out _tabSendKeysEditorBorder);

            _switchTabByTypeComboBox = BuildSwitchTabByTypeComboBox();
            _switchTabByTypeComboBox.SelectionChanged += OnSwitchTabByTypeSelectionChanged;
            _switchTabByTypeRow = CreateRow("ByType", _switchTabByTypeComboBox);
            _switchTabIndexRow = CreateRow("Index", _switchTabIndexExpressionBox, out _switchTabIndexEditorBorder, RowSpacing);
            _switchTabTitleRow = CreateRow("Title", _switchTabTitleExpressionBox, out _switchTabTitleEditorBorder, RowSpacing);
            _switchTabTitleReRow = CreateRow("TitleRe", _switchTabTitleReExpressionBox, out _switchTabTitleReEditorBorder, RowSpacing);
            _switchTabUrlRow = CreateRow("Url", _switchTabUrlExpressionBox, out _switchTabUrlEditorBorder, RowSpacing);
            _switchTabUrlReRow = CreateRow("UrlRe", _switchTabUrlReExpressionBox, out _switchTabUrlReEditorBorder, RowSpacing);
            _switchTabTabRow = CreateRow("Tab", _switchTabTabExpressionBox, out _switchTabTabEditorBorder, RowSpacing);

            body.Children.Add(_browserRow);
            body.Children.Add(_tabRow);
            body.Children.Add(_elementFindElementRow);
            body.Children.Add(_elementFindSelectorRow);
            body.Children.Add(_tabExistsSelectorRow);
            body.Children.Add(_tabFindSelectorRow);
            body.Children.Add(_tabNavigateUrlRow);
            body.Children.Add(_tabRunJsScriptRow);
            body.Children.Add(_tabSendKeysRow);
            body.Children.Add(_switchTabByTypeRow);
            body.Children.Add(_switchTabIndexRow);
            body.Children.Add(_switchTabTitleRow);
            body.Children.Add(_switchTabTitleReRow);
            body.Children.Add(_switchTabUrlRow);
            body.Children.Add(_switchTabUrlReRow);
            body.Children.Add(_switchTabTabRow);

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

        private void RefreshModeRows()
        {
            _browserRow.Visibility = IsBrowserMode(_designerMode) ? Visibility.Visible : Visibility.Collapsed;
            _tabRow.Visibility = IsTabMode(_designerMode) ? Visibility.Visible : Visibility.Collapsed;
            _elementFindElementRow.Visibility = _designerMode == DesignerMode.ElementFindElement ? Visibility.Visible : Visibility.Collapsed;
            _elementFindSelectorRow.Visibility = _designerMode == DesignerMode.ElementFindElement ? Visibility.Visible : Visibility.Collapsed;
            _tabExistsSelectorRow.Visibility = _designerMode == DesignerMode.TabElementExists ? Visibility.Visible : Visibility.Collapsed;
            _tabFindSelectorRow.Visibility = _designerMode == DesignerMode.TabFindElement ? Visibility.Visible : Visibility.Collapsed;
            _tabNavigateUrlRow.Visibility = _designerMode == DesignerMode.TabNavigateUrl ? Visibility.Visible : Visibility.Collapsed;
            _tabRunJsScriptRow.Visibility = _designerMode == DesignerMode.TabRunJs ? Visibility.Visible : Visibility.Collapsed;
            _tabSendKeysRow.Visibility = _designerMode == DesignerMode.TabSendKeys ? Visibility.Visible : Visibility.Collapsed;
            _switchTabByTypeRow.Visibility = _designerMode == DesignerMode.BrowserSwitchTab ? Visibility.Visible : Visibility.Collapsed;

            if (_designerMode != DesignerMode.BrowserSwitchTab)
            {
                _switchTabIndexRow.Visibility = Visibility.Collapsed;
                _switchTabTitleRow.Visibility = Visibility.Collapsed;
                _switchTabTitleReRow.Visibility = Visibility.Collapsed;
                _switchTabUrlRow.Visibility = Visibility.Collapsed;
                _switchTabUrlReRow.Visibility = Visibility.Collapsed;
                _switchTabTabRow.Visibility = Visibility.Collapsed;
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

        private static FrameworkElement CreateRow(string label, FrameworkElement editor, double top = 0)
        {
            return CreateRow(label, editor, out _, top);
        }

        private static FrameworkElement CreateRow(string label, FrameworkElement editor, out Border editorBorder, double top = 0)
        {
            NormalizeEditor(editor);

            var row = new DockPanel
            {
                LastChildFill = true,
                Margin = new Thickness(0, top, 0, 0)
            };
            row.Children.Add(new TextBlock
            {
                Text = label,
                Width = RowLabelWidth,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.SemiBold
            });
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

            // 对齐 LogMessageDesigner：使用双向绑定把画布输入直接同步到 ModelItem 属性。
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

        private static BrowserSwitchTabByType ReadSwitchTabByType(ModelItem modelItem)
        {
            var modelProperty = modelItem.Properties["ByType"];
            if (modelProperty?.ComputedValue is BrowserSwitchTabByType value)
            {
                return value;
            }

            return BrowserSwitchTabByType.Index;
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

        private static DesignerMode ResolveMode(ModelItem modelItem)
        {
            var activityType = modelItem?.ItemType;
            if (activityType == null)
            {
                return DesignerMode.None;
            }

            switch (activityType.Name)
            {
                case nameof(ElementFindElementActivity):
                    return DesignerMode.ElementFindElement;
                case nameof(BrowserSwitchTabActivity):
                    return DesignerMode.BrowserSwitchTab;
                case nameof(BrowserCloseActivity):
                    return DesignerMode.BrowserClose;
                case nameof(BrowserGetAllTabActivity):
                    return DesignerMode.BrowserGetAllTab;
                case nameof(BrowserGetActivatedTabActivity):
                    return DesignerMode.BrowserGetActivatedTab;
                case nameof(BrowserGetCookiesActivity):
                    return DesignerMode.BrowserGetCookies;
                case nameof(BrowserGetLocalStorageActivity):
                    return DesignerMode.BrowserGetLocalStorage;
                case nameof(BrowserGetLatestTabActivity):
                    return DesignerMode.BrowserGetLatestTab;
                case nameof(BrowserGetSessionStorageActivity):
                    return DesignerMode.BrowserGetSessionStorage;
                case nameof(BrowserNewTabActivity):
                    return DesignerMode.BrowserNewTab;
                case nameof(TabBackActivity):
                    return DesignerMode.TabBack;
                case nameof(TabCloseActivity):
                    return DesignerMode.TabClose;
                case nameof(TabElementExistsActivity):
                    return DesignerMode.TabElementExists;
                case nameof(TabFindElementActivity):
                    return DesignerMode.TabFindElement;
                case nameof(TabForwardActivity):
                    return DesignerMode.TabForward;
                case nameof(TabGetCookiesActivity):
                    return DesignerMode.TabGetCookies;
                case nameof(TabGetInfoActivity):
                    return DesignerMode.TabGetInfo;
                case nameof(TabGetLocalStorageActivity):
                    return DesignerMode.TabGetLocalStorage;
                case nameof(TabNavigateUrlActivity):
                    return DesignerMode.TabNavigateUrl;
                case nameof(TabRefreshActivity):
                    return DesignerMode.TabRefresh;
                case nameof(TabRunJsActivity):
                    return DesignerMode.TabRunJs;
                case nameof(TabGetSessionStorageActivity):
                    return DesignerMode.TabGetSessionStorage;
                case nameof(TabSendKeysActivity):
                    return DesignerMode.TabSendKeys;
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

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
            {
                return;
            }

            var byType = ReadSwitchTabByType(ModelItem);

            SetRequiredBorder(_browserEditorBorder, IsBrowserMode(_designerMode), IsArgumentFilled(ModelItem, "Browser", _browserExpressionBox));
            SetRequiredBorder(_tabEditorBorder, IsTabMode(_designerMode), IsArgumentFilled(ModelItem, "Tab", _tabExpressionBox));
            SetRequiredBorder(_elementFindElementEditorBorder, _designerMode == DesignerMode.ElementFindElement, IsArgumentFilled(ModelItem, "Element", _elementFindElementExpressionBox));
            SetRequiredBorder(_elementFindSelectorEditorBorder, _designerMode == DesignerMode.ElementFindElement, IsArgumentFilled(ModelItem, "Selector", _elementFindSelectorExpressionBox));
            SetRequiredBorder(_tabExistsSelectorEditorBorder, _designerMode == DesignerMode.TabElementExists, IsArgumentFilled(ModelItem, "Selector", _tabExistsSelectorExpressionBox));
            SetRequiredBorder(_tabFindSelectorEditorBorder, _designerMode == DesignerMode.TabFindElement, IsArgumentFilled(ModelItem, "Selector", _tabFindSelectorExpressionBox));
            SetRequiredBorder(_tabNavigateUrlEditorBorder, _designerMode == DesignerMode.TabNavigateUrl, IsArgumentFilled(ModelItem, "Url", _tabNavigateUrlExpressionBox));
            SetRequiredBorder(_tabRunJsScriptEditorBorder, _designerMode == DesignerMode.TabRunJs, IsArgumentFilled(ModelItem, "Script", _tabRunJsScriptExpressionBox));
            SetRequiredBorder(_tabSendKeysEditorBorder, _designerMode == DesignerMode.TabSendKeys, IsArgumentFilled(ModelItem, "Keys", _tabSendKeysExpressionBox));

            SetRequiredBorder(_switchTabIndexEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BrowserSwitchTabByType.Index, IsArgumentFilled(ModelItem, "Index", _switchTabIndexExpressionBox));
            SetRequiredBorder(_switchTabTitleEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BrowserSwitchTabByType.Title, IsArgumentFilled(ModelItem, "Title", _switchTabTitleExpressionBox));
            SetRequiredBorder(_switchTabTitleReEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BrowserSwitchTabByType.TitleRegex, IsArgumentFilled(ModelItem, "TitleRe", _switchTabTitleReExpressionBox));
            SetRequiredBorder(_switchTabUrlEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BrowserSwitchTabByType.Url, IsArgumentFilled(ModelItem, "Url", _switchTabUrlExpressionBox));
            SetRequiredBorder(_switchTabUrlReEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BrowserSwitchTabByType.UrlRegex, IsArgumentFilled(ModelItem, "UrlRe", _switchTabUrlReExpressionBox));
            SetRequiredBorder(_switchTabTabEditorBorder, _designerMode == DesignerMode.BrowserSwitchTab && byType == BrowserSwitchTabByType.Tab, IsArgumentFilled(ModelItem, "InputTab", _switchTabTabExpressionBox));
        }

        private static bool IsArgumentFilled(ModelItem modelItem, string propertyName, ExpressionTextBox editor)
        {
            var property = modelItem?.Properties[propertyName];
            if (property == null)
            {
                return HasEditorInput(editor);
            }

            // WF 设计器里，IsSet 是判断“用户是否已赋值”最稳定的标志。
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
            _elementFindElementExpressionBox.OwnerActivity = owner;
            _elementFindSelectorExpressionBox.OwnerActivity = owner;
            _tabExistsSelectorExpressionBox.OwnerActivity = owner;
            _tabFindSelectorExpressionBox.OwnerActivity = owner;
            _tabNavigateUrlExpressionBox.OwnerActivity = owner;
            _tabRunJsScriptExpressionBox.OwnerActivity = owner;
            _tabSendKeysExpressionBox.OwnerActivity = owner;
            _switchTabIndexExpressionBox.OwnerActivity = owner;
            _switchTabTitleExpressionBox.OwnerActivity = owner;
            _switchTabTitleReExpressionBox.OwnerActivity = owner;
            _switchTabUrlExpressionBox.OwnerActivity = owner;
            _switchTabUrlReExpressionBox.OwnerActivity = owner;
            _switchTabTabExpressionBox.OwnerActivity = owner;
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
                case DesignerMode.BrowserGetCookies:
                case DesignerMode.BrowserGetLocalStorage:
                case DesignerMode.BrowserGetLatestTab:
                case DesignerMode.BrowserGetSessionStorage:
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
                case DesignerMode.TabFindElement:
                case DesignerMode.TabForward:
                case DesignerMode.TabGetCookies:
                case DesignerMode.TabGetInfo:
                case DesignerMode.TabGetLocalStorage:
                case DesignerMode.TabNavigateUrl:
                case DesignerMode.TabRefresh:
                case DesignerMode.TabRunJs:
                case DesignerMode.TabGetSessionStorage:
                case DesignerMode.TabSendKeys:
                    return true;
                default:
                    return false;
            }
        }

        private enum DesignerMode
        {
            None,
            ElementFindElement,
            BrowserSwitchTab,
            BrowserClose,
            BrowserGetAllTab,
            BrowserGetActivatedTab,
            BrowserGetCookies,
            BrowserGetLocalStorage,
            BrowserGetLatestTab,
            BrowserGetSessionStorage,
            BrowserNewTab,
            TabBack,
            TabClose,
            TabElementExists,
            TabFindElement,
            TabForward,
            TabGetCookies,
            TabGetInfo,
            TabGetLocalStorage,
            TabNavigateUrl,
            TabRefresh,
            TabGetSessionStorage,
            TabRunJs,
            TabSendKeys
        }
    }
}
