using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using F2B.Browser.Chromium.Cdp;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace F2B.Browser.Chromium.Cdp.Launcher
{
    public partial class MainWindow : Window
    {
        private LauncherSettings _settings;
        private bool _suppressAutoFill;
        private bool _loading;

        public MainWindow()
        {
            // ComboBox SelectedIndex in XAML fires SelectionChanged during InitializeComponent,
            // before sibling named fields (e.g. ExecutablePathTextBox) are assigned.
            _loading = true;
            InitializeComponent();
            Loaded += MainWindow_OnLoaded;
            Closing += MainWindow_OnClosing;
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            _loading = true;
            _settings = SettingsStore.Load();
            ApplySettingsToUi(_settings, refreshExecutableFromBrowserType: string.IsNullOrWhiteSpace(_settings.ExecutablePath));
            RefreshHistoryList();
            UpdateEffectivePathPreview();
            _loading = false;
        }

        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            PersistCurrentFieldsToSettings();
            SettingsStore.Save(_settings);
        }

        private void BrowserTypeCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loading || _suppressAutoFill || ExecutablePathTextBox == null)
            {
                return;
            }

            AutoFillExecutablePath();
            UpdateEffectivePathPreview();
        }

        private void ConfigField_OnChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || EffectivePathTextBox == null)
            {
                return;
            }

            UpdateEffectivePathPreview();
        }

        private void BrowseExecutable_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select browser executable",
                Filter = "Browser executable|chrome.exe;msedge.exe|Executable (*.exe)|*.exe|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (!string.IsNullOrWhiteSpace(ExecutablePathTextBox.Text) && File.Exists(ExecutablePathTextBox.Text))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(ExecutablePathTextBox.Text);
                dialog.FileName = Path.GetFileName(ExecutablePathTextBox.Text);
            }

            if (dialog.ShowDialog(this) == true)
            {
                ExecutablePathTextBox.Text = dialog.FileName;
            }
        }

        private void BrowseUserDataRoot_OnClick(object sender, RoutedEventArgs e)
        {
            using (var dialog = new WinForms.FolderBrowserDialog())
            {
                dialog.Description = "Select browser data directory root";
                dialog.ShowNewFolderButton = true;
                if (!string.IsNullOrWhiteSpace(UserDataDirRootTextBox.Text) &&
                    Directory.Exists(UserDataDirRootTextBox.Text))
                {
                    dialog.SelectedPath = UserDataDirRootTextBox.Text;
                }

                if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                {
                    UserDataDirRootTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void HistoryListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var entry = HistoryListBox.SelectedItem as LaunchHistoryEntry;
            if (entry == null)
            {
                return;
            }

            _suppressAutoFill = true;
            try
            {
                SelectBrowserType(entry.BrowserType);
                PortTextBox.Text = entry.Port.ToString();
                UserDataDirRootTextBox.Text = entry.UserDataDirRoot ?? string.Empty;
                ExecutablePathTextBox.Text = entry.ExecutablePath ?? string.Empty;
                if (string.IsNullOrWhiteSpace(ExecutablePathTextBox.Text))
                {
                    AutoFillExecutablePath();
                }
            }
            finally
            {
                _suppressAutoFill = false;
            }

            UpdateEffectivePathPreview();
        }

        private void Run_OnClick(object sender, RoutedEventArgs e)
        {
            string browserType;
            string executablePath;
            string userDataRoot;
            int port;
            string effectiveUserDataDir;

            try
            {
                browserType = GetSelectedBrowserType();
                executablePath = (ExecutablePathTextBox.Text ?? string.Empty).Trim();
                userDataRoot = (UserDataDirRootTextBox.Text ?? string.Empty).Trim();

                if (string.IsNullOrWhiteSpace(executablePath))
                {
                    throw new InvalidOperationException("Browser executable path is required.");
                }

                if (!File.Exists(executablePath))
                {
                    throw new InvalidOperationException("Browser executable was not found: " + executablePath);
                }

                if (!int.TryParse((PortTextBox.Text ?? string.Empty).Trim(), out port) || port <= 0 || port > 65535)
                {
                    throw new InvalidOperationException("Port must be a number between 1 and 65535.");
                }

                if (string.IsNullOrWhiteSpace(userDataRoot))
                {
                    throw new InvalidOperationException("Browser data directory root is required.");
                }

                effectiveUserDataDir = UserDataPathBuilder.BuildEffectivePath(userDataRoot, browserType, port);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Invalid settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RunButton.IsEnabled = false;
            try
            {
                var options = new BrowserOpenOptions
                {
                    ExecutablePath = executablePath,
                    Port = port,
                    UserDataDir = effectiveUserDataDir,
                    Force = true
                };

                var browser = ChromiumBrowser.OpenBrowser(options);
                var entry = new LaunchHistoryEntry
                {
                    BrowserType = browserType,
                    ExecutablePath = executablePath,
                    Port = browser.Port,
                    UserDataDirRoot = userDataRoot,
                    EffectiveUserDataDir = browser.UserDataDir,
                    UsedAtUtc = DateTime.UtcNow
                };

                PersistCurrentFieldsToSettings();
                SettingsStore.RememberLaunch(_settings, entry);
                RefreshHistoryList();

                var mode = browser.AttachedToExisting ? "Attached to existing browser" : "Opened new browser";
                MessageBox.Show(
                    this,
                    string.Format(
                        "{0}.\r\n\r\nBrowser: {1}\r\nPort: {2}\r\nUser data dir:\r\n{3}",
                        mode,
                        browser.BrowserName,
                        browser.Port,
                        browser.UserDataDir),
                    "CDP Browser Launcher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (BrowserException ex)
            {
                MessageBox.Show(this, ex.Message, "Failed to open browser", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RunButton.IsEnabled = true;
            }
        }

        private void ApplySettingsToUi(LauncherSettings settings, bool refreshExecutableFromBrowserType)
        {
            SelectBrowserType(settings.BrowserType);
            PortTextBox.Text = settings.Port > 0 ? settings.Port.ToString() : "9222";
            UserDataDirRootTextBox.Text = settings.UserDataDirRoot ?? @"C:\Temp";

            if (refreshExecutableFromBrowserType || string.IsNullOrWhiteSpace(settings.ExecutablePath))
            {
                AutoFillExecutablePath();
            }
            else
            {
                ExecutablePathTextBox.Text = settings.ExecutablePath;
            }
        }

        private void PersistCurrentFieldsToSettings()
        {
            if (_settings == null)
            {
                _settings = LauncherSettings.CreateDefault();
            }

            _settings.BrowserType = GetSelectedBrowserType();
            _settings.ExecutablePath = (ExecutablePathTextBox.Text ?? string.Empty).Trim();
            _settings.UserDataDirRoot = (UserDataDirRootTextBox.Text ?? string.Empty).Trim();

            int port;
            if (int.TryParse((PortTextBox.Text ?? string.Empty).Trim(), out port) && port > 0)
            {
                _settings.Port = port;
            }
        }

        private void RefreshHistoryList()
        {
            HistoryListBox.ItemsSource = null;
            HistoryListBox.ItemsSource = _settings != null ? _settings.History : null;
        }

        private void AutoFillExecutablePath()
        {
            if (ExecutablePathTextBox == null)
            {
                return;
            }

            var resolved = BrowserExecutableLocator.TryResolve(GetSelectedBrowserType());
            ExecutablePathTextBox.Text = resolved ?? string.Empty;
        }

        private void UpdateEffectivePathPreview()
        {
            if (EffectivePathTextBox == null || PortTextBox == null || UserDataDirRootTextBox == null)
            {
                return;
            }

            try
            {
                int port;
                if (!int.TryParse((PortTextBox.Text ?? string.Empty).Trim(), out port) || port <= 0 || port > 65535)
                {
                    EffectivePathTextBox.Text = "(enter a valid port to preview path)";
                    return;
                }

                var root = (UserDataDirRootTextBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(root))
                {
                    EffectivePathTextBox.Text = "(enter a browser data directory root)";
                    return;
                }

                EffectivePathTextBox.Text = UserDataPathBuilder.BuildEffectivePath(root, GetSelectedBrowserType(), port);
            }
            catch (Exception ex)
            {
                EffectivePathTextBox.Text = "(invalid path: " + ex.Message + ")";
            }
        }

        private string GetSelectedBrowserType()
        {
            var item = BrowserTypeCombo.SelectedItem as ComboBoxItem;
            var content = item != null ? Convert.ToString(item.Content) : null;
            return string.IsNullOrWhiteSpace(content) ? "Chrome" : content.Trim();
        }

        private void SelectBrowserType(string browserType)
        {
            var folder = UserDataPathBuilder.NormalizeBrowserFolderName(browserType);
            BrowserTypeCombo.SelectedIndex = string.Equals(folder, "MsEdge", StringComparison.Ordinal) ? 1 : 0;
        }
    }
}
