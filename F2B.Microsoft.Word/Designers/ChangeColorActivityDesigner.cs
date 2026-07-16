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
    public sealed class ChangeColorActivityDesigner : ActivityDesigner
    {
        private const string LabelColumn = "WordChangeColorLabelColumn";

        private readonly Border _rootPanel;
        private readonly Border _keywordEditorBorder;
        private readonly ExpressionTextBox _keywordExpressionBox;
        private readonly ComboBox _colorModeComboBox;
        private readonly FrameworkElement _colorNameRow;
        private readonly ExpressionTextBox _colorNameExpressionBox;
        private readonly FrameworkElement _rgbRow;
        private readonly ExpressionTextBox _rgbExpressionBox;

        private bool _isSyncingColorMode;

        public ChangeColorActivityDesigner()
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
                "Apply To Whole Paragraph",
                WordDesignerShared.CreateInExpressionTextBox("ApplyToWholeParagraph", typeof(bool)),
                LabelColumn,
                WordDesignerShared.RowSpacing));

            body.Children.Add(WordDesignerShared.CreateRow(
                "Count",
                WordDesignerShared.CreateInExpressionTextBox("Count", typeof(int)),
                LabelColumn,
                WordDesignerShared.RowSpacing));

            _colorModeComboBox = WordDesignerShared.BuildDescriptionComboBox<WordColorMode>();
            _colorModeComboBox.SelectionChanged += OnColorModeSelectionChanged;
            body.Children.Add(WordDesignerShared.CreateRow("Color Mode", _colorModeComboBox, LabelColumn, WordDesignerShared.RowSpacing));

            _colorNameExpressionBox = WordDesignerShared.CreateInExpressionTextBox("ColorName", typeof(string));
            _colorNameRow = WordDesignerShared.CreateRow("Color Name", _colorNameExpressionBox, LabelColumn, WordDesignerShared.RowSpacing);
            body.Children.Add(_colorNameRow);

            _rgbExpressionBox = WordDesignerShared.CreateInExpressionTextBox("Rgb", typeof(string));
            _rgbRow = WordDesignerShared.CreateRow("RGB", _rgbExpressionBox, LabelColumn, WordDesignerShared.RowSpacing);
            body.Children.Add(_rgbRow);

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
            SyncColorMode(WordDesignerShared.ReadEnum(ModelItem, "ColorMode", WordColorMode.Named));
            RefreshColorRows();
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "ColorMode", StringComparison.Ordinal))
            {
                SyncColorMode(WordDesignerShared.ReadEnum(ModelItem, "ColorMode", WordColorMode.Named));
                RefreshColorRows();
            }

            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void OnColorModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingColorMode || ModelItem == null)
            {
                return;
            }

            var mode = WordDesignerShared.ReadSelectedEnum(_colorModeComboBox, WordColorMode.Named);
            ModelItem.Properties["ColorMode"].SetValue(mode);
            RefreshColorRows();
            RefreshRequiredBorders();
        }

        private void RefreshColorRows()
        {
            var mode = WordDesignerShared.ReadEnum(ModelItem, "ColorMode", WordColorMode.Named);
            _colorNameRow.Visibility = mode == WordColorMode.Named ? Visibility.Visible : Visibility.Collapsed;
            _rgbRow.Visibility = mode == WordColorMode.Rgb ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SyncColorMode(WordColorMode mode)
        {
            _isSyncingColorMode = true;
            WordDesignerShared.SelectEnumItem(_colorModeComboBox, mode);
            _isSyncingColorMode = false;
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
