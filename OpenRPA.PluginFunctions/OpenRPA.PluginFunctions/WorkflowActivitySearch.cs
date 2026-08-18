using System;
using System.Activities;
using System.Activities.Presentation.Model;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using OpenRPA.Interfaces;

namespace OpenRPA.PluginFunctions
{
    internal sealed class WorkflowFindItem
    {
        public ModelItem ModelItem { get; set; }
        public string DisplayName { get; set; }
        public string ActivityName { get; set; }
        public string MatchHint { get; set; }
        public ImageSource Icon { get; set; }
        public int Score { get; set; }
    }

    /// <summary>
    /// Searches activities inside the currently open workflow designer.
    /// </summary>
    internal static class WorkflowActivitySearch
    {
        public static IReadOnlyList<WorkflowFindItem> Search(IDesigner designer, string pattern, int maxResults = 40)
        {
            var results = new List<WorkflowFindItem>();
            if (designer == null)
            {
                return results;
            }

            string needle = (pattern ?? string.Empty).Trim();
            List<ModelItem> activities = ActivityInsertService.GetAllActivities(designer);
            foreach (ModelItem item in activities)
            {
                if (item == null || item.ItemType == null)
                {
                    continue;
                }

                // Skip DynamicActivity / ActivityBuilder / expression Literals etc.
                if (!ActivitySearchFilter.IsNavigableActivity(item.ItemType))
                {
                    continue;
                }

                string displayName = ActivityInsertService.GetDisplayName(item) ?? string.Empty;
                if (!ActivitySearchFilter.IsNavigableDisplayName(displayName))
                {
                    continue;
                }

                string activityName = ResolveActivityName(item);
                List<string> argumentValues = CollectArgumentValues(item);

                int score = ScoreItem(displayName, activityName, argumentValues, needle);
                if (score <= 0 && needle.Length > 0)
                {
                    continue;
                }

                if (needle.Length == 0)
                {
                    score = 1;
                }

                string activityPath = ActivityDisplayPathBuilder.BuildFromModelItem(item);
                string matchHint = !string.IsNullOrWhiteSpace(activityPath)
                    ? activityPath
                    : BuildMatchHint(displayName, activityName, argumentValues, needle);

                // If the hit came from an argument value, append a short snippet after the path.
                if (!string.IsNullOrWhiteSpace(activityPath) && needle.Length > 0 && argumentValues != null)
                {
                    bool nameHit = (!string.IsNullOrEmpty(displayName)
                                    && displayName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                                   || (!string.IsNullOrEmpty(activityName)
                                       && activityName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!nameHit)
                    {
                        foreach (string value in argumentValues)
                        {
                            if (!string.IsNullOrEmpty(value)
                                && value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                string shortValue = value.Length > 60 ? value.Substring(0, 57) + "..." : value;
                                matchHint = activityPath + " · " + shortValue;
                                break;
                            }
                        }
                    }
                }

                results.Add(new WorkflowFindItem
                {
                    ModelItem = item,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? activityName : displayName,
                    ActivityName = activityName,
                    MatchHint = matchHint,
                    Icon = ActivityIconResolver.Resolve(item.ItemType),
                    Score = score
                });
            }

            return results
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .ToList();
        }

        private static string ResolveActivityName(ModelItem item)
        {
            if (item == null || item.ItemType == null)
            {
                return string.Empty;
            }

            string name = item.ItemType.Name ?? string.Empty;
            int tick = name.IndexOf('`');
            if (tick > 0)
            {
                name = name.Substring(0, tick);
            }

            return name;
        }

        private static List<string> CollectArgumentValues(ModelItem item)
        {
            var values = new List<string>();
            if (item == null || item.Properties == null)
            {
                return values;
            }

            try
            {
                foreach (ModelProperty prop in item.Properties)
                {
                    if (prop == null || string.IsNullOrEmpty(prop.Name))
                    {
                        continue;
                    }

                    // Skip structural / designer noise.
                    if (IsSkippedProperty(prop.Name))
                    {
                        continue;
                    }

                    object computed = null;
                    try
                    {
                        computed = prop.ComputedValue;
                    }
                    catch
                    {
                        continue;
                    }

                    if (computed == null)
                    {
                        continue;
                    }

                    if (computed is Argument)
                    {
                        AddArgumentText(values, computed);
                        continue;
                    }

                    // Dictionary&lt;string, Argument&gt; (e.g. Invoke Workflow Arguments)
                    var dict = computed as IDictionary;
                    if (dict != null)
                    {
                        foreach (DictionaryEntry entry in dict)
                        {
                            if (entry.Value is Argument)
                            {
                                AddArgumentText(values, entry.Value);
                            }
                            else if (entry.Value != null)
                            {
                                string text = Convert.ToString(entry.Value);
                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    values.Add(text.Trim());
                                }
                            }
                        }

                        continue;
                    }

                    Type type = computed.GetType();
                    if (IsArgumentType(type))
                    {
                        AddArgumentText(values, computed);
                    }
                }
            }
            catch
            {
            }

            return values;
        }

