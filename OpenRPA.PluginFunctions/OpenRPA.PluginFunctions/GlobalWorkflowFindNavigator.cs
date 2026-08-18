using System;
using System.Activities.Presentation.Model;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Opens a workflow and focuses a matched activity (by display path / name / type).
    /// </summary>
    internal static class GlobalWorkflowFindNavigator
    {
        public static void Navigate(GlobalFindEntry entry)
        {
            if (entry == null || entry.Workflow == null)
            {
                return;
            }

            IMainWindow window = PluginContext.Client != null ? PluginContext.Client.Window : null;
            if (window == null)
            {
                MessageBox.Show("OpenRPA main window is not available.", "Find in projects", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IWorkflow workflow = entry.Workflow;
            GenericTools.RunUI(() => window.OnOpenWorkflow(workflow), 20000);

            Application app = Application.Current;
            if (app != null && app.Dispatcher != null)
            {
                app.Dispatcher.BeginInvoke(
                    new Action(() => FocusAfterOpen(workflow, entry)),
                    DispatcherPriority.Background);
            }
            else
            {
                FocusAfterOpen(workflow, entry);
            }
        }

        private static async void FocusAfterOpen(IWorkflow workflow, GlobalFindEntry entry)
        {
            try
            {
                await Task.Delay(250).ConfigureAwait(true);
                bool focused = false;
                for (int attempt = 0; attempt < 15 && !focused; attempt++)
                {
                    IDesigner designer = FindDesignerForWorkflow(workflow);
                    if (designer != null)
                    {
                        try
                        {
                            designer.IsSelected = true;
                        }
                        catch
                        {
                        }

                        focused = TryFocusEntry(designer, entry);
                    }

                    if (!focused)
                    {
                        await Task.Delay(200).ConfigureAwait(true);
                    }
                }
            }
            catch
            {
            }
        }

        private static IDesigner FindDesignerForWorkflow(IWorkflow workflow)
        {
            if (workflow == null || PluginContext.Client == null)
            {
                return null;
            }

            string id = workflow._id;
            string relative = workflow.RelativeFilename;
            string projectAndName = workflow.ProjectAndName;

            IDesigner byId = null;
            if (!string.IsNullOrWhiteSpace(id))
            {
                byId = PluginContext.Client.GetWorkflowDesignerByIDOrRelativeFilename(id);
                if (byId != null)
                {
                    return byId;
                }
            }

            if (!string.IsNullOrWhiteSpace(relative))
            {
                byId = PluginContext.Client.GetWorkflowDesignerByIDOrRelativeFilename(relative);
                if (byId != null)
                {
                    return byId;
                }
            }

            if (!string.IsNullOrWhiteSpace(projectAndName))
            {
                byId = PluginContext.Client.GetWorkflowDesignerByIDOrRelativeFilename(projectAndName);
                if (byId != null)
                {
                    return byId;
                }
            }

            return PluginContext.ResolveDesigner();
        }

        private static bool TryFocusEntry(IDesigner designer, GlobalFindEntry entry)
        {
            if (designer == null || entry == null)
            {
                return false;
            }

            List<ModelItem> all = ActivityInsertService.GetAllActivities(designer);
            if (all == null || all.Count == 0)
            {
                return false;
            }

            ModelItem match = null;

            // 1) Prefer display path leaf + type.
            if (!string.IsNullOrWhiteSpace(entry.DisplayPath))
            {
                string[] segments = entry.DisplayPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                string leaf = segments.Length > 0 ? segments[segments.Length - 1].Trim() : null;
                if (!string.IsNullOrEmpty(leaf))
                {
                    match = all.LastOrDefault(mi =>
                        string.Equals(ActivityInsertService.GetDisplayName(mi), leaf, StringComparison.OrdinalIgnoreCase)
                        && TypeNameMatches(mi, entry.ActivityName));
                }
            }

            // 2) DisplayName + type.
            if (match == null && !string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                match = all.LastOrDefault(mi =>
                    string.Equals(ActivityInsertService.GetDisplayName(mi), entry.DisplayName, StringComparison.OrdinalIgnoreCase)
                    && TypeNameMatches(mi, entry.ActivityName));
            }

            // 3) DisplayName only.
            if (match == null && !string.IsNullOrWhiteSpace(entry.DisplayName))
            {
                match = all.LastOrDefault(mi =>
                    string.Equals(ActivityInsertService.GetDisplayName(mi), entry.DisplayName, StringComparison.OrdinalIgnoreCase));
            }

            // 4) Argument value disambiguation.
            if (match == null && entry.ArgumentValues != null && entry.ArgumentValues.Count > 0)
            {
                string needle = entry.ArgumentValues[0];
                match = all.FirstOrDefault(mi =>
                {
                    string dn = ActivityInsertService.GetDisplayName(mi) ?? string.Empty;
                    if (!string.IsNullOrEmpty(entry.DisplayName)
                        && !string.Equals(dn, entry.DisplayName, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    return TypeNameMatches(mi, entry.ActivityName);
                });
            }

            if (match == null)
            {
                return false;
            }

            return ActivityInsertService.TryFocusModelItem(designer, match);
        }

        private static bool TypeNameMatches(ModelItem item, string activityName)
        {
            if (item == null || item.ItemType == null || string.IsNullOrEmpty(activityName))
            {
                return true;
            }

            string name = item.ItemType.Name ?? string.Empty;
            int tick = name.IndexOf('`');
            if (tick > 0)
            {
                name = name.Substring(0, tick);
            }

            return string.Equals(name, activityName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
