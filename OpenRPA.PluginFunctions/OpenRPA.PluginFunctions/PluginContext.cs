using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Shared access to the OpenRPA client and current designer helpers.
    /// </summary>
    internal static class PluginContext
    {
        public static IOpenRPAClient Client { get; private set; }

        public static void SetClient(IOpenRPAClient client)
        {
            Client = client;
        }

        public static IDesigner ResolveDesigner()
        {
            if (Client == null)
            {
                return null;
            }

            if (Client.CurrentDesigner != null)
            {
                return Client.CurrentDesigner;
            }

            if (Client.Window != null && Client.Window.Designer != null)
            {
                return Client.Window.Designer;
            }

            IDesigner[] designers = Client.Designers;
            if (designers == null)
            {
                return null;
            }

            return designers.FirstOrDefault(d => d != null && d.IsSelected)
                   ?? designers.FirstOrDefault(d => d != null);
        }

        public static Window MainWindow
        {
            get
            {
                try
                {
                    return Application.Current != null ? Application.Current.MainWindow : null;
                }
                catch
                {
                    return null;
                }
            }
        }

        public static void RunOnUi(Action action)
        {
            if (action == null)
            {
                return;
            }

            Application app = Application.Current;
            if (app != null && app.Dispatcher != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
                return;
            }

            action();
        }

        public static async Task SaveCurrentWorkflowAsync()
        {
            IDesigner designer = ResolveDesigner();
            if (designer == null)
            {
                return;
            }

            try
            {
                Application app = Application.Current;
                if (app != null && app.Dispatcher != null && !app.Dispatcher.CheckAccess())
                {
                    await app.Dispatcher.InvokeAsync(async () =>
                    {
                        await designer.SaveAsync().ConfigureAwait(true);
                    }).Task.Unwrap().ConfigureAwait(false);
                }
                else
                {
                    await designer.SaveAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Save Workflow", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
