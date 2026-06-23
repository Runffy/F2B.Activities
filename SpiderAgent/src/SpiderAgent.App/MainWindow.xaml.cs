using System.Windows;
using SpiderAgent.App.ViewModels;

namespace SpiderAgent.App;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
