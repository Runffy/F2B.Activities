using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using F2B.Forms.Model;

namespace F2B.Forms.Engine
{
    /// <summary>
    /// VersionLabel helpers: compose display text and bump form-wide <c>yyyy.MM.dd.batch</c> on dirty Designer saves.
    /// </summary>
    public static class VersionLabelUtil
    {
        public const string DefaultPrefix = "UI Version ";

        private static readonly Regex VersionRegex = new Regex(
            @"^(?<y>\d{4})\.(?<m>\d{2})\.(?<d>\d{2})\.(?<batch>\d+)$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static string Compose(string prefix, string version)
        {
            string p = prefix ?? string.Empty;
            if (string.IsNullOrEmpty(version))
            {
                return p;
            }

            return p + version;
        }

        /// <summary>
        /// Next version for a dirty save. Empty/invalid → today.01;
        /// same calendar day → batch+1; otherwise today.01.
        /// </summary>
        public static string Bump(string currentVersion, DateTime today)
        {
            DateTime day = today.Date;
            if (!TryParse(currentVersion, out DateTime versionDate, out int batch))
            {
                return Format(day, 1);
            }

            if (versionDate.Date == day)
            {
                return Format(day, batch + 1);
            }

            return Format(day, 1);
        }

        /// <summary>
        /// Compare two version strings. Positive if <paramref name="left"/> is newer.
        /// Invalid versions sort below valid ones.
        /// </summary>
        public static int Compare(string left, string right)
        {
            bool leftOk = TryParse(left, out DateTime leftDate, out int leftBatch);
            bool rightOk = TryParse(right, out DateTime rightDate, out int rightBatch);
            if (!leftOk && !rightOk)
            {
                return 0;
            }

            if (!leftOk)
            {
                return -1;
            }

            if (!rightOk)
            {
                return 1;
            }

            int dateCmp = leftDate.Date.CompareTo(rightDate.Date);
            if (dateCmp != 0)
            {
                return dateCmp;
            }

            return leftBatch.CompareTo(rightBatch);
        }

        /// <summary>
        /// Prefer form <c>uiVersion</c>; otherwise the newest VersionLabel version found in the tree.
        /// </summary>
        public static string ResolveSharedVersion(string formUiVersion, IEnumerable<string> labelVersions)
        {
            string best = null;
            if (TryParse(formUiVersion, out _, out _))
            {
                best = formUiVersion.Trim();
            }

            if (labelVersions != null)
            {
                foreach (string candidate in labelVersions)
                {
                    if (!TryParse(candidate, out _, out _))
                    {
                        continue;
                    }

                    if (best == null || Compare(candidate, best) > 0)
                    {
                        best = candidate.Trim();
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// Collect VersionLabel versions under a control tree (including nested containers).
        /// </summary>
        public static void CollectLabelVersions(
            IEnumerable<ControlDefinition> controls,
            IList<string> into)
        {
            if (controls == null || into == null)
            {
                return;
            }

            foreach (ControlDefinition control in controls)
            {
                if (control == null)
                {
                    continue;
                }

                if (FormControlType.IsVersionLabel(control.Type))
                {
                    ResolveFromDefinition(control, out _, out string version);
                    if (!string.IsNullOrEmpty(version))
                    {
                        into.Add(version);
                    }
                }

                if (control.Controls != null && control.Controls.Count > 0)
                {
                    CollectLabelVersions(control.Controls, into);
                }
            }
        }

        /// <summary>
        /// Align every VersionLabel to the shared form version (prefix unchanged).
        /// Updates <see cref="FormDefinition.UiVersion"/> and each control's <c>version</c>/<c>text</c>.
        /// </summary>
        public static void ApplySharedVersionToForm(FormDefinition definition, string sharedVersion)
        {
            if (definition == null)
            {
                return;
            }

            definition.UiVersion = string.IsNullOrWhiteSpace(sharedVersion) ? null : sharedVersion.Trim();
            ApplySharedVersionToControls(definition.Controls, definition.UiVersion);
        }

        public static void ApplySharedVersionToControls(
            IEnumerable<ControlDefinition> controls,
            string sharedVersion)
        {
            if (controls == null)
            {
                return;
            }

            foreach (ControlDefinition control in controls)
            {
                if (control == null)
                {
                    continue;
                }

                if (FormControlType.IsVersionLabel(control.Type))
                {
                    ResolveFromDefinition(control, out string prefix, out _);
                    control.Prefix = prefix ?? DefaultPrefix;
                    control.Version = sharedVersion;
                    control.Text = Compose(control.Prefix, sharedVersion);
                }

                if (control.Controls != null && control.Controls.Count > 0)
                {
                    ApplySharedVersionToControls(control.Controls, sharedVersion);
                }
            }
        }

        /// <summary>
        /// Resolve shared version from form + labels, then write it onto the form and every VersionLabel.
        /// </summary>
        public static string NormalizeFormUiVersion(FormDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            var labelVersions = new List<string>();
            CollectLabelVersions(definition.Controls, labelVersions);
            string shared = ResolveSharedVersion(definition.UiVersion, labelVersions);
            if (labelVersions.Count > 0 || HasAnyVersionLabel(definition.Controls))
            {
                ApplySharedVersionToForm(definition, shared);
            }

            return shared;
        }

        private static bool HasAnyVersionLabel(IEnumerable<ControlDefinition> controls)
        {
            if (controls == null)
            {
                return false;
            }

            foreach (ControlDefinition control in controls)
            {
                if (control == null)
                {
                    continue;
                }

                if (FormControlType.IsVersionLabel(control.Type))
                {
                    return true;
                }

                if (HasAnyVersionLabel(control.Controls))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryParse(string version, out DateTime date, out int batch)
        {
            date = default(DateTime);
            batch = 0;
            if (string.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            Match match = VersionRegex.Match(version.Trim());
            if (!match.Success)
            {
                return false;
            }

            int y = int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture);
            int m = int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture);
            int d = int.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture);
            batch = int.Parse(match.Groups["batch"].Value, CultureInfo.InvariantCulture);
            if (batch < 1)
            {
                return false;
            }

            try
            {
                date = new DateTime(y, m, d);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }

            return true;
        }

        public static string Format(DateTime date, int batch)
        {
            return date.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture)
                + "."
                + batch.ToString("D2", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Resolve prefix/version from definition fields; fall back to parsing <c>text</c> when needed.
        /// </summary>
        public static void ResolveFromDefinition(
            ControlDefinition definition,
            out string prefix,
            out string version)
        {
            prefix = definition == null ? null : definition.Prefix;
            version = definition == null ? null : definition.Version;

            if (!string.IsNullOrEmpty(version) && TryParse(version, out _, out _))
            {
                if (prefix == null)
                {
                    prefix = string.Empty;
                }

                return;
            }

            string text = definition == null ? null : definition.Text;
            if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(text)
                && text.StartsWith(prefix, StringComparison.Ordinal)
                && TryParse(text.Substring(prefix.Length), out _, out _))
            {
                version = text.Substring(prefix.Length);
                return;
            }

            if (!string.IsNullOrEmpty(text))
            {
                Match trailing = Regex.Match(
                    text,
                    @"(?<prefix>.*?)(?<ver>\d{4}\.\d{2}\.\d{2}\.\d+)\s*$",
                    RegexOptions.CultureInvariant);
                if (trailing.Success && TryParse(trailing.Groups["ver"].Value, out _, out _))
                {
                    if (prefix == null)
                    {
                        prefix = trailing.Groups["prefix"].Value;
                    }

                    version = trailing.Groups["ver"].Value;
                    return;
                }
            }

            if (prefix == null)
            {
                prefix = string.IsNullOrEmpty(text) ? DefaultPrefix : text;
            }

            version = null;
        }
    }
}
