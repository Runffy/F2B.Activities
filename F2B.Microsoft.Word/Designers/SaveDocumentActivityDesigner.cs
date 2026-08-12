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
    public sealed class SaveDocumentActivityDesigner : ActivityDesigner
    {
        private const string LabelColumn = "WordSaveDocumentLabelColumn";

        private readonly Border _rootPanel;
        private readonly ComboBox _documentLabelComboBox;
        private bool _isSyncingLabel;

        public SaveDocumentActivityDesigner()
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
                "Document",
                WordDesignerShared.CreateInExpressionTextBox("Document", typeof(InteropWord.Document)),
                LabelColumn));

            body.Children.Add(WordDesignerShared.CreateRow(
                "Word File Path",
                WordDesignerShared.CreateInExpressionTextBox("WordFilePath", typeof(string)),
                LabelColumn,
                WordDesignerShared.RowSpacing));

            _documentLabelComboBox = WordDesignerShared.BuildDescriptionComboBox<WordDocumentLabel>();
            _documentLabelComboBox.SelectionChanged += OnDocumentLabelSelectionChanged;
            body.Children.Add(WordDesignerShared.CreateRow(
                "Document Label",
                _documentLabelComboBox,
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
            SyncDocumentLabel(WordDesignerShared.ReadEnum(ModelItem, "DocumentLabel", WordDocumentLabel.None));
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "DocumentLabel", StringComparison.Ordinal))
            {
                SyncDocumentLabel(WordDesignerShared.ReadEnum(ModelItem, "DocumentLabel", WordDocumentLabel.None));
            }
        }

        private void OnDocumentLabelSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingLabel || ModelItem == null)
            {
                return;
            }

            var label = WordDesignerShared.ReadSelectedEnum(_documentLabelComboBox, WordDocumentLabel.None);
            ModelItem.Properties["DocumentLabel"].SetValue(label);
        }

        private void SyncDocumentLabel(WordDocumentLabel label)
        {
            _isSyncingLabel = true;
            WordDesignerShared.SelectEnumItem(_documentLabelComboBox, label);
            _isSyncingLabel = false;
        }
    }
}
