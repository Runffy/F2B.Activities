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
    public sealed class ReplaceKeywordActivityDesigner : ActivityDesigner
    {
        private const string LabelColumn = "WordReplaceKeywordLabelColumn";

        private readonly Border _rootPanel;
        private readonly Border _keywordEditorBorder;
        private readonly ExpressionTextBox _keywordExpressionBox;

        public ReplaceKeywordActivityDesigner()
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

            _keywordExpressionBox = WordDesignerShared.CreateInExpressionTextBox("Keyword", typeof(string));
            body.Children.Add(WordDesignerShared.CreateRow(
                "Keyword",
                _keywordExpressionBox,
                LabelColumn,
                out _keywordEditorBorder,
                WordDesignerShared.RowSpacing));

            body.Children.Add(WordDesignerShared.CreateRow(
                "New Text",
                WordDesignerShared.CreateInExpressionTextBox("NewText", typeof(string)),
                LabelColumn,
                WordDesignerShared.RowSpacing));

            body.Children.Add(WordDesignerShared.CreateRow(
                "Count",
                WordDesignerShared.CreateInExpressionTextBox("Count", typeof(int)),
                LabelColumn,
                WordDesignerShared.RowSpacing));

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
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
            {
                return;
            }

            WordDesignerShared.SetRequiredBorder(
                _keywordEditorBorder,
                WordDesignerShared.IsArgumentFilled(ModelItem, "Keyword", _keywordExpressionBox));
        }
    }
}
