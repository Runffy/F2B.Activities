using System.Text;
using System.Text.Json;
using SpiderAgent.Core.Recording;

namespace SpiderAgent.App.Services;

/// <summary>
/// 从录制数据自动构建「目标请求 → 参数溯源 → 请求链路」逆向分析草案，供 Agent 参考。
/// </summary>
public sealed class RequestChainAnalyzer
{
    private static readonly HashSet<string> UserInputParamNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "wd", "q", "query", "keyword", "search", "searchword", "key", "text", "username", "user",
        "password", "pass", "account", "phone", "email", "code", "captcha"
    };

    private static readonly HashSet<string> SkippedResourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Image", "Font", "Media", "Ping", "Script", "Stylesheet"
    };

    public RequestChainAnalysis Analyze(RecordingSession session, string? userPrompt = null)
    {
        var ordered = session.Requests
            .Where(r => !SkippedResourceTypes.Contains(r.ResourceType ?? string.Empty))
            .OrderBy(r => r.Timestamp)
            .ToList();

        var terminals = IdentifyTerminalRequests(ordered).Take(3).ToList();
        var traces = new List<ParameterTrace>();

        foreach (var terminal in terminals)
        {
            traces.AddRange(TraceParameters(terminal, ordered, userPrompt));
        }

        var chain = BuildMinimalChain(ordered, terminals, traces);

        return new RequestChainAnalysis
        {
            TerminalRequests = terminals,
            ParameterTraces = traces,
            ChainRequestIndices = chain
        };
    }

    public string ToMarkdown(RequestChainAnalysis analysis, IReadOnlyList<RecordedRequest> orderedRequests)
    {
        var builder = new StringBuilder();

        builder.AppendLine("## 自动逆向分析草案（由 SpiderAgent 预计算，请在此基础上完善并生成代码）");
        builder.AppendLine();

        builder.AppendLine("### 1. 目标请求（终端请求，优先从这里开始逆向）");
        if (analysis.TerminalRequests.Count == 0)
        {
            builder.AppendLine("（未能自动识别，请根据用户描述在录制中手动定位）");
        }
        else
        {
            foreach (var terminal in analysis.TerminalRequests)
            {
                builder.AppendLine($"- **[{terminal.Index + 1}] {terminal.Method} {Truncate(terminal.Url, 180)}**");
                builder.AppendLine($"  - 类型: {terminal.ResourceType}, 状态: {terminal.StatusCode}, 响应约 **{terminal.ResponseLength:N0}** 字符");
                builder.AppendLine($"  - 识别理由: {terminal.Reason}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### 2. 参数溯源表（目标请求及关联请求的关键字段）");
        builder.AppendLine("| 请求 | 位置 | 参数名 | 录制值(截断) | 分类 | 溯源线索 |");
        builder.AppendLine("|------|------|--------|--------------|------|----------|");

        foreach (var trace in analysis.ParameterTraces.Take(80))
        {
            var value = Truncate(trace.Value, 48).Replace("|", "\\|");
            var hint = Truncate(trace.SourceHint, 60).Replace("|", "\\|");
            builder.AppendLine($"| [{trace.RequestIndex + 1}] | {trace.Location} | `{trace.Name}` | `{value}` | {trace.Category} | {hint} |");
        }

        builder.AppendLine();
        builder.AppendLine("### 3. 参数分类说明");
        builder.AppendLine("- **用户输入**: 应对应 Prompt 中用户描述的可变输入（搜索词、账号等），代码中做成变量");
        builder.AppendLine("- **固定值**: 仅用于真正不变的常量（如 ie=utf-8）；`rsv_*`/`isid` 等**不属于固定值**");
        builder.AppendLine("- **前序响应**: 值出现在更早某请求的响应体/Set-Cookie 中，需先请求该接口并提取");
        builder.AppendLine("- **Session/Cookie**: 由 Session 按链路顺序维护");
        builder.AppendLine("- **待追溯(动态)**: 跟踪 token，须同会话从前序步骤获取或等价生成，**禁止**写死录制快照");
        builder.AppendLine("- **录制快照固定值（来源未知）**: **重要** — 该参数未能归类到「前序响应 / JS或HTML固定值 / 可复现JS计算」三种标准来源。生成代码时必须将其单独提取为模块级常量，并添加醒目的注释，明确说明「该值不属于上述三种情况，来自录制快照」。");
        builder.AppendLine();
        builder.AppendLine("> 完整复现请求链、响应体大小校验、录制缺口见下方「会话洞察」章节。");

        return builder.ToString();
    }

    private static List<TerminalRequestCandidate> IdentifyTerminalRequests(List<RecordedRequest> ordered)
    {
        return ordered
            .Select((request, index) => new { request, index })
            .Where(x => IsApiLike(x.request))
            .Select(x => new TerminalRequestCandidate
            {
                Index = x.index,
                Request = x.request,
                Method = x.request.Method,
                Url = x.request.Url,
                ResourceType = x.request.ResourceType ?? "",
                StatusCode = x.request.StatusCode,
                ResponseLength = GetResponseLength(x.request),
                Score = ScoreTerminal(x.request),
                Reason = DescribeTerminalReason(x.request)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.ResponseLength)
            .DistinctBy(x => NormalizeUrlPath(x.Url))
            .ToList();
    }

    private static int ScoreTerminal(RecordedRequest request)
    {
        var score = 0;
        var url = request.Url ?? "";
        var bodyLen = GetResponseLength(request);

        if (bodyLen > 50_000)
        {
            score += 40;
        }
        else if (bodyLen > 5_000)
        {
            score += 25;
        }
        else if (bodyLen > 500)
        {
            score += 10;
        }

        if (IsHtmlResponse(request))
        {
            score += 30;
        }

        if (IsJsonResponse(request))
        {
            score += 20;
        }

        if (url.Contains("/s?", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("search", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("query", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("list", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("api/", StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
        }

        var type = request.ResourceType ?? "";
        if (type.Equals("XHR", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("Fetch", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("Document", StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
        }

        return score;
    }

    private static string DescribeTerminalReason(RecordedRequest request)
    {
        var parts = new List<string>();
        var bodyLen = GetResponseLength(request);

        if (IsHtmlResponse(request) && bodyLen > 10_000)
        {
            parts.Add("大体积 HTML 响应，可能是结果页/列表页");
        }

        if (IsJsonResponse(request))
        {
            parts.Add("JSON API 响应");
        }

        var url = request.Url ?? "";
        if (url.Contains("search", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("/s?", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("URL 含搜索特征");
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add("POST 提交型接口");
        }

        return parts.Count > 0 ? string.Join("；", parts) : "API 类请求";
    }

    private static List<ParameterTrace> TraceParameters(
        TerminalRequestCandidate terminal,
        List<RecordedRequest> ordered,
        string? userPrompt)
    {
        var request = terminal.Request;
        var traces = new List<ParameterTrace>();
        var priorResponses = ordered
            .Take(terminal.Index)
            .Select((r, i) => (Index: i, Body: DecodeResponseBody(r), Headers: r.ResponseHeadersJson))
            .ToList();

        foreach (var (name, value) in ExtractQueryParams(request.Url))
        {
            traces.Add(TraceOne(terminal.Index, "query", name, value, priorResponses, userPrompt));
        }

        foreach (var (name, value) in ParseHeadersJson(request.RequestHeadersJson))
        {
            if (name.Equals("User-Agent", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Accept", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Accept-Language", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            traces.Add(TraceOne(terminal.Index, "header", name, value, priorResponses, userPrompt));
        }

        foreach (var (name, value) in ExtractBodyParams(request.RequestBody))
        {
            traces.Add(TraceOne(terminal.Index, "body", name, value, priorResponses, userPrompt));
        }

        return traces;
    }

    private static ParameterTrace TraceOne(
        int requestIndex,
        string location,
        string name,
        string value,
        List<(int Index, string Body, string? Headers)> priorResponses,
        string? userPrompt)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new ParameterTrace
            {
                RequestIndex = requestIndex,
                Location = location,
                Name = name,
                Value = value,
                Category = "空值",
                SourceHint = "可省略或观察是否必填"
            };
        }

        if (UserInputParamNames.Contains(name) ||
            LooksLikeUserKeyword(value, userPrompt))
        {
            return new ParameterTrace
            {
                RequestIndex = requestIndex,
                Location = location,
                Name = name,
                Value = value,
                Category = "用户输入",
                SourceHint = "对应用户 Prompt 中的可变输入，代码中参数化"
            };
        }

        var priorHitIndex = -1;
        for (var i = priorResponses.Count - 1; i >= 0; i--)
        {
            var prior = priorResponses[i];
            if (prior.Body.Contains(value, StringComparison.Ordinal) ||
                (prior.Headers?.Contains(value, StringComparison.Ordinal) ?? false))
            {
                priorHitIndex = prior.Index;
                break;
            }
        }

        if (priorHitIndex >= 0)
        {
            return new ParameterTrace
            {
                RequestIndex = requestIndex,
                Location = location,
                Name = name,
                Value = value,
                Category = "前序响应",
                SourceHint = $"值出现在请求 [{priorHitIndex + 1}] 的响应中，需先调用并提取"
            };
        }

        if (name.Contains("cookie", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("Referer", StringComparison.OrdinalIgnoreCase))
        {
            return new ParameterTrace
            {
                RequestIndex = requestIndex,
                Location = location,
                Name = name,
                Value = value,
                Category = "Session/上下文",
                SourceHint = "由 Session 按链路顺序自动携带或从上一步 URL 构造"
            };
        }

        if (RecordingSessionInsightsBuilder.IsDynamicTrackingParam(name) ||
            name.Equals("Ps-Dataurlconfigqid", StringComparison.OrdinalIgnoreCase))
        {
            var hint = name.ToLowerInvariant() switch
            {
                "rsv_pq" or "ps-dataurlconfigqid" =>
                    "与 sugrec/pc_his 返回的 queryid 同步；见会话洞察 §12/§13",
                "isid" => "会话 id；前序响应无则 secrets.token_hex(8).upper() 生成",
                "rsv_t" => "会话 token；前序响应无则 secrets.token_urlsafe(32) 生成",
                _ => "跟踪/会话参数，须从前序响应/Cookie 提取或 §13 等价生成，禁止写死录制值"
            };

            return new ParameterTrace
            {
                RequestIndex = requestIndex,
                Location = location,
                Name = name,
                Value = value,
                Category = "待追溯(动态)",
                SourceHint = hint
            };
        }

        if (IsLikelyFixedToken(value))
        {
            return new ParameterTrace
            {
                RequestIndex = requestIndex,
                Location = location,
                Name = name,
                Value = value,
                Category = "固定值",
                SourceHint = "录制中静态出现，可暂时写死并注释来源"
            };
        }

        // 无法归类到“前序响应 / 固定值 / 可复现JS计算 / 已知动态参数”的情况
        // 这类参数应被Agent作为“录制快照固定值”处理，并在代码中加注释说明来源不明
        return new ParameterTrace
        {
            RequestIndex = requestIndex,
            Location = location,
            Name = name,
            Value = value,
            Category = "录制快照固定值（来源未知）",
            SourceHint = "未在前序响应、HTML/JS固定值或可稳定复现的JS计算逻辑中找到。该参数应作为录制时刻的快照固定值写入代码，并添加明确注释说明其不属于标准三种来源。"
        };
    }

    private static List<int> BuildMinimalChain(
        List<RecordedRequest> ordered,
        List<TerminalRequestCandidate> terminals,
        List<ParameterTrace> traces)
    {
        var indices = new HashSet<int>();

        foreach (var terminal in terminals)
        {
            indices.Add(terminal.Index);
        }

        foreach (var trace in traces.Where(t => t.Category == "前序响应"))
        {
            var match = RegexHintIndex(trace.SourceHint);
            if (match.HasValue)
            {
                indices.Add(match.Value);
            }
        }

        if (indices.Count <= 1)
        {
            foreach (var (request, index) in ordered.Select((r, i) => (r, i)))
            {
                if (IsApiLike(request))
                {
                    indices.Add(index);
                }
            }
        }

        return indices.OrderBy(i => i).ToList();
    }

    private static int? RegexHintIndex(string hint)
    {
        var match = System.Text.RegularExpressions.Regex.Match(hint, @"\[(\d+)\]");
        return match.Success && int.TryParse(match.Groups[1].Value, out var n) ? n - 1 : null;
    }

    private static bool LooksLikeUserKeyword(string value, string? userPrompt)
    {
        if (string.IsNullOrWhiteSpace(userPrompt) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Length <= 64 && userPrompt.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyFixedToken(string value)
    {
        if (value.Length is >= 2 and <= 16 && value.All(char.IsDigit))
        {
            return true;
        }

        return value is "utf-8" or "1" or "0" or "true" or "false";
    }

    private static IEnumerable<(string Name, string Value)> ExtractQueryParams(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            string.IsNullOrEmpty(uri.Query))
        {
            yield break;
        }

        foreach (var (key, val) in ParseQueryString(uri.Query))
        {
            yield return (key, val);
        }
    }

    private static IEnumerable<(string Name, string Value)> ParseHeadersJson(string? json)
    {
        foreach (var pair in ParseHeadersJsonSafe(json))
        {
            yield return pair;
        }
    }

    private static List<(string Name, string Value)> ParseHeadersJsonSafe(string? json)
    {
        var result = new List<(string Name, string Value)>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                result.Add((prop.Name, prop.Value.GetString() ?? ""));
            }
        }
        catch
        {
            // ignore malformed header json
        }

        return result;
    }

    private static IEnumerable<(string Name, string Value)> ExtractBodyParams(string? body)
    {
        foreach (var pair in ExtractBodyParamsSafe(body))
        {
            yield return pair;
        }
    }

    private static List<(string Name, string Value)> ExtractBodyParamsSafe(string? body)
    {
        var result = new List<(string Name, string Value)>();
        if (string.IsNullOrWhiteSpace(body))
        {
            return result;
        }

        var trimmed = body.TrimStart();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        result.Add((prop.Name, prop.Value.ToString()));
                    }
                }
            }
            catch
            {
                result.Add(("body", Truncate(body, 200)));
            }

            return result;
        }

        if (body.Contains('='))
        {
            result.AddRange(ParseQueryString(body).Select(p => (p.Key, p.Value)));
            return result;
        }

        result.Add(("body", Truncate(body, 200)));
        return result;
    }

    private static bool IsApiLike(RecordedRequest request)
    {
        var type = request.ResourceType ?? "";
        return type.Equals("XHR", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("Fetch", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("Document", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("Other", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHtmlResponse(RecordedRequest request)
    {
        var mime = request.MimeType ?? "";
        if (mime.Contains("html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var body = DecodeResponseBody(request);
        return body.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
               body.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJsonResponse(RecordedRequest request)
    {
        var mime = request.MimeType ?? "";
        if (mime.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var body = DecodeResponseBody(request).TrimStart();
        return body.StartsWith('{') || body.StartsWith('[');
    }

    private static int GetResponseLength(RecordedRequest request)
        => DecodeResponseBody(request).Length;

    private static string DecodeResponseBody(RecordedRequest request)
    {
        if (string.IsNullOrEmpty(request.ResponseBody))
        {
            return string.Empty;
        }

        if (!request.ResponseBodyIsBase64)
        {
            return request.ResponseBody;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(request.ResponseBody));
        }
        catch
        {
            return request.ResponseBody;
        }
    }

    private static string NormalizeUrlPath(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        return uri.GetLeftPart(UriPartial.Path);
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "...";

    private static IEnumerable<(string Key, string Value)> ParseQueryString(string query)
    {
        var text = query.StartsWith('?') ? query[1..] : query;
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        foreach (var part in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq < 0)
            {
                yield return (Uri.UnescapeDataString(part), "");
                continue;
            }

            var key = Uri.UnescapeDataString(part[..eq]);
            var value = Uri.UnescapeDataString(part[(eq + 1)..]);
            yield return (key, value);
        }
    }
}

public sealed class RequestChainAnalysis
{
    public required List<TerminalRequestCandidate> TerminalRequests { get; init; }

    public required List<ParameterTrace> ParameterTraces { get; init; }

    public required List<int> ChainRequestIndices { get; init; }
}

public sealed class TerminalRequestCandidate
{
    public required int Index { get; init; }

    public required RecordedRequest Request { get; init; }

    public required string Method { get; init; }

    public required string Url { get; init; }

    public required string ResourceType { get; init; }

    public int? StatusCode { get; init; }

    public int ResponseLength { get; init; }

    public int Score { get; init; }

    public required string Reason { get; init; }
}

public sealed class ParameterTrace
{
    public required int RequestIndex { get; init; }

    public required string Location { get; init; }

    public required string Name { get; init; }

    public required string Value { get; init; }

    public required string Category { get; init; }

    public required string SourceHint { get; init; }
}
