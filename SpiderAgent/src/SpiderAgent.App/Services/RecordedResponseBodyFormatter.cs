using System.Text;
using System.Text.RegularExpressions;
using SpiderAgent.Core.Recording;

namespace SpiderAgent.App.Services;

/// <summary>
/// 将录制响应体格式化为 Agent 可理解的文本（正确处理二进制 / Base64）。
/// </summary>
public static class RecordedResponseBodyFormatter
{
    private const int MaxTextPreviewChars = 3500;
    private const int MaxBase64PreviewChars = 120;

    public static byte[]? TryGetRawBytes(RecordedRequest request)
    {
        if (string.IsNullOrEmpty(request.ResponseBody))
        {
            return null;
        }

        if (!request.ResponseBodyIsBase64)
        {
            return Encoding.UTF8.GetBytes(request.ResponseBody);
        }

        try
        {
            return Convert.FromBase64String(request.ResponseBody);
        }
        catch
        {
            return null;
        }
    }

    public static bool IsBinaryResponse(RecordedRequest request)
    {
        if (string.IsNullOrEmpty(request.ResponseBody))
        {
            return false;
        }

        var mime = request.MimeType ?? string.Empty;
        var url = request.Url ?? string.Empty;

        if (request.ResponseBodyIsBase64)
        {
            return true;
        }

        if (mime.Contains("octet-stream", StringComparison.OrdinalIgnoreCase)
            || mime.Contains("mpegurl", StringComparison.OrdinalIgnoreCase) == false
               && (url.Contains(".key", StringComparison.OrdinalIgnoreCase)
                   || url.Contains("/key", StringComparison.OrdinalIgnoreCase)))
        {
            return mime.Contains("octet-stream", StringComparison.OrdinalIgnoreCase)
                   || url.Contains(".key", StringComparison.OrdinalIgnoreCase);
        }

        var bytes = TryGetRawBytes(request);
        if (bytes is null or { Length: 0 })
        {
            return false;
        }

        return !IsMostlyText(bytes);
    }

    public static int GetByteLength(RecordedRequest request)
        => TryGetRawBytes(request)?.Length ?? 0;

    public static string DecodeAsText(RecordedRequest request)
    {
        if (string.IsNullOrEmpty(request.ResponseBody))
        {
            return string.Empty;
        }

        if (!request.ResponseBodyIsBase64)
        {
            return request.ResponseBody;
        }

        var bytes = TryGetRawBytes(request);
        if (bytes is null)
        {
            return request.ResponseBody;
        }

        return Encoding.UTF8.GetString(bytes);
    }

    public static string FormatPreview(RecordedRequest request, int maxChars = MaxTextPreviewChars)
    {
        if (string.IsNullOrEmpty(request.ResponseBody))
        {
            return string.Empty;
        }

        if (IsBinaryResponse(request))
        {
            return FormatBinaryPreview(request);
        }

        var text = DecodeAsText(request);
        return text.Length <= maxChars ? text : text[..maxChars];
    }

    public static string FormatBinaryPreview(RecordedRequest request)
    {
        var bytes = TryGetRawBytes(request);
        if (bytes is null or { Length: 0 })
        {
            return "[二进制响应，但无法解析内容]";
        }

        var mime = request.MimeType ?? "(unknown)";
        var hexHead = BitConverter.ToString(bytes.Take(Math.Min(16, bytes.Length)).ToArray());
        var base64Head = Convert.ToBase64String(bytes.Take(Math.Min(48, bytes.Length)).ToArray());
        if (base64Head.Length > MaxBase64PreviewChars)
        {
            base64Head = base64Head[..MaxBase64PreviewChars];
        }

        return $"""
            [二进制响应 — 录制中已保存为 Base64，共 {bytes.Length} 字节]
            MIME: {mime}
            Hex(前16字节): {hexHead}
            Base64(前48字节): {base64Head}
            说明: 此类响应（如 HLS AES key）不能用 UTF-8 文本解析；生成代码时应使用 `base64.b64decode(...)` 还原 key 字节。
            """;
    }

    public static IReadOnlyList<HlsEncryptionHint> DetectHlsEncryption(IEnumerable<RecordedRequest> requests)
    {
        var hints = new List<HlsEncryptionHint>();

        foreach (var request in requests)
        {
            var text = DecodeAsText(request);
            if (string.IsNullOrWhiteSpace(text)
                || !text.Contains("#EXTM3U", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (Match match in Regex.Matches(
                         text,
                         "#EXT-X-KEY:METHOD=([^,]+)(?:,URI=\"([^\"]+)\")?",
                         RegexOptions.IgnoreCase))
            {
                hints.Add(new HlsEncryptionHint(
                    request.Url,
                    match.Groups[1].Value,
                    match.Groups[2].Success ? match.Groups[2].Value : null));
            }
        }

        return hints;
    }

    public static IReadOnlyList<RecordedRequest> FindLikelyHlsKeyRequests(IEnumerable<RecordedRequest> requests)
        => requests
            .Where(r => IsBinaryResponse(r)
                        && GetByteLength(r) is > 0 and <= 512
                        && ((r.Url?.Contains(".key", StringComparison.OrdinalIgnoreCase) ?? false)
                            || (r.MimeType?.Contains("octet-stream", StringComparison.OrdinalIgnoreCase) ?? false)))
            .ToList();

    private static bool IsMostlyText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
        {
            return true;
        }

        int printable = 0;
        foreach (var b in bytes)
        {
            if (b is 9 or 10 or 13 or >= 32 and <= 126)
            {
                printable++;
            }
        }

        return printable * 100 / bytes.Length >= 85;
    }

    public sealed record HlsEncryptionHint(string M3u8Url, string Method, string? KeyUri);
}
