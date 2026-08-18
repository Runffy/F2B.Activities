using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Before a root workflow run (Play / F5): clear Output, save every dirty designer,
    /// log per-workflow save failures, and confirm if any save failed.
    /// Nested Invoke Workflow (ident&gt;0 or caller set) is left alone.
    /// </summary>
    internal static class SaveBeforeRun
    {
        private static int _suppressPrepare;

        public static void SuppressNextPrepare()
        {
            System.Threading.Interlocked.Increment(ref _suppressPrepare);
        }

        public static bool OnWorkflowStarting(IWorkflowInstance instance, bool resumed)
        {
            if (resumed || instance == null)
            {
                return true;
            }

            if (System.Threading.Interlocked.CompareExchange(ref _suppressPrepare, 0, 0) > 0)
            {
                System.Threading.Interlocked.Decrement(ref _suppressPrepare);
                return true;
            }

            if (!IsRootStart(instance))
            {
                return true;
            }

            return InvokeOnUi(TryPrepareRootStart);
        }

        public static bool TryPrepareRootStart()
        {
            ClearOutputPanel();

            List<string> failures = SaveAllDirtyDesigners();
            if (failures == null || failures.Count == 0)
            {
                return true;
            }

            foreach (string line in failures)
            {
                Log.Output(line);
            }

            Window owner = PluginContext.MainWindow;
            MessageBoxResult result;
            if (owner != null)
            {
                result = MessageBox.Show(
                    owner,
                    BuildConfirmText(failures),
                    "Save before run",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);
            }
            else
            {
                result = MessageBox.Show(
                    BuildConfirmText(failures),
                    "Save before run",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);
            }

            return result == MessageBoxResult.Yes;
        }

        public static bool IsDesignerAtBreakpoint(IDesigner designer)
        {
            if (designer == null)
            {
                return false;
            }

            try
            {
                PropertyInfo prop = designer.GetType().GetProperty(
                    "BreakPointhit",
                    BindingFlags.Public | BindingFlags.Instance);
                object value = prop != null ? prop.GetValue(designer, null) : null;
                return true.Equals(value);
            }
            catch
            {
                return false;
            }
        }

        public static void WrapPlayCommands(DependencyObject root)
        {
            if (root == null)
            {
                return;
            }

            WalkAndWrap(root);
        }

        private static bool IsRootStart(IWorkflowInstance instance)
        {
            if (!string.IsNullOrWhiteSpace(instance.caller))
            {
                return false;
            }

            try
            {
                PropertyInfo identProp = instance.GetType().GetProperty(
                    "ident",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (identProp != null)
                {
                    object identValue = identProp.GetValue(instance, null);
                    if (identValue != null)
                    {
                        int ident = Convert.ToInt32(identValue);
                        if (ident > 0)
                        {
                            return false;
                        }
                    }
                }
            }
            catch
            {
            }

            return true;
        }

        private static List<string> SaveAllDirtyDesigners()
        {
            var failures = new List<string>();
            IOpenRPAClient client = PluginContext.Client;
            if (client == null)
            {
                return failures;
            }

            IDesigner[] designers = null;
            try
            {
                designers = client.Designers;
            }
            catch
            {
            }

            if (designers == null || designers.Length == 0)
            {
                return failures;
            }

            foreach (IDesigner designer in designers)
            {
                if (designer == null)
                {
                    continue;
                }

                bool dirty = false;
                try
                {
                    dirty = designer.HasChanged;
                }
                catch
                {
                    continue;
                }

                if (!dirty)
                {
                    continue;
                }

                string name = ResolveWorkflowLabel(designer);
                try
                {
                    bool ok = WaitForUiTask(designer.SaveAsync());
                    if (!ok)
                    {
                        failures.Add("Save failed: " + name + " (cancelled or a newer version exists).");
                    }
                }
                catch (Exception ex)
                {
                    failures.Add("Save failed: " + name + " — " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message));
                }
            }

            return failures;
        }

        private static string ResolveWorkflowLabel(IDesigner designer)
        {
            try
            {
                IWorkflow workflow = designer != null ? designer.Workflow : null;
                if (workflow == null)
                {
                    return "(unknown workflow)";
                }

                if (!string.IsNullOrWhiteSpace(workflow.ProjectAndName))
                {
                    return workflow.ProjectAndName;
                }

                if (!string.IsNullOrWhiteSpace(workflow.name))
                {
                    return workflow.name;
                }

                if (!string.IsNullOrWhiteSpace(workflow.RelativeFilename))
                {
                    return workflow.RelativeFilename;
                }
            }
            catch
            {
            }

            return "(unknown workflow)";
        }

        private static string BuildConfirmText(IList<string> failures)
        {
            var sb = new StringBuilder();
            sb.AppendLine("One or more workflows failed to save. Details were written to Output.");
            sb.AppendLine();
            int shown = 0;
            foreach (string line in failures)
            {
                if (shown >= 8)
                {
                    sb.AppendLine("…");
                    break;
                }

                sb.AppendLine(line);
                shown++;
            }

            sb.AppendLine();
            sb.Append("Continue running the workflow anyway?");
            return sb.ToString();
        }

        private static void ClearOutputPanel()
        {
            try
            {
                Window window = PluginContext.MainWindow;
                if (window == null)
                {
                    return;
                }

                PropertyInfo tracingProp = window.GetType().GetProperty(
                    "Tracing",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                object tracing = tracingProp != null ? tracingProp.GetValue(window, null) : null;
                if (tracing == null)
                {
                    return;
                }

                PropertyInfo outputProp = tracing.GetType().GetProperty(
                    "OutputMessages",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (outputProp != null && outputProp.CanWrite)
                {
                    outputProp.SetValue(tracing, string.Empty, null);
                }
            }
            catch
            {
            }
        }

        private static T InvokeOnUi<T>(Func<T> func)
        {
            if (func == null)
            {
                return default(T);
            }

            Application app = Application.Current;
            if (app != null && app.Dispatcher != null && !app.Dispatcher.CheckAccess())
            {
                return app.Dispatcher.Invoke(func, DispatcherPriority.Normal);
            }

            return func();
        }

        private static T WaitForUiTask<T>(Task<T> task)
        {
            if (task == null)
            {
                return default(T);
            }

            if (task.IsCompleted)
            {
                return task.GetAwaiter().GetResult();
            }

            Dispatcher dispatcher = Application.Current != null ? Application.Current.Dispatcher : null;
            if (dispatcher != null && dispatcher.CheckAccess())
            {
                var frame = new DispatcherFrame();
                task.ContinueWith(
                    _ => dispatcher.BeginInvoke(new Action(() => { frame.Continue = false; })),
                    TaskScheduler.Default);
                Dispatcher.PushFrame(frame);
                return task.GetAwaiter().GetResult();
            }

            if (dispatcher != null)
            {
                return dispatcher.Invoke(() => WaitForUiTask(task), DispatcherPriority.Normal);
            }

            return task.GetAwaiter().GetResult();
        }

        private static void WalkAndWrap(DependencyObject node)
        {
            if (node == null)
            {
                return;
            }

            var commandTarget = node as ICommandSource;
            if (commandTarget != null)
            {
                ICommand command = commandTarget.Command;
                if (IsOnPlayCommand(command))
                {
                    SetCommand(node, new PrepareThenPlayCommand(command));
                }
            }

            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++)
            {
                WalkAndWrap(VisualTreeHelper.GetChild(node, i));
            }

            foreach (object logical in LogicalTreeHelper.GetChildren(node))
            {
                WalkAndWrap(logical as DependencyObject);
            }
        }

        private static bool IsOnPlayCommand(ICommand command)
        {
            if (command == null || command is PrepareThenPlayCommand)
            {
                return false;
            }

            try
            {
                Type type = command.GetType();
                FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (FieldInfo field in fields)
                {
                    object value = field.GetValue(command);
                    var del = value as Delegate;
                    if (del != null && string.Equals(del.Method.Name, "OnPlay", StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static void SetCommand(DependencyObject node, ICommand command)
        {
            var button = node as System.Windows.Controls.Primitives.ButtonBase;
            if (button != null)
            {
                button.Command = command;
                return;
            }

            var menuItem = node as System.Windows.Controls.MenuItem;
            if (menuItem != null)
            {
                menuItem.Command = command;
            }
        }

        private sealed class PrepareThenPlayCommand : ICommand
        {
            private readonly ICommand _inner;

            public PrepareThenPlayCommand(ICommand inner)
            {
                _inner = inner;
                if (_inner != null)
                {
                    _inner.CanExecuteChanged += (s, e) =>
                    {
                        EventHandler handler = CanExecuteChanged;
                        if (handler != null)
                        {
                            handler(this, e);
                        }
                    };
                }
            }

            public event EventHandler CanExecuteChanged;

            public bool CanExecute(object parameter)
            {
                return _inner == null || _inner.CanExecute(parameter);
            }

            public void Execute(object parameter)
            {
                if (!TryPrepareRootStart())
                {
                    return;
                }

                SuppressNextPrepare();
                if (_inner != null)
                {
                    _inner.Execute(parameter);
                }
            }
        }
    }
}
