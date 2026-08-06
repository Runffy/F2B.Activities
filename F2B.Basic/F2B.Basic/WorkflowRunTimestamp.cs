using OpenRPA.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace F2B.Basic
{
    /// <summary>
    /// Shared second-precision timestamp for aligning LogMessage csv names and Runtime folders
    /// within the same workflow instance. Non-Second runtime modes do not participate.
    /// </summary>
    internal static class WorkflowRunTimestamp
    {
        private static readonly ConcurrentDictionary<string, string> SecondStampsByInstanceId =
            new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Regex LogFileStampRegex = new Regex(
            @"^\[(?<stamp>\d{14})\]",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex RuntimeFolderStampRegex = new Regex(
            @"^(?<stamp>\d{14})$",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public const string SecondStampFormat = "yyyyMMddHHmmss";

        public static string GetOrCreateSecondStamp(string workflowInstanceId)
        {
            string key = NormalizeKey(workflowInstanceId);
            return SecondStampsByInstanceId.GetOrAdd(key, _ => DateTime.Now.ToString(SecondStampFormat));
        }

        public static bool TryGetSecondStamp(string workflowInstanceId, out string stamp)
        {
            return SecondStampsByInstanceId.TryGetValue(NormalizeKey(workflowInstanceId), out stamp)
                   && !string.IsNullOrWhiteSpace(stamp);
        }

        public static void SetSecondStamp(string workflowInstanceId, string stamp)
        {
            if (string.IsNullOrWhiteSpace(stamp) || stamp.Length != 14)
            {
                return;
            }

            SecondStampsByInstanceId[NormalizeKey(workflowInstanceId)] = stamp;
        }

        public static bool TryParseLogFileStamp(string logFilePath, out string stamp)
        {
            stamp = null;
            if (string.IsNullOrWhiteSpace(logFilePath))
            {
                return false;
            }

            string name = Path.GetFileName(logFilePath);
            Match match = LogFileStampRegex.Match(name ?? string.Empty);
            if (!match.Success)
            {
                return false;
            }

            stamp = match.Groups["stamp"].Value;
            return true;
        }

        public static bool TryParseRuntimeFolderStamp(string runtimeDirectory, out string stamp)
        {
            stamp = null;
            if (string.IsNullOrWhiteSpace(runtimeDirectory))
            {
                return false;
            }

            string name = Path.GetFileName(runtimeDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            Match match = RuntimeFolderStampRegex.Match(name ?? string.Empty);
            if (!match.Success)
            {
                return false;
            }

            stamp = match.Groups["stamp"].Value;
            return true;
        }

        public static bool TryParseStampDateTime(string stamp, out DateTime value)
        {
            return DateTime.TryParseExact(
                stamp,
                SecondStampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out value);
        }

        private static string NormalizeKey(string workflowInstanceId)
        {
            return string.IsNullOrWhiteSpace(workflowInstanceId)
                ? string.Empty
                : workflowInstanceId.Trim();
        }
    }
}
