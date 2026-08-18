using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Closes designer popups when the main window is minimized or loses OS focus
    /// to another application. Keeps them open when focus moves into the popup itself
    /// (WPF Popup is a separate HWND and would otherwise look like a deactivate).
    /// </summary>
    internal static class DesignerPopupLifetime
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public static void Hook(Window window, Action hide)
        {
            if (window == null || hide == null)
            {
                return;
            }

            window.Deactivated -= OnDeactivated;
            window.StateChanged -= OnStateChanged;
            window.Deactivated += OnDeactivated;
            window.StateChanged += OnStateChanged;

            // Store hide via weak-less static pair of actions — both popups call Hook separately.
            // Use multicast: register per-popup via event table keyed by window is overkill;
            // callers pass Hide which we invoke for all registered.
            RegisterHide(window, hide);
        }

        public static void Unhook(Window window, Action hide)
        {
            if (window == null)
            {
                return;
            }

            UnregisterHide(window, hide);
            if (!HasHideHandlers(window))
            {
                window.Deactivated -= OnDeactivated;
                window.StateChanged -= OnStateChanged;
            }
        }

        private static Window _trackedWindow;
        private static Action _hideAll;

        private static void RegisterHide(Window window, Action hide)
        {
            if (!ReferenceEquals(_trackedWindow, window))
            {
                _trackedWindow = window;
                _hideAll = null;
            }

            _hideAll -= hide;
            _hideAll += hide;
        }

        private static void UnregisterHide(Window window, Action hide)
        {
            if (!ReferenceEquals(_trackedWindow, window))
            {
                return;
            }

            _hideAll -= hide;
        }

        private static bool HasHideHandlers(Window window)
        {
            return ReferenceEquals(_trackedWindow, window) && _hideAll != null;
        }

        private static void OnStateChanged(object sender, EventArgs e)
        {
            var window = sender as Window;
            if (window == null)
            {
                return;
            }

            if (window.WindowState == WindowState.Minimized)
            {
                InvokeHide();
            }
        }

        private static void OnDeactivated(object sender, EventArgs e)
        {
            var window = sender as Window;
            if (window == null)
            {
                return;
            }

            // Defer: focus may be moving into our Popup HWND in the same click.
            window.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (window.WindowState == WindowState.Minimized)
                {
                    InvokeHide();
                    return;
                }

                if (window.IsActive)
                {
                    return;
                }

                IntPtr foreground = GetForegroundWindow();
                if (foreground == IntPtr.Zero)
                {
                    InvokeHide();
                    return;
                }

                IntPtr mainHwnd = new WindowInteropHelper(window).Handle;
                if (foreground == mainHwnd)
                {
                    return;
                }

                if (IsOwnedByOpenRpaPopups(foreground))
                {
                    return;
                }

                InvokeHide();
            }), DispatcherPriority.Input);
        }

        private static bool IsOwnedByOpenRpaPopups(IntPtr hwnd)
        {
            return IsPopupHwnd(ActivityPalettePopup.CurrentPopup, hwnd)
                   || IsPopupHwnd(WorkflowFindPopup.CurrentPopup, hwnd)
                   || IsPopupHwnd(GlobalWorkflowFindPopup.CurrentPopup, hwnd);
        }

        private static bool IsPopupHwnd(Popup popup, IntPtr hwnd)
        {
            if (popup == null || !popup.IsOpen || popup.Child == null)
            {
                return false;
            }

            try
            {
                HwndSource source = PresentationSource.FromVisual(popup.Child) as HwndSource;
                return source != null && source.Handle == hwnd;
            }
            catch
            {
                return false;
            }
        }

        private static void InvokeHide()
        {
            Action hide = _hideAll;
            if (hide != null)
            {
                hide();
            }
        }
    }
}
