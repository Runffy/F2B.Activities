using System.Windows;
using System.Windows.Controls;
using F2B.Browser.Chromium.Inspector.Models;
using F2B.Browser.Chromium.Inspector.ViewModels;

namespace F2B.Browser.Chromium.Inspector
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel = new MainViewModel();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel;
            _viewModel.Initialize(Dispatcher);
        }

        private void VisualTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_viewModel.SuppressVisualTreeSelection)
                return;

            if (e.NewValue is InspectorVisualTreeNode node)
                _viewModel.SelectedVisualNode = node;
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _viewModel.Cleanup();
            base.OnClosed(e);
        }
    }
}
