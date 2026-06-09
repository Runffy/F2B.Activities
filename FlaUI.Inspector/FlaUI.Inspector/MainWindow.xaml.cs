using System.Windows;
using System.Windows.Controls;
using FlaUI.Inspector.Models;
using FlaUI.Inspector.ViewModels;

namespace FlaUI.Inspector
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
            if (_viewModel.IgnoreVisualTreeSelectionChanges)
                return;

            if (e.NewValue is VisualTreeNode node)
                _viewModel.SelectedVisualNode = node;
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _viewModel.Cleanup();
            base.OnClosed(e);
        }
    }
}
