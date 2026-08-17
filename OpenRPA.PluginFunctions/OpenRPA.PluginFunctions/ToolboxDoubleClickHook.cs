using System;
using System.Activities;
using System.Activities.Presentation.Toolbox;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Double-click a Toolbox activity to insert it into the active workflow designer.
    /// </summary>
    internal static class ToolboxDoubleClickHook
    {
        private static readonly object Gate = new object();
        private static DispatcherTimer _retryTimer;
        private static bool _attached;
        private static int _attempts;

        public static void Start()
        {
            _attempts = 0;
            _attached = false;

            Application app = Application.Current;
            if (app == null || app.Dispatcher == null)
            {
                return;
            }

            app.Dispatcher.BeginInvoke(new Action(TryAttach), DispatcherPriority.ApplicationIdle);
            EnsureRetryTimer(app.Dispatcher);
        }

        private static void EnsureRetryTimer(Dispatcher dispatcher)
        {
            if (_retryTimer != null)
            {
                return;
            }

            _retryTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _retryTimer.Tick += (s, e) =>
            {
                if (_attached || _attempts > 60)
                {
                    _retryTimer.Stop();
                    return;
                }

                TryAttach();
            };
            _retryTimer.Start();
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
                try
                {
                    ToolboxControl toolbox = ToolboxAccess.FindToolboxControl();
                    if (toolbox == null)
                    {
                        return;
                    }

                    toolbox.MouseDoubleClick -= OnToolboxMouseDoubleClick;
                    toolbox.MouseDoubleClick += OnToolboxMouseDoubleClick;
                    _attached = true;
                    if (_retryTimer != null)
                    {
                        _retryTimer.Stop();
                    }
                }
                catch
                {
                }
            }
        }

        private static void OnToolboxMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var source = e.OriginalSource as DependencyObject;
                TreeViewItem treeItem = FindAncestor<TreeViewItem>(source);
                if (treeItem == null)
                {
                    return;
                }

                ToolboxItemWrapper wrapper = ResolveToolboxWrapper(treeItem);
                if (wrapper == null || wrapper.Type == null)
                {
                    return;
                }

                if (treeItem.HasItems && treeItem.Items.Count > 0 && !(treeItem.DataContext is ToolboxItemWrapper))
                {
                    return;
                }

                if (ActivityInsertService.TryAddActivity(wrapper.Type))
                {
                    e.Handled = true;
                }
            }
            catch
            {
            }
        }

        private static ToolboxItemWrapper ResolveToolboxWrapper(TreeViewItem treeItem)
        {
            var direct = treeItem.DataContext as ToolboxItemWrapper;
            if (direct != null)
            {
                return direct;
            }

            object ctx = treeItem.DataContext;
            if (ctx == null)
            {
                return null;
            }

            PropertyInfo toolProp = ctx.GetType().GetProperty("Tool")
                ?? ctx.GetType().GetProperty("ToolboxItem")
                ?? ctx.GetType().GetProperty("Item");
            return toolProp != null ? toolProp.GetValue(ctx) as ToolboxItemWrapper : null;
        }

        private static T FindAncestor<T>(DependencyObject from) where T : DependencyObject
        {
            DependencyObject current = from;
            while (current != null)
            {
                var match = current as T;
                if (match != null)
                {
                    return match;
                }

                current = VisualTreeHelper.GetParent(current)
                    ?? LogicalTreeHelper.GetParent(current);
            }

            return null;
        }
    }
}
