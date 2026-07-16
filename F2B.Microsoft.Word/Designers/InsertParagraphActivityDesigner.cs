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
    public sealed class InsertParagraphActivityDesigner : ActivityDesigner
    {
        private const string LabelColumn = "WordInsertParagraphLabelColumn";

        private readonly Border _rootPanel;
        private readonly ExpressionTextBox _wordFilePathExpressionBox;
        private readonly ExpressionTextBox _documentExpressionBox;
        private readonly Border _textEditorBorder;
        private readonly ExpressionTextBox _textExpressionBox;
        private readonly ComboBox _locateModeComboBox;
        private readonly FrameworkElement _relativePositionRow;
        private readonly ComboBox _relativePositionComboBox;
        private readonly FrameworkElement _bookmarkRow;
        private readonly ExpressionTextBox _bookmarkExpressionBox;
        private readonly FrameworkElement _keywordRow;
        private readonly ExpressionTextBox _keywordExpressionBox;

        private bool _isSyncingLocateMode;
        private bool _isSyncingRelativePosition;

        public InsertParagraphActivityDesigner()
        {
            var host = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(4)
            };

            _rootPanel = new Border
            {
                Padding = new Thickness(6, 5, 6, 5)
            };

            var body = new StackPanel { Orientation = Orientation.Vertical };
            Grid.SetIsSharedSizeScope(body, true);

            _wordFilePathExpressionBox = WordDesignerShared.CreateInExpressionTextBox("WordFilePath", typeof(string));
            body.Children.Add(WordDesignerShared.CreateRow("Word File Path", _wordFilePathExpressionBox, LabelColumn));

            _documentExpressionBox = WordDesignerShared.CreateInOutExpressionTextBox("Document", typeof(InteropWord.Document));
            body.Children.Add(WordDesignerShared.CreateRow("Document", _documentExpressionBox, LabelColumn, WordDesignerShared.RowSpacing));

            _textExpressionBox = WordDesignerShared.CreateInExpressionTextBox("Text", typeof(string));
            body.Children.Add(WordDesignerShared.CreateRow("Text", _textExpressionBox, LabelColumn, out _textEditorBorder, WordDesignerShared.RowSpacing));

            _locateModeComboBox = WordDesignerShared.BuildDescriptionComboBox<WordInsertLocateMode>();
            _locateModeComboBox.SelectionChanged += OnLocateModeSelectionChanged;
            body.Children.Add(WordDesignerShared.CreateRow("Locate Mode", _locateModeComboBox, LabelColumn, WordDesignerShared.RowSpacing));

            _relativePositionComboBox = WordDesignerShared.BuildDescriptionComboBox<WordInsertRelativePosition>();
            _relativePositionComboBox.SelectionChanged += OnRelativePositionSelectionChanged;
            _relativePositionRow = WordDesignerShared.CreateRow("Relative Position", _relativePositionComboBox, LabelColumn, WordDesignerShared.RowSpacing);
            body.Children.Add(_relativePositionRow);

            _bookmarkExpressionBox = WordDesignerShared.CreateInExpressionTextBox("BookmarkName", typeof(string));
            _bookmarkRow = WordDesignerShared.CreateRow("Bookmark Name", _bookmarkExpressionBox, LabelColumn, WordDesignerShared.RowSpacing);
            body.Children.Add(_bookmarkRow);

            _keywordExpressionBox = WordDesignerShared.CreateInExpressionTextBox("Keyword", typeof(string));
            _keywordRow = WordDesignerShared.CreateRow("Keyword", _keywordExpressionBox, LabelColumn, WordDesignerShared.RowSpacing);
            body.Children.Add(_keywordRow);

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
            SyncLocateMode(WordDesignerShared.ReadEnum(ModelItem, "LocateMode", WordInsertLocateMode.Keyword));
            SyncRelativePosition(WordDesignerShared.ReadEnum(ModelItem, "RelativePosition", WordInsertRelativePosition.After));
            RefreshLocateRows();
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "LocateMode", StringComparison.Ordinal))
            {
                SyncLocateMode(WordDesignerShared.ReadEnum(ModelItem, "LocateMode", WordInsertLocateMode.Keyword));
                RefreshLocateRows();
            }
            else if (string.Equals(e.PropertyName, "RelativePosition", StringComparison.Ordinal))
            {
                SyncRelativePosition(WordDesignerShared.ReadEnum(ModelItem, "RelativePosition", WordInsertRelativePosition.After));
            }

            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void OnLocateModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingLocateMode || ModelItem == null)
            {
                return;
            }

            var mode = WordDesignerShared.ReadSelectedEnum(_locateModeComboBox, WordInsertLocateMode.Keyword);
            ModelItem.Properties["LocateMode"].SetValue(mode);
            RefreshLocateRows();
            RefreshRequiredBorders();
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
            var mode = WordDesignerShared.ReadEnum(ModelItem, "LocateMode", WordInsertLocateMode.Keyword);
            _relativePositionRow.Visibility = mode == WordInsertLocateMode.DocumentStart
                ? Visibility.Collapsed
                : Visibility.Visible;
            _bookmarkRow.Visibility = mode == WordInsertLocateMode.Bookmark ? Visibility.Visible : Visibility.Collapsed;
            _keywordRow.Visibility = mode == WordInsertLocateMode.Keyword ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SyncLocateMode(WordInsertLocateMode mode)
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

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
            {
                return;
            }

            WordDesignerShared.SetRequiredBorder(
                _textEditorBorder,
                WordDesignerShared.IsArgumentFilled(ModelItem, "Text", _textExpressionBox));
        }
    }
}
