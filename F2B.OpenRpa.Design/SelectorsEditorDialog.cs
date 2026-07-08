using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace F2B.OpenRpa.Design
{
    public sealed class SelectorsEditorDialog : Window
    {
        private readonly TextBox _editorTextBox;
        private readonly TextBlock _hintText;
        private readonly ListView _itemsList;
        private readonly ObservableCollection<SelectorItemRow> _items;
        private int? _editingIndex;

        public IReadOnlyList<string> Selectors { get; private set; }

        public SelectorsEditorDialog(IEnumerable<string> initialSelectors, bool replaceExpressionWarning)
        {
            Title = "Selectors Editor";
            Width = 720;
            Height = 520;
            MinWidth = 560;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.CanResizeWithGrip;

            _items = new ObservableCollection<SelectorItemRow>();
            var index = 0;
            foreach (var selector in initialSelectors ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(selector))
                {
                    continue;
                }

                _items.Add(new SelectorItemRow(index++, NormalizeSelectorText(selector)));
            }

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.2, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _hintText = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 8),
                Visibility = replaceExpressionWarning ? Visibility.Visible : Visibility.Collapsed
            };
            if (replaceExpressionWarning)
            {
                _hintText.Text = "当前 Selectors 是自定义表达式。在此保存将替换为标准 VB.NET 字符串数组表达式，例如 New String() { \"...\", \"...\" }。";
            }

            Grid.SetRow(_hintText, 0);
            root.Children.Add(_hintText);

            _editorTextBox = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = TextWrapping.NoWrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                MinHeight = 120
            };
            Grid.SetRow(_editorTextBox, 1);
            root.Children.Add(_editorTextBox);

            var itemsHeader = new Grid { Margin = new Thickness(0, 12, 0, 6) };
            itemsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            itemsHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var itemsTitle = new TextBlock
            {
                Text = "Selector Items",
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(itemsTitle, 0);
            itemsHeader.Children.Add(itemsTitle);

            var addButton = new Button
            {
                Content = "+",
                Width = 28,
                Height = 24,
                Padding = new Thickness(0),
                ToolTip = "Add selector from editor above"
            };
            addButton.Click += (sender, args) => AddOrUpdateFromEditor();
            Grid.SetColumn(addButton, 1);
            itemsHeader.Children.Add(addButton);

            Grid.SetRow(itemsHeader, 2);
            root.Children.Add(itemsHeader);

            _itemsList = CreateItemsList();
            Grid.SetRow(_itemsList, 3);
            root.Children.Add(_itemsList);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };

            var okButton = new Button
            {
                Content = "OK",
                MinWidth = 72,
                Margin = new Thickness(0, 0, 8, 0),
                IsDefault = true
            };
            okButton.Click += (sender, args) =>
            {
                if (_items.Count == 0)
                {
                    MessageBox.Show(this, "至少添加一个 selector。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Selectors = _items.Select(item => item.SelectorXml).ToArray();
                DialogResult = true;
                Close();
            };

            var cancelButton = new Button
            {
                Content = "Cancel",
                MinWidth = 72,
                IsCancel = true
            };
            cancelButton.Click += (sender, args) =>
            {
                DialogResult = false;
                Close();
            };

            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);
            Grid.SetRow(buttons, 4);
            root.Children.Add(buttons);

            Content = root;
            Loaded += (sender, args) => _editorTextBox.Focus();
        }

        private ListView CreateItemsList()
        {
            var gridView = new GridView();
            gridView.Columns.Add(new GridViewColumn
            {
                Header = "Index",
                Width = 56,
                DisplayMemberBinding = new System.Windows.Data.Binding(nameof(SelectorItemRow.Index))
            });
            gridView.Columns.Add(new GridViewColumn
            {
                Header = "Selector Xml",
                Width = 420,
                DisplayMemberBinding = new System.Windows.Data.Binding(nameof(SelectorItemRow.SelectorXml))
            });

            var actionsColumn = new GridViewColumn
            {
                Header = "Actions",
                Width = 88
            };
            actionsColumn.CellTemplate = CreateActionsCellTemplate();
            gridView.Columns.Add(actionsColumn);

            return new ListView
            {
                View = gridView,
                ItemsSource = _items,
                MinHeight = 140
            };
        }

        private DataTemplate CreateActionsCellTemplate()
        {
            var template = new DataTemplate();
            var panelFactory = new FrameworkElementFactory(typeof(StackPanel));
            panelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            var editFactory = new FrameworkElementFactory(typeof(Button));
            editFactory.SetValue(Button.ContentProperty, "\u270E");
            editFactory.SetValue(Button.WidthProperty, 28.0);
            editFactory.SetValue(Button.HeightProperty, 22.0);
            editFactory.SetValue(Button.PaddingProperty, new Thickness(0));
            editFactory.SetValue(Button.MarginProperty, new Thickness(0, 0, 4, 0));
            editFactory.SetValue(Button.ToolTipProperty, "Edit selector");
            editFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnEditRowClicked));

            var deleteFactory = new FrameworkElementFactory(typeof(Button));
            deleteFactory.SetValue(Button.ContentProperty, "-");
            deleteFactory.SetValue(Button.WidthProperty, 28.0);
            deleteFactory.SetValue(Button.HeightProperty, 22.0);
            deleteFactory.SetValue(Button.PaddingProperty, new Thickness(0));
            deleteFactory.SetValue(Button.ToolTipProperty, "Delete selector");
            deleteFactory.AddHandler(Button.ClickEvent, new RoutedEventHandler(OnDeleteRowClicked));

            panelFactory.AppendChild(editFactory);
            panelFactory.AppendChild(deleteFactory);
            template.VisualTree = panelFactory;
            return template;
        }

        private void OnEditRowClicked(object sender, RoutedEventArgs e)
        {
            var row = GetRowFromSender(sender);
            if (row == null)
            {
                return;
            }

            _editingIndex = row.Index;
            _editorTextBox.Text = row.SelectorXml;
            _editorTextBox.Focus();
            _editorTextBox.SelectAll();
        }

        private void OnDeleteRowClicked(object sender, RoutedEventArgs e)
        {
            var row = GetRowFromSender(sender);
            if (row == null)
            {
                return;
            }

            _items.Remove(row);
            ReindexItems();

            if (_editingIndex == row.Index)
            {
                _editingIndex = null;
                _editorTextBox.Clear();
            }
        }

        private static SelectorItemRow GetRowFromSender(object sender)
        {
            if (sender is Button button && button.DataContext is SelectorItemRow row)
            {
                return row;
            }

            return null;
        }

        private void AddOrUpdateFromEditor()
        {
            var text = NormalizeSelectorText(_editorTextBox.Text);
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show(this, "请先在上方编辑区输入 selector。", Title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_editingIndex.HasValue)
            {
                var existing = _items.FirstOrDefault(item => item.Index == _editingIndex.Value);
                if (existing != null)
                {
                    existing.SelectorXml = text;
                    _itemsList.Items.Refresh();
                }

                _editingIndex = null;
            }
            else
            {
                _items.Add(new SelectorItemRow(_items.Count, text));
            }

            _editorTextBox.Clear();
            ReindexItems();
        }

        private void ReindexItems()
        {
            for (var i = 0; i < _items.Count; i++)
            {
                _items[i].Index = i;
            }

            _itemsList.Items.Refresh();
        }

        private static string NormalizeSelectorText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        }

        private sealed class SelectorItemRow
        {
            public SelectorItemRow(int index, string selectorXml)
            {
                Index = index;
                SelectorXml = selectorXml ?? string.Empty;
            }

            public int Index { get; set; }

            public string SelectorXml { get; set; }
        }
    }
}
