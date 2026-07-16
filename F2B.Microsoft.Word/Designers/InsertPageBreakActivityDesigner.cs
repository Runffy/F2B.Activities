using System;
using System.Activities.Presentation;
using System.Activities.Presentation.View;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    public sealed class InsertPageBreakActivityDesigner : ActivityDesigner
    {
        private const string LabelColumn = "WordInsertPageBreakLabelColumn";

        private readonly Border _rootPanel;
        private readonly ComboBox _locateModeComboBox;
        private readonly FrameworkElement _relativePositionRow;
        private readonly ComboBox _relativePositionComboBox;
        private readonly FrameworkElement _keywordRow;
        private readonly FrameworkElement _bookmarkRow;
        private bool _isSyncingLocateMode;
        private bool _isSyncingRelativePosition;

        public InsertPageBreakActivityDesigner()
        {
            var host = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(4)
            };

            _rootPanel = new Border { Padding = new Thickness(6, 5, 6, 5) };
            var body = new StackPanel { Orientation = Orientation.Vertical };
            Grid.SetIsSharedSizeScope(body, true);

            body.Children.Add(WordDesignerShared.CreateRow(
                "Word File Path",
                WordDesignerShared.CreateInExpressionTextBox("WordFilePath", typeof(string)),
                LabelColumn));

            body.Children.Add(WordDesignerShared.CreateRow(
                "Document",
                WordDesignerShared.CreateInOutExpressionTextBox("Document", typeof(InteropWord.Document)),
                LabelColumn,
                WordDesignerShared.RowSpacing));

            _locateModeComboBox = WordDesignerShared.BuildDescriptionComboBox<WordPageBreakLocateMode>();
            _locateModeComboBox.SelectionChanged += OnLocateModeSelectionChanged;
            body.Children.Add(WordDesignerShared.CreateRow("Locate Mode", _locateModeComboBox, LabelColumn, WordDesignerShared.RowSpacing));

            _relativePositionComboBox = WordDesignerShared.BuildDescriptionComboBox<WordInsertRelativePosition>();
            _relativePositionComboBox.SelectionChanged += OnRelativePositionSelectionChanged;
            _relativePositionRow = WordDesignerShared.CreateRow(
                "Relative Position",
                _relativePositionComboBox,
                LabelColumn,
                WordDesignerShared.RowSpacing);
            body.Children.Add(_relativePositionRow);

            _keywordRow = WordDesignerShared.CreateRow(
                "Keyword",
                WordDesignerShared.CreateInExpressionTextBox("Keyword", typeof(string)),
                LabelColumn,
                WordDesignerShared.RowSpacing);
            body.Children.Add(_keywordRow);

            _bookmarkRow = WordDesignerShared.CreateRow(
                "Bookmark Name",
                WordDesignerShared.CreateInExpressionTextBox("BookmarkName", typeof(string)),
                LabelColumn,
                WordDesignerShared.RowSpacing);
            body.Children.Add(_bookmarkRow);

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

            WordDesignerShared.BindExpressionOwner(_rootPanel, ModelItem);
            SyncLocateMode(WordDesignerShared.ReadEnum(ModelItem, "LocateMode", WordPageBreakLocateMode.DocumentEnd));
            SyncRelativePosition(WordDesignerShared.ReadEnum(ModelItem, "RelativePosition", WordInsertRelativePosition.After));
            RefreshLocateRows();
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "LocateMode", StringComparison.Ordinal))
            {
                SyncLocateMode(WordDesignerShared.ReadEnum(ModelItem, "LocateMode", WordPageBreakLocateMode.DocumentEnd));
                RefreshLocateRows();
            }
            else if (string.Equals(e.PropertyName, "RelativePosition", StringComparison.Ordinal))
            {
                SyncRelativePosition(WordDesignerShared.ReadEnum(ModelItem, "RelativePosition", WordInsertRelativePosition.After));
            }
        }

        private void OnLocateModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingLocateMode || ModelItem == null)
            {
                return;
            }

            var mode = WordDesignerShared.ReadSelectedEnum(_locateModeComboBox, WordPageBreakLocateMode.DocumentEnd);
            ModelItem.Properties["LocateMode"].SetValue(mode);
            RefreshLocateRows();
        }

        private void OnRelativePositionSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingRelativePosition || ModelItem == null)
            {
                return;
            }

            var position = WordDesignerShared.ReadSelectedEnum(_relativePositionComboBox, WordInsertRelativePosition.After);
            ModelItem.Properties["RelativePosition"].SetValue(position);
        }

        private void RefreshLocateRows()
        {
            var mode = WordDesignerShared.ReadEnum(ModelItem, "LocateMode", WordPageBreakLocateMode.DocumentEnd);
            var needsRelative = mode == WordPageBreakLocateMode.Keyword || mode == WordPageBreakLocateMode.Bookmark;
            _relativePositionRow.Visibility = needsRelative ? Visibility.Visible : Visibility.Collapsed;
            _keywordRow.Visibility = mode == WordPageBreakLocateMode.Keyword ? Visibility.Visible : Visibility.Collapsed;
            _bookmarkRow.Visibility = mode == WordPageBreakLocateMode.Bookmark ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SyncLocateMode(WordPageBreakLocateMode mode)
        {
            _isSyncingLocateMode = true;
            WordDesignerShared.SelectEnumItem(_locateModeComboBox, mode);
            _isSyncingLocateMode = false;
        }

        private void SyncRelativePosition(WordInsertRelativePosition position)
        {
            _isSyncingRelativePosition = true;
            WordDesignerShared.SelectEnumItem(_relativePositionComboBox, position);
            _isSyncingRelativePosition = false;
        }
    }
}
