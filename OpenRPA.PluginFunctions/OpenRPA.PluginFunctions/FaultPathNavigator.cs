using System;
using System.Activities.Presentation.Model;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    /// <summary>
    /// Parses TraceableTryCatch Exception.Source and opens/focuses the leaf activity.
    /// Expected line format:
    ///   [Project/workflow.xaml] Sequence/Only If/Log Message
    ///   [Project/a.xaml] Sequence/Invoke Workflow&lt;Child/b.xaml&gt;
    /// Multi-line: last line is the fault leaf workflow + path.
    /// </summary>
    internal static class FaultPathNavigator
    {
        private static readonly Regex LineRegex = new Regex(
            @"^\s*\[(?<wf>[^\]]+)\]\s*(?<path>.*?)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex SegmentRegex = new Regex(
            @"^(?<name>.*?)(?:\[(?<idx>\d+)\])?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool Navigate(string exceptionSource)
        {
            FaultPathLine target = ParseLeafLine(exceptionSource);
            if (target == null)
            {
                MessageBox.Show(
                    "Could not parse Exception.Source.\nExpected lines like:\n[Project/workflow.xaml] Sequence/Activity",
                    "Go to Fault Path",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            IWorkflow workflow = ResolveWorkflow(target.WorkflowLabel);
            if (workflow == null)
            {
                MessageBox.Show(
                    "Workflow not found: " + target.WorkflowLabel,
                    "Go to Fault Path",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            IMainWindow window = PluginContext.Client != null ? PluginContext.Client.Window : null;
            if (window == null)
            {
                MessageBox.Show("OpenRPA main window is not available.", "Go to Fault Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            GenericTools.RunUI(() => window.OnOpenWorkflow(workflow), 20000);

            Application app = Application.Current;
            if (app != null && app.Dispatcher != null)
            {
                app.Dispatcher.BeginInvoke(new Action(() => FocusAfterOpen(workflow, target.ActivityPath)), DispatcherPriority.Background);
            }
            else
            {
                FocusAfterOpen(workflow, target.ActivityPath);
            }

            return true;
        }

        private static async void FocusAfterOpen(IWorkflow workflow, string activityPath)
        {
            try
            {
                await Task.Delay(250).ConfigureAwait(true);
                bool focused = false;
                for (int attempt = 0; attempt < 12 && !focused; attempt++)
                {
                    IDesigner designer = FindDesignerForWorkflow(workflow);
                    if (designer != null)
                    {
                        // Closing the Ctrl+T window can leave Open Project selected;
                        // re-activate the designer tab before focusing the activity.
                        try
                        {
                            designer.IsSelected = true;
                        }
                        catch
                        {
                        }

                        focused = TryFocusPath(designer, activityPath);
                    }

                    if (!focused)
                    {
                        await Task.Delay(200).ConfigureAwait(true);
                    }
                }

                if (!focused)
                {
                    MessageBox.Show(
                        "Opened workflow, but could not focus activity path:\n" + activityPath,
                        "Go to Fault Path",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch
            {
            }
        }

        private static FaultPathLine ParseLeafLine(string exceptionSource)
        {
            if (string.IsNullOrWhiteSpace(exceptionSource))
            {
                return null;
            }

            string[] lines = exceptionSource
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            FaultPathLine last = null;
            foreach (string raw in lines)
            {
                FaultPathLine parsed = ParseLine(raw);
                if (parsed != null)
                {
                    last = parsed;
                }
            }

            // Fallback: entire text without brackets — treat as display path in current workflow.
            if (last == null)
            {
                string trimmed = exceptionSource.Trim();
                if (trimmed.IndexOf('/') >= 0 || trimmed.IndexOf('>') >= 0)
                {
                    IDesigner current = PluginContext.ResolveDesigner();
                    string label = current != null && current.Workflow != null
                        ? (current.Workflow.ProjectAndName ?? current.Workflow.RelativeFilename)
                        : null;
                    return new FaultPathLine
                    {
                        WorkflowLabel = label,
                        ActivityPath = NormalizePathSeparators(trimmed)
                    };
                }
            }

            return last;
        }

        private static FaultPathLine ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            Match match = LineRegex.Match(line.Trim());
            if (!match.Success)
            {
                return null;
            }

            string wf = match.Groups["wf"].Value.Trim();
            string path = match.Groups["path"].Value.Trim();

            // Drop trailing "<ChildWorkflow...>" invoke suffix — leaf line usually has none;
            // if present, path before '<' is still the activity chain in this workflow.
            int angle = path.IndexOf('<');
            if (angle >= 0)
            {
                path = path.Substring(0, angle).Trim();
            }

            path = NormalizePathSeparators(path);
            if (string.IsNullOrWhiteSpace(wf))
            {
                return null;
            }

            return new FaultPathLine
            {
                WorkflowLabel = wf,
                ActivityPath = path
            };
        }

        private static string NormalizePathSeparators(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path
                .Replace(" > ", "/")
                .Replace(">", "/")
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
        }

        private static IWorkflow ResolveWorkflow(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return null;
            }

            string key = label.Trim().Replace('\\', '/');
            Match indexed = Regex.Match(key, @"^(?<base>.+?)(?:\[(?<idx>\d+)\])?$");
            if (indexed.Success)
            {
                key = indexed.Groups["base"].Value.Trim();
            }

            IWorkflow current = null;
            try
            {
                IDesigner designer = PluginContext.ResolveDesigner();
                current = designer != null ? designer.Workflow : null;
            }
            catch
            {
            }

            if (current != null && WorkflowMatchesFaultLabel(current, key))
            {
                return current;
            }

            foreach (string lookup in EnumerateLookupKeys(key))
            {
                IWorkflow found = TryGetWorkflowByIdOrRelative(lookup);
                if (found != null)
                {
                    return found;
                }
            }

            IReadOnlyList<IWorkflow> all = OpenRpaCatalogAccess.GetAllWorkflows();
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (WorkflowMatchesFaultLabel(all[i], key))
                    {
                        return all[i];
                    }
                }
            }

            return null;
        }

        private static IEnumerable<string> EnumerateLookupKeys(string key)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string candidate in new[]
            {
                key,
                StripXamlExtension(key)
            })
            {
                if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
                {
                    yield return candidate;
                }
            }

            string project;
            string file;
            SplitProjectFile(key, out project, out file);
            if (string.IsNullOrWhiteSpace(file))
            {
                yield break;
            }

            foreach (string candidate in new[]
            {
                file,
                StripXamlExtension(file),
                string.IsNullOrWhiteSpace(project) ? null : project + "/" + StripXamlExtension(file)
            })
            {
                if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        private static IWorkflow TryGetWorkflowByIdOrRelative(string key)
        {
            if (PluginContext.Client == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            try
            {
                return PluginContext.Client.GetWorkflowByIDOrRelativeFilename(key);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// TraceableTryCatch writes [project/DisplayName.xaml]. OpenRPA lookup keys are
        /// ProjectAndName (no .xaml) or RelativeFilename (often spaces stripped).
        /// </summary>
        private static bool WorkflowMatchesFaultLabel(IWorkflow workflow, string label)
        {
            if (workflow == null || string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            string project;
            string file;
            SplitProjectFile(label, out project, out file);
            if (string.IsNullOrWhiteSpace(file))
            {
                file = label;
            }

            string workflowProject = OpenRpaCatalogAccess.GetProjectName(workflow);
            if (!string.IsNullOrWhiteSpace(project) && !string.IsNullOrWhiteSpace(workflowProject)
                && !string.Equals(NormalizeKey(project), NormalizeKey(workflowProject), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string[] candidates =
            {
                workflow.ProjectAndName,
                workflow.RelativeFilename,
                workflow.Filename,
                workflow.name,
                workflow._id
            };

            string labelNorm = NormalizeKey(label);
            string fileNorm = NormalizeKey(file);
            string fileCompact = CompactKey(file);

            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrWhiteSpace(candidate))
                {
                    continue;
                }

                string candNorm = NormalizeKey(candidate);
                if (string.Equals(candNorm, labelNorm, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candNorm, fileNorm, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                string candFile;
                string unusedProject;
                SplitProjectFile(candidate, out unusedProject, out candFile);
                if (string.IsNullOrWhiteSpace(candFile))
                {
                    candFile = candidate;
                }

                string candFileNorm = NormalizeKey(candFile);
                if (string.Equals(candFileNorm, fileNorm, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(CompactKey(candFile), fileCompact, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string StripXamlExtension(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            value = value.Trim();
            if (value.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            {
                return value.Substring(0, value.Length - 5);
            }

            return value;
        }

        private static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return StripXamlExtension(value.Trim().Replace('\\', '/'));
        }

        private static string CompactKey(string value)
        {
            string normalized = NormalizeKey(value);
            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            int slash = normalized.LastIndexOf('/');
            if (slash >= 0 && slash < normalized.Length - 1)
            {
                normalized = normalized.Substring(slash + 1);
            }

            var chars = new char[normalized.Length];
            int n = 0;
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if (char.IsLetterOrDigit(c))
                {
                    chars[n++] = char.ToLowerInvariant(c);
                }
            }

            return n == 0 ? string.Empty : new string(chars, 0, n);
        }

        private static void SplitProjectFile(string label, out string project, out string file)
        {
            project = null;
            file = label;
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            int slash = label.IndexOf('/');
            if (slash <= 0 || slash >= label.Length - 1)
            {
                return;
            }

            project = label.Substring(0, slash).Trim();
            file = label.Substring(slash + 1).Trim();
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
            }

            if (byId != null)
            {
                return byId;
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

        private static bool TryFocusPath(IDesigner designer, string activityPath)
        {
            if (designer == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(activityPath) || activityPath == "?")
            {
                return true;
            }

            List<string> segments = SplitPathSegments(activityPath);
            if (segments.Count == 0)
            {
                return true;
            }

            List<ModelItem> all = ActivityInsertService.GetAllActivities(designer);
            ModelItem match = FindByDisplayPath(all, segments);
            if (match == null)
            {
                // Fallback: last segment name only.
                PathSegment last = ParseSegment(segments[segments.Count - 1]);
                match = all.LastOrDefault(mi =>
                    string.Equals(ActivityInsertService.GetDisplayName(mi), last.Name, StringComparison.OrdinalIgnoreCase));
            }

            if (match == null)
            {
                return false;
            }

            return ActivityInsertService.TryFocusModelItem(designer, match);
        }

        private static List<string> SplitPathSegments(string path)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(path))
            {
                return list;
            }

            foreach (string part in path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = part.Trim();
                if (trimmed.Length > 0)
                {
                    list.Add(trimmed);
                }
            }

            return list;
        }

        private static PathSegment ParseSegment(string segment)
        {
            Match m = SegmentRegex.Match((segment ?? string.Empty).Trim());
            if (!m.Success)
            {
                return new PathSegment { Name = segment, Index1Based = 1 };
            }

            int idx = 1;
            if (m.Groups["idx"].Success)
            {
                int.TryParse(m.Groups["idx"].Value, out idx);
                if (idx < 1)
                {
                    idx = 1;
                }
            }

            return new PathSegment
            {
                Name = (m.Groups["name"].Value ?? string.Empty).Trim(),
                Index1Based = idx
            };
        }

        /// <summary>
        /// Match a chain of DisplayNames across the flattened activity list by walking parent links.
        /// </summary>
        private static ModelItem FindByDisplayPath(List<ModelItem> all, List<string> segments)
        {
            if (all == null || all.Count == 0 || segments == null || segments.Count == 0)
            {
                return null;
            }

            PathSegment[] parsed = segments.Select(ParseSegment).ToArray();

            // Candidates for the last segment.
            PathSegment leafSeg = parsed[parsed.Length - 1];
            List<ModelItem> leafCandidates = all
                .Where(mi => string.Equals(ActivityInsertService.GetDisplayName(mi), leafSeg.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (leafCandidates.Count == 0)
            {
                return null;
            }

            if (leafCandidates.Count == 1 && parsed.Length == 1)
            {
                return leafCandidates[0];
            }

            var scored = new List<Tuple<ModelItem, int>>();
            foreach (ModelItem leaf in leafCandidates)
            {
                int score = ScoreAncestorChain(leaf, parsed);
                if (score > 0)
                {
                    scored.Add(Tuple.Create(leaf, score));
                }
            }

            if (scored.Count == 0)
            {
                // Same-name index fallback among leaves.
                int index = Math.Max(0, leafSeg.Index1Based - 1);
                if (index < leafCandidates.Count)
                {
                    return leafCandidates[index];
                }

                return leafCandidates[leafCandidates.Count - 1];
            }

            return scored
                .OrderByDescending(t => t.Item2)
                .ThenBy(t => Depth(t.Item1))
                .Select(t => t.Item1)
                .First();
        }

        private static int ScoreAncestorChain(ModelItem leaf, PathSegment[] parsed)
        {
            var chain = new List<string>();
            ModelItem current = leaf;
            int guard = 0;
            while (current != null && guard++ < 256)
            {
                string name = ActivityInsertService.GetDisplayName(current);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    chain.Insert(0, name);
                }

                current = current.Parent;
            }

            // Align parsed segments as a suffix / subsequence of ancestor display names.
            int pi = 0;
            for (int i = 0; i < chain.Count && pi < parsed.Length; i++)
            {
                if (string.Equals(chain[i], parsed[pi].Name, StringComparison.OrdinalIgnoreCase))
                {
                    pi++;
                }
            }

            if (pi == parsed.Length)
            {
                return 1000 + parsed.Length * 10;
            }

            // Suffix match only on last N
            int need = parsed.Length;
            if (chain.Count >= need)
            {
                bool ok = true;
                for (int i = 0; i < need; i++)
                {
                    if (!string.Equals(chain[chain.Count - need + i], parsed[i].Name, StringComparison.OrdinalIgnoreCase))
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    return 800 + need;
                }
            }

            return 0;
        }

        private static int Depth(ModelItem item)
        {
            int d = 0;
            while (item != null && d < 256)
            {
                d++;
                item = item.Parent;
            }

            return d;
        }

        private sealed class FaultPathLine
        {
            public string WorkflowLabel { get; set; }
            public string ActivityPath { get; set; }
        }

        private sealed class PathSegment
        {
            public string Name { get; set; }
            public int Index1Based { get; set; }
        }
    }
}
