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
    public sealed class SaveAsActivityDesigner : ActivityDesigner
    {
        private const string LabelColumn = "WordSaveAsLabelColumn";

        private readonly Border _rootPanel;
        private readonly Border _outputPathEditorBorder;
        private readonly ExpressionTextBox _outputPathExpressionBox;
        private readonly ComboBox _formatComboBox;
        private bool _isSyncingFormat;

        public SaveAsActivityDesigner()
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

            _outputPathExpressionBox = WordDesignerShared.CreateInExpressionTextBox("OutputPath", typeof(string));
            body.Children.Add(WordDesignerShared.CreateRow(
                "Output Path",
                _outputPathExpressionBox,
                LabelColumn,
                out _outputPathEditorBorder,
                WordDesignerShared.RowSpacing));

            _formatComboBox = WordDesignerShared.BuildDescriptionComboBox<WordSaveAsFormat>();
            _formatComboBox.SelectionChanged += OnFormatSelectionChanged;
            body.Children.Add(WordDesignerShared.CreateRow("Format", _formatComboBox, LabelColumn, WordDesignerShared.RowSpacing));

            body.Children.Add(WordDesignerShared.CreateRow(
                "Overwrite",
                WordDesignerShared.CreateInExpressionTextBox("Overwrite", typeof(bool)),
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
            SyncFormat(WordDesignerShared.ReadEnum(ModelItem, "Format", WordSaveAsFormat.Docx));
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "Format", StringComparison.Ordinal))
            {
                SyncFormat(WordDesignerShared.ReadEnum(ModelItem, "Format", WordSaveAsFormat.Docx));
            }

            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void OnFormatSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingFormat || ModelItem == null)
            {
                return;
            }

            var format = WordDesignerShared.ReadSelectedEnum(_formatComboBox, WordSaveAsFormat.Docx);
            ModelItem.Properties["Format"].SetValue(format);
        }

        private void SyncFormat(WordSaveAsFormat format)
        {
            _isSyncingFormat = true;
            WordDesignerShared.SelectEnumItem(_formatComboBox, format);
            _isSyncingFormat = false;
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
            {
                return;
            }

            WordDesignerShared.SetRequiredBorder(
                _outputPathEditorBorder,
                WordDesignerShared.IsArgumentFilled(ModelItem, "OutputPath", _outputPathExpressionBox));
        }
    }
}
