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
    public sealed class AppendImageActivityDesigner : ActivityDesigner
    {
        private const string LabelColumn = "WordAppendImageLabelColumn";

        private readonly Border _rootPanel;
        private readonly FrameworkElement _wordFilePathRow;
        private readonly Border _wordFilePathEditorBorder;
        private readonly ExpressionTextBox _wordFilePathExpressionBox;
        private readonly FrameworkElement _documentRow;
        private readonly ExpressionTextBox _documentExpressionBox;
        private readonly FrameworkElement _imagePathRow;
        private readonly Border _imagePathEditorBorder;
        private readonly ExpressionTextBox _imagePathExpressionBox;
        private readonly FrameworkElement _sizeModeRow;
        private readonly ComboBox _sizeModeComboBox;
        private readonly FrameworkElement _widthRow;
        private readonly Border _widthEditorBorder;
        private readonly ExpressionTextBox _widthExpressionBox;
        private readonly FrameworkElement _heightRow;
        private readonly Border _heightEditorBorder;
        private readonly ExpressionTextBox _heightExpressionBox;
        private readonly FrameworkElement _unitRow;
        private readonly ComboBox _unitComboBox;

        private bool _isSyncingSizeMode;
        private bool _isSyncingUnit;

        public AppendImageActivityDesigner()
        {
            var host = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(4)
            };

            _rootPanel = new Border
            {
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 5, 6, 5)
            };

            var body = new StackPanel { Orientation = Orientation.Vertical };
            Grid.SetIsSharedSizeScope(body, true);

            _wordFilePathExpressionBox = WordDesignerShared.CreateInExpressionTextBox("WordFilePath", typeof(string));
            _wordFilePathRow = WordDesignerShared.CreateRow("Word File Path", _wordFilePathExpressionBox, LabelColumn, out _wordFilePathEditorBorder);
            body.Children.Add(_wordFilePathRow);

            _documentExpressionBox = WordDesignerShared.CreateInOutExpressionTextBox("Document", typeof(InteropWord.Document));
            _documentRow = WordDesignerShared.CreateRow("Document", _documentExpressionBox, LabelColumn, WordDesignerShared.RowSpacing);
            body.Children.Add(_documentRow);

            _imagePathExpressionBox = WordDesignerShared.CreateInExpressionTextBox("ImagePath", typeof(string));
            _imagePathRow = WordDesignerShared.CreateRow("Image Path", _imagePathExpressionBox, LabelColumn, out _imagePathEditorBorder, WordDesignerShared.RowSpacing);
            body.Children.Add(_imagePathRow);

            _sizeModeComboBox = WordDesignerShared.BuildDescriptionComboBox<WordImageSizeMode>();
            _sizeModeComboBox.SelectionChanged += OnSizeModeSelectionChanged;
            _sizeModeRow = WordDesignerShared.CreateRow("Size Mode", _sizeModeComboBox, LabelColumn, WordDesignerShared.RowSpacing);
            body.Children.Add(_sizeModeRow);

            _widthExpressionBox = WordDesignerShared.CreateInExpressionTextBox("Width", typeof(double));
            _widthRow = WordDesignerShared.CreateRow("Width", _widthExpressionBox, LabelColumn, out _widthEditorBorder, WordDesignerShared.RowSpacing);
            body.Children.Add(_widthRow);

            _heightExpressionBox = WordDesignerShared.CreateInExpressionTextBox("Height", typeof(double));
            _heightRow = WordDesignerShared.CreateRow("Height", _heightExpressionBox, LabelColumn, out _heightEditorBorder, WordDesignerShared.RowSpacing);
            body.Children.Add(_heightRow);

            _unitComboBox = WordDesignerShared.BuildDescriptionComboBox<WordImageUnit>();
            _unitComboBox.SelectionChanged += OnUnitSelectionChanged;
            _unitRow = WordDesignerShared.CreateRow("Unit", _unitComboBox, LabelColumn, WordDesignerShared.RowSpacing);
            body.Children.Add(_unitRow);

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
            SyncSizeModeCombo(WordDesignerShared.ReadEnum(ModelItem, "SizeMode", WordImageSizeMode.RegularSize));
            SyncUnitCombo(WordDesignerShared.ReadEnum(ModelItem, "Unit", WordImageUnit.Cm));
            RefreshCustomRows();
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "SizeMode", StringComparison.Ordinal))
            {
                SyncSizeModeCombo(WordDesignerShared.ReadEnum(ModelItem, "SizeMode", WordImageSizeMode.RegularSize));
                RefreshCustomRows();
            }
            else if (string.Equals(e.PropertyName, "Unit", StringComparison.Ordinal))
            {
                SyncUnitCombo(WordDesignerShared.ReadEnum(ModelItem, "Unit", WordImageUnit.Cm));
            }

            Dispatcher.BeginInvoke(new Action(RefreshRequiredBorders), DispatcherPriority.Background);
        }

        private void OnSizeModeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingSizeMode || ModelItem == null)
            {
                return;
            }

            var sizeMode = WordDesignerShared.ReadSelectedEnum(_sizeModeComboBox, WordImageSizeMode.RegularSize);
            ModelItem.Properties["SizeMode"].SetValue(sizeMode);
            RefreshCustomRows();
            RefreshRequiredBorders();
        }

        private void OnUnitSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingUnit || ModelItem == null)
            {
                return;
            }

            var unit = WordDesignerShared.ReadSelectedEnum(_unitComboBox, WordImageUnit.Cm);
            ModelItem.Properties["Unit"].SetValue(unit);
        }

        private void RefreshCustomRows()
        {
            var isCustom = WordDesignerShared.ReadEnum(ModelItem, "SizeMode", WordImageSizeMode.RegularSize) == WordImageSizeMode.Custom;
            var visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
            _widthRow.Visibility = visibility;
            _heightRow.Visibility = visibility;
            _unitRow.Visibility = visibility;
        }

        private void SyncSizeModeCombo(WordImageSizeMode sizeMode)
        {
            _isSyncingSizeMode = true;
            WordDesignerShared.SelectEnumItem(_sizeModeComboBox, sizeMode);
            _isSyncingSizeMode = false;
        }

        private void SyncUnitCombo(WordImageUnit unit)
        {
            _isSyncingUnit = true;
            WordDesignerShared.SelectEnumItem(_unitComboBox, unit);
            _isSyncingUnit = false;
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
            {
                return;
            }

            WordDesignerShared.SetRequiredBorder(
                _imagePathEditorBorder,
                WordDesignerShared.IsArgumentFilled(ModelItem, "ImagePath", _imagePathExpressionBox));

            var isCustom = WordDesignerShared.ReadEnum(ModelItem, "SizeMode", WordImageSizeMode.RegularSize) == WordImageSizeMode.Custom;
            WordDesignerShared.SetRequiredBorder(
                _widthEditorBorder,
                !isCustom || WordDesignerShared.IsArgumentFilled(ModelItem, "Width", _widthExpressionBox));
            WordDesignerShared.SetRequiredBorder(
                _heightEditorBorder,
                !isCustom || WordDesignerShared.IsArgumentFilled(ModelItem, "Height", _heightExpressionBox));
        }
    }
}
