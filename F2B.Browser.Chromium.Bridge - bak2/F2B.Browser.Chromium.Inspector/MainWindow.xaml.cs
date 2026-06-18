using System.Windows;
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

        protected override void OnClosed(System.EventArgs e)
        {
            _viewModel.Cleanup();
            base.OnClosed(e);
        }
    }
}
