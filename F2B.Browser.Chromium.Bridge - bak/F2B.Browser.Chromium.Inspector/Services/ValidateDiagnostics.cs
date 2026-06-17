using System;
using System.Text;
using F2B.Browser.Chromium.Bridge;
using F2B.Browser.Chromium.Bridge.Selectors;

namespace F2B.Browser.Chromium.Inspector.Services
{
    internal static class ValidateDiagnostics
    {
        public static string BuildFailureSummary(
            BwTab tab,
            BwTabInfo tabInfo,
            BwTab activeTab,
            BwTabInfo activeInfo,
            SelectorScope scope,
            string selectorXml,
            SelectorResolveResult resolveResult,
            BridgeInspectorValidateProbeResult probe,
            Exception probeException)
        {
            var builder = new StringBuilder();
            builder.Append("Validate failed: 0 matches within ")
                .Append(SelectorResolveRetry.DefaultTimeoutMilliseconds)
                .Append("ms.");

            AppendTabContext(builder, tab, tabInfo, activeTab, activeInfo);
            AppendSelectorContext(builder, scope, selectorXml);
            AppendResolveContext(builder, resolveResult);
            AppendProbeContext(builder, probe, probeException, resolveResult);
            AppendHint(builder);
            return builder.ToString();
        }

        public static void LogFailure(
            BwTab tab,
            BwTabInfo tabInfo,
            BwTab activeTab,
            BwTabInfo activeInfo,
            SelectorScope scope,
            string selectorXml,
            SelectorResolveResult resolveResult,
            BridgeInspectorValidateProbeResult probe,
            Exception probeException)
        {
            var message = BuildFailureSummary(
                tab,
                tabInfo,
                activeTab,
                activeInfo,
                scope,
                selectorXml,
                resolveResult,
                probe,
                probeException);

            BridgeFileLog.Write("[INSPECTOR-VALIDATE] " + message.Replace(Environment.NewLine, " | "));
        }

        private static void AppendTabContext(
            StringBuilder builder,
            BwTab tab,
            BwTabInfo tabInfo,
            BwTab activeTab,
            BwTabInfo activeInfo)
        {
            builder.AppendLine();
            builder.Append("Target tabId=")
                .Append(tab?.TabId ?? 0)
                .Append(", title='")
                .Append(tabInfo?.Title ?? tab?.Title ?? string.Empty)
                .Append("', url=")
                .Append(tabInfo?.Url ?? tab?.Url ?? string.Empty);

            if (activeTab != null &&
                (activeTab.TabId != tab?.TabId ||
                 !string.Equals(activeInfo?.Url, tabInfo?.Url, StringComparison.OrdinalIgnoreCase)))
            {
                builder.AppendLine();
                builder.Append("Active tabId=")
                    .Append(activeTab.TabId)
                    .Append(", title='")
                    .Append(activeInfo?.Title ?? activeTab.Title ?? string.Empty)
                    .Append("', url=")
                    .Append(activeInfo?.Url ?? activeTab.Url ?? string.Empty);
            }
        }

        private static void AppendSelectorContext(StringBuilder builder, SelectorScope scope, string selectorXml)
        {
            builder.AppendLine();
            builder.Append("Operation selector: ")
                .Append(SelectorXmlSerializer.ToOperationXml(scope));

            if (scope?.TabLevel != null)
            {
                builder.AppendLine();
                builder.Append("Wnd selector: ")
                    .Append(SelectorXmlSerializer.SerializeLevelTag(scope.TabLevel));
            }

            if (!string.IsNullOrWhiteSpace(selectorXml))
            {
                builder.AppendLine();
                builder.Append("Full selector: ")
                    .Append(selectorXml.Replace("\r\n", " / "));
            }
        }

        private static void AppendResolveContext(StringBuilder builder, SelectorResolveResult resolveResult)
        {
            if (resolveResult == null)
                return;

            builder.AppendLine();
            builder.Append("FindElements attempts=")
                .Append(resolveResult.Attempts);

            if (!string.IsNullOrWhiteSpace(resolveResult.LastError))
            {
                builder.Append(", last RPC error: ")
                    .Append(resolveResult.LastError);
            }
        }

        private static void AppendProbeContext(
            StringBuilder builder,
            BridgeInspectorValidateProbeResult probe,
            Exception probeException,
            SelectorResolveResult resolveResult)
        {
            builder.AppendLine();
            if (probeException != null)
            {
                builder.Append("Page probe failed: ")
                    .Append(probeException.Message);
                return;
            }

            if (probe == null)
            {
                builder.Append("Page probe unavailable.");
                return;
            }

            builder.Append("Page title='")
                .Append(probe.DocumentTitle ?? string.Empty)
                .Append("', href=")
                .Append(probe.LocationHref ?? string.Empty);

            builder.AppendLine();
            builder.Append("Segment resolved=")
                .Append(probe.SegmentResolved ? "yes" : "no");

            if (probe.SegmentResolved)
            {
                builder.Append(" (")
                    .Append(probe.SegmentElementTag ?? string.Empty)
                    .Append("#")
                    .Append(probe.SegmentElementId ?? string.Empty)
                    .Append(")");
            }

            builder.Append(", selector matches=")
                .Append(probe.SelectorMatchCount)
                .Append(", getElementById('")
                .Append(probe.ProbeId ?? string.Empty)
                .Append("')=")
                .Append(probe.GetElementByIdFound ? "found" : "missing");

            if (probe.GetElementByIdFound)
            {
                builder.Append(" (")
                    .Append(probe.GetElementByIdTag ?? string.Empty)
                    .Append(")");
            }

            if (!string.IsNullOrWhiteSpace(probe.FrameError))
            {
                builder.AppendLine();
                builder.Append("Frame error: ")
                    .Append(probe.FrameError);
            }

            if (!string.IsNullOrWhiteSpace(probe.SelectorError))
            {
                builder.AppendLine();
                builder.Append("Selector error: ")
                    .Append(probe.SelectorError);
            }

            if (probe.SegmentResolved && probe.SelectorMatchCount == 0)
            {
                builder.AppendLine();
                builder.Append("Diagnosis: element exists in DOM tree path, but selector matching returned 0 (likely selector property mismatch).");
            }
            else if (!probe.SegmentResolved && !probe.GetElementByIdFound)
            {
                builder.AppendLine();
                builder.Append("Diagnosis: element not found in page DOM (page changed, wrong tab, or SPA not ready).");
            }
            else if (!string.IsNullOrWhiteSpace(resolveResult?.LastError))
            {
                builder.AppendLine();
                builder.Append("Diagnosis: Bridge RPC failed before page matching completed.");
            }
        }

        private static void AppendHint(StringBuilder builder)
        {
            builder.AppendLine();
            builder.Append("Log file: ")
                .Append(BridgeFileLog.LogFilePath);
        }
    }
}
