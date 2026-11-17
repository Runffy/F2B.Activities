using System;
using System.Threading;

namespace F2B.Browser.Chromium.Cdp.Activities
{
    /// <summary>
    /// STA-safe access to the Windows clipboard (WPF <see cref="System.Windows.Clipboard"/>).
    /// </summary>
    internal static class CdpClipboardHelper
    {
        internal static void SetText(string text)
        {
            RunSta(() =>
            {
                System.Windows.Clipboard.Clear();
                System.Windows.Clipboard.SetText(text ?? string.Empty);
            });
        }

        internal static string GetText()
        {
            string result = null;
            RunSta(() =>
            {
                result = System.Windows.Clipboard.ContainsText()
                    ? System.Windows.Clipboard.GetText()
                    : string.Empty;
            });
            return result ?? string.Empty;
        }

        private static void RunSta(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                action();
                return;
            }

            Exception error = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error != null)
            {
                throw new InvalidOperationException("Clipboard operation failed.", error);
            }
        }
    }
}
