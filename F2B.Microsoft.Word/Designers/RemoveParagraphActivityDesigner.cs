using System;
using System.Activities.Presentation;
using System.Activities.Presentation.View;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using InteropWord = Microsoft.Office.Interop.Word;

namespace F2B.Microsoft.Word
{
    public sealed class RemoveParagraphActivityDesigner : ActivityDesigner
    {
        private const string LabelColumn = "WordRemoveParagraphLabelColumn";

        private readonly Border _rootPanel;
        private readonly ComboBox _locateModeComboBox;
        private readonly FrameworkElement _keywordRow;
        private readonly FrameworkElement _bookmarkRow;
        private readonly FrameworkElement _paragraphIndexRow;
        private readonly FrameworkElement _countRow;
        private readonly FrameworkElement _matchCaseRow;
        private bool _isSyncingLocateMode;

        public RemoveParagraphActivityDesigner()
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

            _locateModeComboBox = WordDesignerShared.BuildDescriptionComboBox<WordParagraphLocateMode>();
            _locateModeComboBox.SelectionChanged += OnLocateModeSelectionChanged;
            body.Children.Add(WordDesignerShared.CreateRow("Locate Mode", _locateModeComboBox, LabelColumn, WordDesignerShared.RowSpacing));

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

            _paragraphIndexRow = WordDesignerShared.CreateRow(
                "Paragraph Index",
                WordDesignerShared.CreateInExpressionTextBox("ParagraphIndex", typeof(int)),
                LabelColumn,
                WordDesignerShared.RowSpacing);
            body.Children.Add(_paragraphIndexRow);

            _countRow = WordDesignerShared.CreateRow(
                "Count",
                WordDesignerShared.CreateInExpressionTextBox("Count", typeof(int)),
                LabelColumn,
                WordDesignerShared.RowSpacing);
            body.Children.Add(_countRow);

            _matchCaseRow = WordDesignerShared.CreateRow(
                "Match Case",
                WordDesignerShared.CreateInExpressionTextBox("MatchCase", typeof(bool)),
                LabelColumn,
                WordDesignerShared.RowSpacing);
            body.Children.Add(_matchCaseRow);

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
            SyncLocateMode(WordDesignerShared.ReadEnum(ModelItem, "LocateMode", WordParagraphLocateMode.Keyword));
            RefreshLocateRows();
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "LocateMode", StringComparison.Ordinal))
            {
                SyncLocateMode(WordDesignerShared.ReadEnum(ModelItem, "LocateMode", WordParagraphLocateMode.Keyword));
                RefreshLocateRows();
            }
        }

        private void OnLocateModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingLocateMode || ModelItem == null)
            {
                return;
            }

            var mode = WordDesignerShared.ReadSelectedEnum(_locateModeComboBox, WordParagraphLocateMode.Keyword);
            ModelItem.Properties["LocateMode"].SetValue(mode);
            RefreshLocateRows();
        }

        private void RefreshLocateRows()
        {
            var mode = WordDesignerShared.ReadEnum(ModelItem, "LocateMode", WordParagraphLocateMode.Keyword);
            _keywordRow.Visibility = mode == WordParagraphLocateMode.Keyword ? Visibility.Visible : Visibility.Collapsed;
            _bookmarkRow.Visibility = mode == WordParagraphLocateMode.Bookmark ? Visibility.Visible : Visibility.Collapsed;
            _paragraphIndexRow.Visibility = mode == WordParagraphLocateMode.ParagraphIndex ? Visibility.Visible : Visibility.Collapsed;
            _countRow.Visibility = mode == WordParagraphLocateMode.Keyword ? Visibility.Visible : Visibility.Collapsed;
            _matchCaseRow.Visibility = mode == WordParagraphLocateMode.Keyword ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SyncLocateMode(WordParagraphLocateMode mode)
        {
            _isSyncingLocateMode = true;
            WordDesignerShared.SelectEnumItem(_locateModeComboBox, mode);
            _isSyncingLocateMode = false;
        }
    }
}
