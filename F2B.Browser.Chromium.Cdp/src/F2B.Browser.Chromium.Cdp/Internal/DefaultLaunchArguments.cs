using System;
using System.Collections.Generic;
using System.Linq;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class DefaultLaunchArguments
    {
        private static readonly string[] CommonDefaults =
        {
            "--no-default-browser-check",
            "--disable-suggestions-ui",
            "--no-first-run",
            "--disable-infobars",
            "--disable-popup-blocking",
            "--hide-crash-restore-bubble",
            "--disable-search-engine-choice-screen",
            "--enterprise-signin-dialog-behavior-for-testing=cancel",
            "--profile-directory=Default"
        };

        private static readonly string[] ChromeDisableFeatures =
        {
            "PrivacySandboxSettings4",
            "SigninPromo",
            "ProfilePicker",
            "ChromeWhatsNewUI",
            "WelcomeExperience"
        };

        private static readonly string[] EdgeDisableFeatures =
        {
            "PrivacySandboxSettings4",
            "SigninPromo",
            "ProfilePicker",
            "ChromeWhatsNewUI",
            "WelcomeExperience",
            "EdgeWelcomePage",
            "EdgeFirstRunExperience",
            "MSEdgeWelcomePage"
        };

        public static IEnumerable<string> MergeWithUserArguments(
            string browserName,
            IEnumerable<string> userArguments)
        {
            var defaults = BuildDefaults(browserName);
            return LaunchArgumentMerger.Merge(defaults, userArguments ?? new string[0]);
        }

        private static IEnumerable<string> BuildDefaults(string browserName)
        {
            var defaults = new List<string>(CommonDefaults);

            if (IsEdge(browserName))
            {
                defaults.Add("--disable-sync");
                defaults.Add(BuildDisableFeaturesArgument(EdgeDisableFeatures));
            }
            else
            {
                defaults.Add(BuildDisableFeaturesArgument(ChromeDisableFeatures));
            }

            return defaults;
        }

        private static string BuildDisableFeaturesArgument(IEnumerable<string> features)
        {
            return "--disable-features=" + string.Join(",", features);
        }

        private static bool IsEdge(string browserName)
        {
            return string.Equals(browserName, "msedge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(browserName, "edge", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class LaunchArgumentMerger
    {
        public static List<string> Merge(IEnumerable<string> defaults, IEnumerable<string> userArguments)
        {
            var orderedKeys = new List<string>();
            var arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var argument in defaults)
            {
                AddArgument(arguments, orderedKeys, argument);
            }

            foreach (var argument in userArguments)
            {
                AddArgument(arguments, orderedKeys, argument);
            }

            return orderedKeys.Select(key => arguments[key]).ToList();
        }

        private static void AddArgument(
            IDictionary<string, string> arguments,
            IList<string> orderedKeys,
            string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
            {
                return;
            }

            var trimmed = argument.Trim();
            var flagName = GetFlagName(trimmed);
            if (string.IsNullOrEmpty(flagName))
            {
                return;
            }

            if (arguments.ContainsKey(flagName))
            {
                if (string.Equals(flagName, "disable-features", StringComparison.OrdinalIgnoreCase))
                {
                    arguments[flagName] = MergeDisableFeatures(arguments[flagName], trimmed);
                    return;
                }

                arguments[flagName] = trimmed;
                return;
            }

            orderedKeys.Add(flagName);
            arguments[flagName] = trimmed;
        }

        private static string MergeDisableFeatures(string existingArgument, string newArgument)
        {
            var existingFeatures = ExtractFeatureValues(existingArgument);
            var newFeatures = ExtractFeatureValues(newArgument);
            var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var feature in existingFeatures)
            {
                merged.Add(feature);
            }

            foreach (var feature in newFeatures)
            {
                merged.Add(feature);
            }

            return "--disable-features=" + string.Join(",", merged.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> ExtractFeatureValues(string argument)
        {
            var equalsIndex = argument.IndexOf('=');
            if (equalsIndex < 0 || equalsIndex >= argument.Length - 1)
            {
                yield break;
            }

            var values = argument.Substring(equalsIndex + 1);
            foreach (var feature in values.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = feature.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    yield return trimmed;
                }
            }
        }

        private static string GetFlagName(string argument)
        {
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                return argument;
            }

            var body = argument.Substring(2);
            var equalsIndex = body.IndexOf('=');
            return equalsIndex >= 0 ? body.Substring(0, equalsIndex) : body;
        }
    }
}
