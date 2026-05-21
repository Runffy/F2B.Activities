using System;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>Frame/document retry policy aligned with Python <c>_is_retryable_frame_error</c>.</summary>
    internal static class IeLocateRetry
    {
        public static bool IsRetryable(Exception ex)
        {
            if (ex == null)
                return false;

            var message = ex.Message ?? string.Empty;
            return message.IndexOf("未找到 frame", StringComparison.Ordinal) >= 0
                || message.IndexOf("当前文档不包含 frames", StringComparison.Ordinal) >= 0
                || message.IndexOf("frame 没有可访问的 document", StringComparison.Ordinal) >= 0
                || message.IndexOf("无法获取 IE Document", StringComparison.Ordinal) >= 0
                || message.IndexOf("WM_HTML_GETOBJECT failed", StringComparison.OrdinalIgnoreCase) >= 0
                || message.IndexOf("IE server HWND is no longer valid", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Re-read MSHTML document from the IE server HWND (like Python <c>refresh_document</c>).</summary>
        public static void RefreshDom(ITridentDomHost window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            try
            {
                window.GetMsHtmlDocument();
            }
            catch
            {
                // best-effort; next poll will throw if still broken
            }
        }
    }
}
