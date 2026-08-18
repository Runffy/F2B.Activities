using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Global designer hotkeys on the OpenRPA main window:
    /// Ctrl+S save, Ctrl+T fault-path go-to, Ctrl+P activity palette,
    /// Ctrl+F find in current workflow, Ctrl+Shift+F find in all projects.
    /// </summary>
    internal static class DesignerHotkeys
    {
        private static readonly object Gate = new object();
        private static Window _window;
        private static DispatcherTimer _retryTimer;
        private static int _attempts;
        private static bool _attached;

        public static void Start()
        {
            Application app = Application.Current;
            if (app == null || app.Dispatcher == null)
            {
                return;
            }

            app.Dispatcher.BeginInvoke(new Action(TryAttach), DispatcherPriority.ApplicationIdle);
            if (_retryTimer == null)
            {
                _retryTimer = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher)
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                _retryTimer.Tick += (s, e) =>
                {
                    if (_attached || _attempts > 90)
                    {
                        _retryTimer.Stop();
                        return;
                    }

                    TryAttach();
                };
                _retryTimer.Start();
            }
        }

        private static void TryAttach()
        {
            lock (Gate)
            {
                if (_attached)
                {
                    return;
                }

                _attempts++;
                Window main = PluginContext.MainWindow;
                if (main == null)
                {
                    return;
                }

                if (_window != null)
                {
                    _window.PreviewKeyDown -= OnPreviewKeyDown;
                    _window.PreviewMouseDown -= OnPreviewMouseDown;
                }

                _window = main;
                _window.PreviewKeyDown += OnPreviewKeyDown;
                _window.PreviewMouseDown += OnPreviewMouseDown;
                _attached = true;
                if (_retryTimer != null)
                {
                    _retryTimer.Stop();
                }
            }
        }

        private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (e.ChangedButton != MouseButton.Left || e.ClickCount != 1)
                {
                    return;
                }

                if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                {
                    return;
                }

                if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                {
                    return;
                }

                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    return;
                }

                if (PluginContext.ResolveDesigner() == null)
                {
                    return;
                }

                var source = e.OriginalSource as DependencyObject;
                if (!ActivityInsertService.IsSequenceSpacer(source))
                {
                    return;
                }

                e.Handled = true;
                ActivityPalettePopup.ShowAtSpacer(source);
            }
            catch
            {
            }
        }

        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
                if (!ctrl)
                {
                    return;
                }

                // Ignore when Alt is also pressed.
                if ((Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
                {
                    return;
                }

                Key key = e.Key == Key.System ? e.SystemKey : e.Key;
                bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

                if (key == Key.S && !shift)
                {
                    e.Handled = true;
                    var saveTask = PluginContext.SaveCurrentWorkflowAsync();
                    return;
                }

                if (key == Key.F)
                {
                    if (shift)
                    {
                        // Global find: works even on Open Project (no designer required).
                        e.Handled = true;
                        PluginContext.RunOnUi(GlobalWorkflowFindPopup.Show);
                        return;
                    }

                    if (PluginContext.ResolveDesigner() == null)
                    {
                        return;
                    }

                    // Override OpenRPA MainWindow Ctrl+F (SearchBox focus).
                    e.Handled = true;
                    GlobalWorkflowFindPopup.Hide();
                    PluginContext.RunOnUi(WorkflowFindPopup.Show);
                    return;
                }

                if (key == Key.T && !shift)
                {
                    e.Handled = true;
                    PluginContext.RunOnUi(() =>
                    {
                        var win = new FaultPathSearchWindow();
                        win.Show();
                    });
                    return;
                }

                if (key == Key.P && !shift)
                {
                    if (PluginContext.ResolveDesigner() == null)
                    {
                        return;
                    }

                    e.Handled = true;
                    WorkflowFindPopup.Hide();
                    GlobalWorkflowFindPopup.Hide();
                    PluginContext.RunOnUi(ActivityPalettePopup.Show);
                }
            }
            catch
            {
            }
        }
    }
}
