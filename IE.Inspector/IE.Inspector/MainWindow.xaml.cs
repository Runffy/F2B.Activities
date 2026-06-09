using System.Windows;
using System.Windows.Controls;
using IE.Inspector.Models;
using IE.Inspector.ViewModels;

namespace IE.Inspector
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

        private void DomTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_viewModel.IgnoreDomTreeSelectionChanges)
                return;

            if (e.NewValue is DomTreeNode node)
                _viewModel.SelectedDomNode = node;
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _viewModel.Cleanup();
            base.OnClosed(e);
        }
    }
}
