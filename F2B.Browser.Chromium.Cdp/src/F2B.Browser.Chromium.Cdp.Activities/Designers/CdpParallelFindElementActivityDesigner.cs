using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.Activities.Presentation.View;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.OpenRpa.Design;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    public sealed class CdpParallelFindElementActivityDesigner : ActivityDesigner
    {
        private const string LabelColumn = "CdpParallelFindLabelColumn";

        private readonly Border _rootPanel;
        private readonly StackPanel _body;
        private readonly FrameworkElement _parentObjectRow;
        private readonly FrameworkElement _selectorsRow;
        private readonly Border _parentObjectEditorBorder;
        private readonly Border _selectorsEditorBorder;
        private readonly ExpressionTextBox _parentObjectExpressionBox;
        private readonly ExpressionTextBox _selectorsExpressionBox;

        public CdpParallelFindElementActivityDesigner()
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

            _body = new StackPanel { Orientation = Orientation.Vertical };
            Grid.SetIsSharedSizeScope(_body, true);

            _parentObjectExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("ParentObject", typeof(CdpBase));
            _parentObjectRow = CdpDesignerShared.CreateRow(
                "Parent Object",
                _parentObjectExpressionBox,
                LabelColumn,
                out _parentObjectEditorBorder);

            _selectorsExpressionBox = CdpDesignerShared.CreateInExpressionTextBox("Selectors", typeof(string[]));
            _selectorsRow = SelectorDesignerSupport.CreateSelectorsRow(
                "Selectors",
                _selectorsExpressionBox,
                "Selectors",
                () => ModelItem,
                LabelColumn,
                CdpDesignerShared.EditorMinWidth,
                out _selectorsEditorBorder,
                CdpDesignerShared.RowSpacing);

            _body.Children.Add(_parentObjectRow);
            _body.Children.Add(_selectorsRow);

            _rootPanel.Child = _body;
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

            CdpDesignerShared.BindExpressionOwner(_rootPanel, ModelItem);
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
            RefreshRequiredBorders();
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                RefreshRequiredBorders();
            }), DispatcherPriority.Background);
        }

        private void RefreshRequiredBorders()
        {
            if (ModelItem == null)
            {
                return;
            }

            CdpDesignerShared.SetRequiredBorder(
                _parentObjectEditorBorder,
                false,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "ParentObject", _parentObjectExpressionBox));
            CdpDesignerShared.SetRequiredBorder(
                _selectorsEditorBorder,
                true,
                CdpDesignerShared.IsArgumentFilled(ModelItem, "Selectors", _selectorsExpressionBox));
        }
    }
}
