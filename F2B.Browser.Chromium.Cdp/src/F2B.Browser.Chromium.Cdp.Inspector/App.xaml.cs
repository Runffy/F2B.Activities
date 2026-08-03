using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using F2B.Browser.Chromium.Cdp.Inspector.Services;

namespace F2B.Browser.Chromium.Cdp.Inspector
{
    public partial class App : Application
    {
        public App()
        {
            DesignerIntegrationStub.ParseStartupArgs(Environment.GetCommandLineArgs());
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;

            if (MainWindow?.DataContext is ViewModels.MainViewModel viewModel)
                viewModel.ReportRuntimeError(e.Exception);
            else
                MessageBox.Show(
                    e.Exception?.Message ?? "Unknown error",
                    "F2B Chromium CDP Inspector",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
        }
    }
}
