using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace FlaUI.Demo.App
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SetStatus(string message)
        {
            lblStatus.Text = message;
        }

        private void BtnClick_OnClick(object sender, RoutedEventArgs e)
        {
            SetStatus("Single click detected");
        }

        private void BtnDoubleClick_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            SetStatus("Double click detected");
        }

        private void ChkAgree_OnChanged(object sender, RoutedEventArgs e)
        {
            SetStatus(chkAgree.IsChecked == true ? "Checkbox checked" : "Checkbox unchecked");
        }

        private void BtnScrollTarget_OnClick(object sender, RoutedEventArgs e)
        {
            SetStatus("Scroll target clicked");
        }

        private void BtnReset_OnClick(object sender, RoutedEventArgs e)
        {
            chkAgree.IsChecked = false;
            txtName.Text = "OpenRPA";
            cboCity.SelectedIndex = 0;
            scrollMain.ScrollToTop();
            SetStatus("Reset");
        }

        private void BtnClose_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
