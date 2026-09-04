using System;
using System.Activities.Presentation.Toolbox;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Registers Lib XAML activities into the OpenRPA toolbox after UI is ready.
    /// </summary>
    internal static class LibXamlToolboxRegistrar
    {
        private static readonly object Gate = new object();
        private static DispatcherTimer _retryTimer;
        private static bool _registered;
        private static int _attempts;
        private static LibXamlActivityGenerator _generator;

        public static void Start()
        {
            lock (Gate)
            {
                _attempts = 0;
                _registered = false;
            }

            LibXamlPaths.EnsureLibsRootExists();

            Application app = Application.Current;
            if (app == null || app.Dispatcher == null)
            {
                System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                {
                    PluginContext.RunOnUi(Start);
                });
                return;
            }

            app.Dispatcher.BeginInvoke(new Action(TryRegister), DispatcherPriority.ApplicationIdle);
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
                if (_registered || _attempts > 90)
                {
                    _retryTimer.Stop();
                    if (!_registered)
                    {
                        Log.Warning("PluginFunctions: timed out waiting for toolbox to register Lib XAMLs.");
                    }

                    return;
                }

                TryRegister();
            };
            _retryTimer.Start();
        }

        private static void TryRegister()
        {
            lock (Gate)
            {
                if (_registered)
                {
                    return;
                }

                _attempts++;
                try
                {
                    ToolboxControl toolbox = ToolboxAccess.FindToolboxControl();
                    if (toolbox == null || toolbox.Categories == null)
                    {
                        return;
                    }

                    IReadOnlyList<LibXamlEntry> entries = LibXamlScanner.Scan();
                    if (entries.Count == 0)
                    {
                        _registered = true;
                        StopRetryTimer();
                        Log.Information("PluginFunctions: no Lib XAML files to register.");
                        return;
                    }

                    if (_generator == null)
                    {
                        LibXamlActivityGenerator.EnsureAssemblyResolveHook();
                        _generator = new LibXamlActivityGenerator(LibXamlActivityGenerator.GetGeneratedDirectory());
                    }

                    var byCategory = entries
                        .GroupBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
                        .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

                    int added = 0;
                    foreach (IGrouping<string, LibXamlEntry> group in byCategory)
                    {
                        ToolboxCategory category = FindOrCreateCategory(toolbox, group.Key);

                        foreach (LibXamlEntry entry in group.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase))
                        {
                            try
                            {
                                if (CategoryContainsDisplayName(category, entry.DisplayName))
                                {
                                    continue;
                                }

                                List<LibXamlArgumentSpec> args =
                                    InvokeLibXamlActivityBase.ReadArgumentSpecs(entry.FilePath);
                                Type activityType = _generator.GetOrCreateActivityType(entry, args);
                                if (activityType == null)
                                {
                                    continue;
                                }

                                category.Add(new ToolboxItemWrapper(activityType, entry.DisplayName));
                                added++;
                            }
                            catch (Exception ex)
                            {
                                Log.Error(
                                    "PluginFunctions: failed to register '" + entry.FilePath + "': " + ex);
                            }
                        }
                    }

                    _generator.Save();
                    RemoveAutoScannedLibXamlCategory(toolbox);
                    ActivityCatalog.Invalidate();
                    PromoteCustomizedCategories(toolbox);
                    _registered = true;
                    StopRetryTimer();
                    Log.Information("PluginFunctions: registered " + added + " Lib XAML activities into toolbox.");
                }
                catch (Exception ex)
                {
                    Log.Error("PluginFunctions: Lib XAML toolbox registration error: " + ex);
                }
            }
        }

        private static void RemoveAutoScannedLibXamlCategory(ToolboxControl toolbox)
        {
            if (toolbox?.Categories == null)
            {
                return;
            }

            ToolboxCategory match = null;
            foreach (ToolboxCategory category in toolbox.Categories)
            {
                if (category != null
                    && string.Equals(
                        category.CategoryName,
                        LibXamlActivityGenerator.AssemblySimpleName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    match = category;
                    break;
                }
            }

            if (match != null)
            {
                try
                {
                    toolbox.Categories.Remove(match);
                }
                catch
                {
                }
            }
        }

        private static void PromoteCustomizedCategories(ToolboxControl toolbox)
        {
            if (toolbox?.Categories == null || toolbox.Categories.Count == 0)
            {
                return;
            }

            var snapshot = new List<ToolboxCategory>();
            foreach (ToolboxCategory category in toolbox.Categories)
            {
                if (category != null)
                {
                    snapshot.Add(category);
                }
            }

            List<ToolboxCategory> customized = snapshot
                .Where(c => IsCustomizedCategory(c.CategoryName))
                .OrderBy(c => c.CategoryName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (customized.Count == 0)
            {
                return;
            }

            List<ToolboxCategory> others = snapshot
                .Where(c => !IsCustomizedCategory(c.CategoryName))
                .ToList();

            List<ToolboxCategory> ordered = customized.Concat(others).ToList();
            bool alreadyOrdered = true;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (!ReferenceEquals(ordered[i], snapshot[i]))
                {
                    alreadyOrdered = false;
                    break;
                }
            }

            if (alreadyOrdered)
            {
                return;
            }

            toolbox.Categories.Clear();
            foreach (ToolboxCategory category in ordered)
            {
                toolbox.Categories.Add(category);
            }
        }

        private static bool IsCustomizedCategory(string categoryName)
        {
            return !string.IsNullOrEmpty(categoryName)
                   && categoryName.StartsWith(LibXamlPaths.CategoryPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static void StopRetryTimer()
        {
            if (_retryTimer != null)
            {
                _retryTimer.Stop();
            }
        }

        private static ToolboxCategory FindOrCreateCategory(ToolboxControl toolbox, string categoryName)
        {
            foreach (ToolboxCategory existing in toolbox.Categories)
            {
                if (existing != null
                    && string.Equals(existing.CategoryName, categoryName, StringComparison.OrdinalIgnoreCase))
                {
                    return existing;
                }
            }

            var created = new ToolboxCategory(categoryName);
            toolbox.Categories.Add(created);
            return created;
        }

        private static bool CategoryContainsDisplayName(ToolboxCategory category, string displayName)
        {
            if (category?.Tools == null || string.IsNullOrWhiteSpace(displayName))
            {
                return false;
            }

            foreach (ToolboxItemWrapper tool in category.Tools)
            {
                if (tool != null
                    && string.Equals(tool.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
