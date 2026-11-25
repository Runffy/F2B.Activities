using System;
using System.Threading;
using System.Windows.Forms;

namespace F2B.Forms.Engine
{
    /// <summary>
    /// Modal dialogs that do not require an active <see cref="Session.FormSession"/>.
    /// Shown on a dedicated STA thread; blocks the caller until closed.
    /// </summary>
    internal static class StandaloneFormDialogs
    {
        public static void ShowMessage(string message, string title)
        {
            RunOnSta(() =>
            {
                MessageBox.Show(
                    null,
                    message ?? string.Empty,
                    string.IsNullOrWhiteSpace(title) ? "Form" : title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            });
        }

        public static bool RequestConfirm(string message, string title)
        {
            bool confirmed = false;
            RunOnSta(() =>
            {
                DialogResult result = MessageBox.Show(
                    null,
                    message ?? string.Empty,
                    string.IsNullOrWhiteSpace(title) ? "Form" : title,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                confirmed = result == DialogResult.Yes;
            });
            return confirmed;
        }

        public static object ShowSelect(
            string title,
            string message,
            object options,
            string buttonName)
        {
            object selected = null;
            RunOnSta(() =>
            {
                using (var popup = new SelectPopupForm(
                    title,
                    message,
                    SelectPopupForm.ParseOptions(options),
                    string.IsNullOrWhiteSpace(buttonName) ? "OK" : buttonName))
                {
                    PrepareStandalone(popup);
                    popup.ShowDialog();
                    selected = popup.SelectedItem;
                }
            });
            return selected;
        }

        public static string ShowInput(
            string title,
            string message,
            bool isPassword,
            string buttonName)
        {
            string input = null;
            RunOnSta(() =>
            {
                using (var popup = new InputPopupForm(
                    title,
                    message,
                    isPassword,
                    string.IsNullOrWhiteSpace(buttonName) ? "OK" : buttonName))
                {
                    PrepareStandalone(popup);
                    popup.ShowDialog();
                    input = popup.InputText;
                }
            });
            return input;
        }

        private static void PrepareStandalone(Form popup)
        {
            popup.StartPosition = FormStartPosition.CenterScreen;
            popup.ShowInTaskbar = true;
            popup.TopMost = true;
        }

        private static void RunOnSta(Action action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            Exception captured = null;
            var done = new ManualResetEventSlim(false);

            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    captured = ex;
                }
                finally
                {
                    done.Set();
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            done.Wait();
            done.Dispose();

            if (captured != null)
            {
                throw new InvalidOperationException(
                    "Standalone form dialog failed: " + captured.Message,
                    captured);
            }
        }
    }
}
