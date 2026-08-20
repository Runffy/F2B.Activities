using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    internal sealed class GlobalFindEntry
    {
        public IWorkflow Workflow { get; set; }
        public string ProjectName { get; set; }
        public string WorkflowName { get; set; }
        public string DisplayName { get; set; }
        public string ActivityName { get; set; }
        public string DisplayPath { get; set; }
        public List<string> ArgumentValues { get; set; }
    }

    internal sealed class GlobalFindItem
    {
        public GlobalFindEntry Entry { get; set; }
        public string DisplayName { get; set; }
        public string MatchHint { get; set; }
        public System.Windows.Media.ImageSource Icon { get; set; }
        public int Score { get; set; }
    }

    /// <summary>
    /// Indexes and filters activities across all projects / workflows (XAML text scan).
    /// </summary>
    internal static class GlobalWorkflowActivitySearch
    {
        private static readonly HashSet<string> SkipElementNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Activity", "ActivityBuilder", "TextExpression", "NamespacesForImplementation",
            "ReferencesForImplementation", "WorkflowViewState", "ViewStateData", "ViewStateManager",
            "Dictionary", "List", "Collection", "Property", "InArgument", "OutArgument", "InOutArgument",
            "Variable", "VariableCollection", "ActivityAction", "ActivityFunc", "DelegateInArgument",
            "DelegateOutArgument", "VisualBasicValue", "VisualBasicReference", "CSharpValue", "CSharpReference",
            "Literal", "LambdaValue", "LocationReferenceValue", "VisualBasicSettings", "AssemblyReference"
        };

        private static List<GlobalFindEntry> _index;
        private static readonly object Gate = new object();

        public static void Invalidate()
        {
            lock (Gate)
            {
                _index = null;
            }
        }

        public static void EnsureIndex()
        {
            lock (Gate)
            {
                if (_index != null)
                {
                    return;
                }

                _index = BuildIndex();
            }
        }

        public static IReadOnlyList<GlobalFindItem> Search(string pattern, int maxResults = 50)
        {
            EnsureIndex();
            List<GlobalFindEntry> source;
            lock (Gate)
            {
                source = _index ?? new List<GlobalFindEntry>();
            }

            string needle = (pattern ?? string.Empty).Trim();
            // Global search requires a keyword to avoid dumping thousands of rows.
            if (needle.Length == 0)
            {
                return new List<GlobalFindItem>();
            }

            var results = new List<GlobalFindItem>();
            foreach (GlobalFindEntry entry in source)
            {
                if (entry == null)
                {
                    continue;
                }

                int score = WorkflowActivitySearch.ScoreMatch(
                    entry.DisplayName,
                    entry.ActivityName,
                    entry.ArgumentValues,
                    needle);

                // Also allow matching project / workflow name lightly.
                score = Math.Max(score, WorkflowActivitySearch.ScoreMatch(
                    entry.WorkflowName, entry.ProjectName, null, needle) / 2);

                if (score <= 0)
                {
                    continue;
                }

                string path = !string.IsNullOrWhiteSpace(entry.DisplayPath)
                    ? ActivityDisplayPathBuilder.FormatDisplayNameChain(
                        entry.DisplayPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = entry.DisplayName ?? entry.ActivityName ?? string.Empty;
                }

                string location = FormatLocation(entry.ProjectName, entry.WorkflowName);
                // Same spirit as TraceableTryCatch Exception.Source: [project/workflow] Sequence/...
                string hint;
                if (!string.IsNullOrEmpty(location) && !string.IsNullOrEmpty(path))
                {
                    hint = "[" + location + "] " + path;
                }
                else if (!string.IsNullOrEmpty(path))
                {
                    hint = path;
                }
                else
                {
                    hint = location;
                }

                results.Add(new GlobalFindItem
                {
                    Entry = entry,
                    DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                        ? entry.ActivityName
                        : entry.DisplayName,
                    MatchHint = hint,
                    Icon = ResolveIcon(entry.ActivityName),
                    Score = score
                });
            }

            return results
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .ToList();
        }

        private static string FormatLocation(string project, string workflow)
        {
            if (!string.IsNullOrWhiteSpace(project) && !string.IsNullOrWhiteSpace(workflow))
            {
                return project + "/" + workflow;
            }

            if (!string.IsNullOrWhiteSpace(workflow))
            {
                return workflow;
            }

            return project ?? string.Empty;
        }

        private static System.Windows.Media.ImageSource ResolveIcon(string activityName)
        {
            if (string.IsNullOrEmpty(activityName))
            {
                return null;
            }

            try
            {
                foreach (ActivityCatalogItem item in ActivityCatalog.Search(activityName))
                {
                    if (item == null || item.Type == null)
                    {
                        continue;
                    }

                    string name = item.Type.Name ?? string.Empty;
                    int tick = name.IndexOf('`');
                    if (tick > 0)
                    {
                        name = name.Substring(0, tick);
                    }

                    if (string.Equals(name, activityName, StringComparison.OrdinalIgnoreCase))
                    {
                        return item.Icon;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static List<GlobalFindEntry> BuildIndex()
        {
            var list = new List<GlobalFindEntry>();
            IReadOnlyList<IWorkflow> workflows = OpenRpaCatalogAccess.GetAllWorkflows();
            foreach (IWorkflow workflow in workflows)
            {
                if (workflow == null)
                {
                    continue;
                }

                string xaml = OpenRpaCatalogAccess.ResolveXaml(workflow);
                if (string.IsNullOrWhiteSpace(xaml))
                {
                    continue;
                }

                string projectName = OpenRpaCatalogAccess.GetProjectName(workflow);
                string workflowName = workflow.name
                    ?? workflow.Filename
                    ?? workflow.RelativeFilename
                    ?? "Workflow";

                int before = list.Count;
                try
                {
                    IndexXaml(xaml, workflow, projectName, workflowName, list);
                }
                catch
                {
                }

                // Regex fallback if XML walk produced nothing (malformed / unexpected XAML shape).
                if (list.Count == before)
                {
                    try
                    {
                        IndexXamlByRegex(xaml, workflow, projectName, workflowName, list);
                    }
                    catch
                    {
                    }
                }
            }

            return list;
        }

        private static void IndexXamlByRegex(
            string xaml,
            IWorkflow workflow,
            string projectName,
            string workflowName,
            List<GlobalFindEntry> sink)
        {
            if (string.IsNullOrEmpty(xaml) || sink == null)
            {
                return;
            }

            // DisplayName="..."
            foreach (System.Text.RegularExpressions.Match match in
                     System.Text.RegularExpressions.Regex.Matches(
                         xaml,
                         @"DisplayName\s*=\s*""([^""]*)""",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                string display = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(display))
                {
                    continue;
                }

                sink.Add(new GlobalFindEntry
                {
                    Workflow = workflow,
                    ProjectName = projectName,
                    WorkflowName = workflowName,
                    DisplayName = display.Trim(),
                    ActivityName = display.Trim(),
                    DisplayPath = display.Trim(),
                    ArgumentValues = new List<string>()
                });
            }

            // <prefix:TypeName ...> activity-like elements
            foreach (System.Text.RegularExpressions.Match match in
                     System.Text.RegularExpressions.Regex.Matches(
                         xaml,
                         @"<(?:[A-Za-z_][\w]*:)?([A-Z][A-Za-z0-9_]*)\b([^>]*)>",
                         System.Text.RegularExpressions.RegexOptions.Multiline))
            {
                string typeName = match.Groups[1].Value;
                if (ShouldSkipElement(typeName)
                    || IsPropertyElementName(typeName)
                    || IsExpressionElementName(StripGeneric(typeName))
                    || !LooksLikeActivityElement(typeName))
                {
                    continue;
                }

                string attrs = match.Groups[2].Value ?? string.Empty;
                string display = typeName;
                System.Text.RegularExpressions.Match dn =
                    System.Text.RegularExpressions.Regex.Match(
                        attrs,
                        @"DisplayName\s*=\s*""([^""]*)""",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (dn.Success && !string.IsNullOrWhiteSpace(dn.Groups[1].Value))
                {
                    display = dn.Groups[1].Value.Trim();
                }

                sink.Add(new GlobalFindEntry
                {
                    Workflow = workflow,
                    ProjectName = projectName,
                    WorkflowName = workflowName,
                    DisplayName = display,
                    ActivityName = StripGeneric(typeName),
                    DisplayPath = display,
                    ArgumentValues = ExtractAttributeValues(attrs)
                });
            }
        }

        private static List<string> ExtractAttributeValues(string attrs)
        {
            var values = new List<string>();
            if (string.IsNullOrEmpty(attrs))
            {
                return values;
            }

            foreach (System.Text.RegularExpressions.Match match in
                     System.Text.RegularExpressions.Regex.Matches(
                         attrs,
                         @"\b(?!DisplayName\b)[A-Za-z_][\w]*\s*=\s*""([^""]*)""",
                         System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                string value = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(value)
                    || value.Length > 500
                    || value.IndexOf("clr-namespace", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                values.Add(Unquote(value.Trim()));
            }

            return values;
        }

        private static void IndexXaml(
            string xaml,
            IWorkflow workflow,
            string projectName,
            string workflowName,
            List<GlobalFindEntry> sink)
        {
            var elementStack = new Stack<PathFrame>();
            var pathStack = new List<string>();
            int lastActivitySinkIndex = -1;

            using (var reader = XmlReader.Create(new StringReader(xaml), new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
                DtdProcessing = DtdProcessing.Prohibit
            }))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        string localName = reader.LocalName ?? string.Empty;
                        bool isEmpty = reader.IsEmptyElement;
                        string displayName = reader.GetAttribute("DisplayName");
                        var argumentValues = new List<string>();
                        var expressionOnlyValues = new List<string>();
                        bool isExpressionElement = IsExpressionElementName(StripGeneric(localName));

                        if (reader.HasAttributes)
                        {
                            while (reader.MoveToNextAttribute())
                            {
                                string attrName = reader.LocalName ?? string.Empty;
                                if (string.Equals(attrName, "DisplayName", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(attrName, "Name", StringComparison.OrdinalIgnoreCase)
                                    || attrName.EndsWith("Ref", StringComparison.OrdinalIgnoreCase)
                                    || attrName.StartsWith("xmlns", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(attrName, "TypeArguments", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(attrName, "Key", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                string value = reader.Value;
                                if (string.IsNullOrWhiteSpace(value)
                                    || value.Length > 500
                                    || value.IndexOf("clr-namespace", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    continue;
                                }

                                string cleaned = Unquote(value.Trim());
                                if (string.IsNullOrWhiteSpace(cleaned))
                                {
                                    continue;
                                }

                                // ExpressionText / nested expression attrs belong to the parent activity.
                                if (isExpressionElement
                                    || string.Equals(attrName, "ExpressionText", StringComparison.OrdinalIgnoreCase))
                                {
                                    expressionOnlyValues.Add(cleaned);
                                }
                                else
                                {
                                    argumentValues.Add(cleaned);
                                }
                            }

                            reader.MoveToElement();
                        }

                        if (expressionOnlyValues.Count > 0 && lastActivitySinkIndex >= 0 && lastActivitySinkIndex < sink.Count)
                        {
                            GlobalFindEntry parentEntry = sink[lastActivitySinkIndex];
                            if (parentEntry.ArgumentValues == null)
                            {
                                parentEntry.ArgumentValues = new List<string>();
                            }

                            foreach (string value in expressionOnlyValues)
                            {
                                if (!parentEntry.ArgumentValues.Any(v =>
                                    string.Equals(v, value, StringComparison.Ordinal)))
                                {
                                    parentEntry.ArgumentValues.Add(value);
                                }
                            }
                        }

                        // Property elements like TraceableTryCatchActivity.Try / .Catch are not activities.
                        bool skipType = ShouldSkipElement(localName)
                                        || IsPropertyElementName(localName)
                                        || isExpressionElement;
                        string activityName = StripGeneric(localName);
                        if (!skipType && !string.IsNullOrWhiteSpace(displayName))
                        {
                            skipType = !ActivitySearchFilter.IsNavigableDisplayName(displayName)
                                       || IsPropertyElementName(displayName);
                        }

                        bool include = !skipType
                                       && (!string.IsNullOrWhiteSpace(displayName) || LooksLikeActivityElement(localName));

                        var frame = new PathFrame
                        {
                            LocalName = localName,
                            PushedToPath = false
                        };

                        if (include)
                        {
                            string resolvedDisplay = string.IsNullOrWhiteSpace(displayName)
                                ? activityName
                                : displayName.Trim();

                            pathStack.Add(resolvedDisplay);
                            frame.PushedToPath = true;

                            string displayPath = ActivityDisplayPathBuilder.FormatDisplayNameChain(pathStack);
                            sink.Add(new GlobalFindEntry
                            {
                                Workflow = workflow,
                                ProjectName = projectName,
                                WorkflowName = workflowName,
                                DisplayName = resolvedDisplay,
                                ActivityName = activityName,
                                DisplayPath = displayPath,
                                ArgumentValues = argumentValues
                            });
                            lastActivitySinkIndex = sink.Count - 1;

                            if (isEmpty)
                            {
                                pathStack.RemoveAt(pathStack.Count - 1);
                                frame.PushedToPath = false;
                            }
                        }

                        if (!isEmpty)
                        {
                            elementStack.Push(frame);
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA)
                    {
                        string text = (reader.Value ?? string.Empty).Trim();
                        if (text.Length > 0 && text.Length <= 500 && lastActivitySinkIndex >= 0
                            && lastActivitySinkIndex < sink.Count)
                        {
                            GlobalFindEntry last = sink[lastActivitySinkIndex];
                            if (last.ArgumentValues == null)
                            {
                                last.ArgumentValues = new List<string>();
                            }

                            string cleaned = Unquote(text);
                            if (!last.ArgumentValues.Any(v => string.Equals(v, cleaned, StringComparison.Ordinal)))
                            {
                                last.ArgumentValues.Add(cleaned);
                            }
                        }
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        if (elementStack.Count > 0)
                        {
                            PathFrame frame = elementStack.Pop();
                            if (frame != null && frame.PushedToPath && pathStack.Count > 0)
                            {
                                pathStack.RemoveAt(pathStack.Count - 1);
                            }
                        }
                    }
                }
            }
        }

        private sealed class PathFrame
        {
            public string LocalName { get; set; }
            public bool PushedToPath { get; set; }
        }

        private static bool IsPropertyElementName(string name)
        {
            // XAML property-element syntax: TypeName.Try / TypeName.Catch / …
            return !string.IsNullOrEmpty(name) && name.IndexOf('.') >= 0;
        }

        private static bool IsExpressionElementName(string localName)
        {
            if (string.IsNullOrEmpty(localName))
            {
                return true;
            }

            string bare = StripGeneric(localName);
            if (string.Equals(bare, "Literal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(bare, "LambdaValue", StringComparison.OrdinalIgnoreCase)
                || string.Equals(bare, "LambdaReference", StringComparison.OrdinalIgnoreCase)
                || string.Equals(bare, "LocationReferenceValue", StringComparison.OrdinalIgnoreCase)
                || bare.StartsWith("VisualBasic", StringComparison.OrdinalIgnoreCase)
                || bare.StartsWith("CSharp", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            bool valueOrRef = bare.EndsWith("Value", StringComparison.Ordinal)
                              || bare.EndsWith("Reference", StringComparison.Ordinal);
            if (!valueOrRef)
            {
                return false;
            }

            return bare.IndexOf("Argument", StringComparison.OrdinalIgnoreCase) >= 0
                   || bare.IndexOf("Variable", StringComparison.OrdinalIgnoreCase) >= 0
                   || bare.IndexOf("Field", StringComparison.OrdinalIgnoreCase) >= 0
                   || bare.IndexOf("Property", StringComparison.OrdinalIgnoreCase) >= 0
                   || bare.IndexOf("ArrayItem", StringComparison.OrdinalIgnoreCase) >= 0
                   || bare.IndexOf("DelegateArgument", StringComparison.OrdinalIgnoreCase) >= 0
                   || bare.IndexOf("LocationReference", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ShouldSkipElement(string localName)
        {
            if (string.IsNullOrEmpty(localName))
            {
                return true;
            }

            if (SkipElementNames.Contains(localName))
            {
                return true;
            }

            if (localName.StartsWith("x:", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool LooksLikeActivityElement(string localName)
        {
            if (string.IsNullOrEmpty(localName) || ShouldSkipElement(localName))
            {
                return false;
            }

            // Heuristic: activity type names are PascalCase identifiers, not lowercase helpers.
            char c = localName[0];
            return char.IsUpper(c);
        }

        private static string StripGeneric(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            int tick = name.IndexOf('`');
            return tick > 0 ? name.Substring(0, tick) : name;
        }

        private static string Unquote(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length < 2)
            {
                return text;
            }

            if ((text[0] == '"' && text[text.Length - 1] == '"')
                || (text[0] == '\'' && text[text.Length - 1] == '\''))
            {
                return text.Substring(1, text.Length - 2);
            }

            return text;
        }
    }
}