        private static bool IsSkippedProperty(string name)
        {
            return string.Equals(name, "DisplayName", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Id", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Constraints", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Implementation", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Body", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Handler", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Activities", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Variables", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Nodes", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Try", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Catch", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Finally", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Then", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(name, "Else", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsArgumentType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            if (typeof(Argument).IsAssignableFrom(type))
            {
                return true;
            }

            string full = type.FullName ?? string.Empty;
            return full.StartsWith("System.Activities.InArgument", StringComparison.Ordinal)
                   || full.StartsWith("System.Activities.OutArgument", StringComparison.Ordinal)
                   || full.StartsWith("System.Activities.InOutArgument", StringComparison.Ordinal);
        }

        private static void AddArgumentText(List<string> values, object argument)
        {
            if (argument == null)
            {
                return;
            }

            try
            {
                PropertyInfo expressionProp = argument.GetType().GetProperty(
                    "Expression",
                    BindingFlags.Public | BindingFlags.Instance);
                object expression = expressionProp != null ? expressionProp.GetValue(argument, null) : null;
                if (expression != null)
                {
                    PropertyInfo textProp = expression.GetType().GetProperty(
                        "ExpressionText",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (textProp != null)
                    {
                        string text = textProp.GetValue(expression, null) as string;
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            values.Add(Unquote(text.Trim()));
                            return;
                        }
                    }

                    PropertyInfo valueProp = expression.GetType().GetProperty(
                        "Value",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (valueProp != null)
                    {
                        object value = valueProp.GetValue(expression, null);
                        if (value != null)
                        {
                            string text = Convert.ToString(value);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                values.Add(text.Trim());
                                return;
                            }
                        }
                    }
                }

                string fallback = Convert.ToString(argument);
                if (!string.IsNullOrWhiteSpace(fallback)
                    && fallback.IndexOf("System.Activities", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    values.Add(fallback.Trim());
                }
            }
            catch
            {
            }
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

        private static int ScoreItem(
            string displayName,
            string activityName,
            IList<string> argumentValues,
            string needle)
        {
            if (string.IsNullOrEmpty(needle))
            {
                return 1;
            }

            int best = 0;
            best = Math.Max(best, ScoreText(displayName, needle, 1000, 800, 500));
            best = Math.Max(best, ScoreText(activityName, needle, 900, 700, 400));

            if (argumentValues != null)
            {
                foreach (string value in argumentValues)
                {
                    best = Math.Max(best, ScoreText(value, needle, 600, 450, 250));
                }
            }

            return best;
        }

        private static int ScoreText(string text, string needle, int exact, int starts, int contains)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            if (string.Equals(text, needle, StringComparison.OrdinalIgnoreCase))
            {
                return exact;
            }

            if (text.StartsWith(needle, StringComparison.OrdinalIgnoreCase))
            {
                return starts - Math.Min(100, text.Length);
            }

            int idx = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return contains - idx;
            }

            return 0;
        }

        private static string BuildMatchHint(
            string displayName,
            string activityName,
            IList<string> argumentValues,
            string needle)
        {
            if (string.IsNullOrEmpty(needle))
            {
                return activityName;
            }

            if (!string.IsNullOrEmpty(activityName)
                && activityName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
                && (string.IsNullOrEmpty(displayName)
                    || displayName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0))
            {
                return activityName;
            }

            if (argumentValues != null)
            {
                foreach (string value in argumentValues)
                {
                    if (!string.IsNullOrEmpty(value)
                        && value.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string shortValue = value.Length > 80 ? value.Substring(0, 77) + "..." : value;
                        return activityName + " · " + shortValue;
                    }
                }
            }

            return activityName;
        }

        internal static int ScoreMatch(
            string displayName,
            string activityName,
            IList<string> argumentValues,
            string needle)
        {
            return ScoreItem(displayName, activityName, argumentValues, needle);
        }

        internal static string FormatMatchHint(
            string displayName,
            string activityName,
            IList<string> argumentValues,
            string needle)
        {
            return BuildMatchHint(displayName, activityName, argumentValues, needle);
        }
    }
}
