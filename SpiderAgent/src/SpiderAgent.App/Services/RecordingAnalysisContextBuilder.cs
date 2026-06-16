using System.Text;
using System.Text.RegularExpressions;
using SpiderAgent.Core.Recording;

namespace SpiderAgent.App.Services;

public sealed class RecordingAnalysisContextBuilder
{
    private const int MaxDetailedRequests = 28;
    private const int MaxResponsePreviewChars = 3500;
    private const int MaxHeaderJsonChars = 2500;
    private const int MaxRequestBodyChars = 1500;

    private static readonly HashSet<string> SkippedResourceTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Image", "Font", "Media", "Ping"
    };

    private readonly RequestChainAnalyzer _chainAnalyzer;
    private readonly RecordingSessionInsightsBuilder _insightsBuilder;

    public RecordingAnalysisContextBuilder(
        RequestChainAnalyzer chainAnalyzer,
        RecordingSessionInsightsBuilder insightsBuilder)
    {
        _chainAnalyzer = chainAnalyzer;
        _insightsBuilder = insightsBuilder;
    }

    public string BuildSystemPrompt(RecordingSession session, string? userPrompt = null)
    {
        var ordered = GetOrderedApiRequests(session);
        var chainAnalysis = _chainAnalyzer.Analyze(session, userPrompt);
        var chainMarkdown = _chainAnalyzer.ToMarkdown(chainAnalysis, ordered);
        var insights = _insightsBuilder.Build(session, ordered, chainAnalysis);
        var insightsMarkdown = _insightsBuilder.ToMarkdown(insights, ordered);
        var flowOverview = BuildFlowOverview(session);
        var detailedRequests = BuildDetailedRequests(session, ordered);
        var htmlHints = BuildHtmlParseHints(session);
        var scriptHints = BuildScriptHints(session);

        return $"""
            你是 SpiderAgent 的**网络请求逆向分析专家**。用户只会用自然语言描述业务操作（如「打开百度，搜索 RPA，得到结果」），
            你必须**自动**完成完整请求链路逆向，并生成可运行的 Python 爬虫代码。

            ## 标准逆向方法论（必须遵循）
            1. **确定目标请求**：从录制中找到返回**最大业务响应体**的那条请求（见「会话洞察」中的终端搜索结果），以它为逆向起点。
            2. **拆解目标请求参数**：列出 URL query、Headers、Body 中的每个关键字段。
            3. **逐字段溯源**：用户输入 / 前序响应提取 / Session 上下文 / JS 计算 / 录制快照固定值（来源未知）；**禁止**把录制快照中的 `rsv_*`、`isid` 写死到代码，除非该参数已被明确分类为「录制快照固定值（来源未知）」。
            4. **完整请求链**：必须按录制顺序复现**所有**同类中间请求（如 sugrec、mod=11），不可跳过。
            5. **响应校验**：目标请求返回后检查响应体大小与标记，不达标则报错，**禁止**解析中间态小响应。
            6. **实现代码**：Session 顺序复现 → 校验 → 解析终端响应；**多请求时每个请求独立封装为一个函数**（见下方「代码生成硬性规则」）。

            ## 代码生成硬性规则
            - 禁止臆造 URL/参数名；必须来自录制
            - **多请求函数封装**（必须）：若达成爬虫目标需要多个 HTTP 请求（含完整请求链中的每一步），**每个请求必须独立封装为一个 Python 函数**。函数只负责该次请求的发起、响应解码/校验，并返回下游步骤所需的提取结果；`if __name__ == "__main__"` 或顶层编排函数按录制顺序调用这些函数，共享同一个 `requests.Session`。函数名使用英文 snake_case、见名知意（如 `fetch_homepage`、`fetch_sugrec`、`fetch_search_results`）。**禁止**在 `main` 中无封装地堆砌多段请求逻辑。
            - **禁止** `if not extracted: param = "录制时的值"` 这类 fallback
            - 默认 `requests.Session` + `Accept-Encoding: gzip, deflate`（**禁止**默认带 `br`，除非 requirements 含 `brotlicffi`）
            - 响应 ~90 字节乱码时优先报「Brotli 解压失败」，**不要**误判为需 playwright
            - 纯 HTTP 在完整请求链 + 参数映射仍无法闭环时，才考虑 playwright
            - 生成的 Python 必须含 `assert`/`raise` 做响应体长度与内容校验
            - 输出 `requirements.txt`、脚本文件名（英文大驼峰 PascalCase，如 `ComicListPagination.py`）和完整 `python` 脚本
            - **自测闭环**：生成的脚本会被自动执行验证。只有当脚本在用户环境中真正跑通（退出码 0 + 产生可观察的成功输出）时，才会作为最终结果呈现给你用户。失败时会把执行错误日志反馈回来让你迭代修复（重试次数由UI上的「自测最大次数」控制，0表示不限）。因此请务必写出“能一次跑通”的代码。
            - **录制快照固定值（来源未知）处理规则**（非常重要）：
              当参数溯源表中某个参数的分类为「录制快照固定值（来源未知）」时：
              - 必须在脚本最顶部（import 之后、类/函数之前）以大写常量形式定义该值。
              - 必须添加醒目多行注释，内容大致为：
                ```python
                # === 录制快照固定值（来源未知）===
                # 该参数在自动溯源中未能归类为以下三种标准来源：
                #   1. 前序请求的响应体 / HTML 中出现的值
                #   2. JS 或 HTML 中写死的固定常量
                #   3. 可通过稳定、可复现的 JS 逻辑计算得到的值
                # 当前直接使用录制时刻的快照值作为固定值。
                # 若未来请求因该值失效，请重新录制或人工确认最新来源。
                THE_UNKNOWN_PARAM = "录制时的具体值"
                ```
              - 禁止在代码中用 `if not xxx: xxx = "录制值"` 这类静默 fallback。

            ## 会话概览
            - 会话 ID: {session.SessionId}
            - 请求总数: {session.Requests.Count}（下方 [N] 为过滤静态资源后的 API 序号）
            - 脚本总数: {session.Scripts.Count}
            - 录制时间: {session.StartedAt:yyyy-MM-dd HH:mm:ss} ~ {session.StoppedAt:yyyy-MM-dd HH:mm:ss}

            {chainMarkdown}

            ## 会话洞察（预计算，代码必须遵循）
            {insightsMarkdown}

            ## 关键请求时间线
            {flowOverview}

            ## 关键请求详情（含 Headers / Body / 响应片段）
            {detailedRequests}

            ## HTML/JSON 解析提示
            {htmlHints}

            ## 录制 JS 资源
            {scriptHints}
            """;
    }

    public static string BuildPhase1UserMessage(string userPrompt) =>
        $"""
        用户描述的业务操作：
        {userPrompt}

        请严格按 System Prompt 中的逆向方法论，结合「自动逆向分析草案」和录制详情，输出完整的【逆向分析报告】。

        报告必须包含：用户意图还原、目标请求（引用 [N] 编号及**响应体字符数**）、参数溯源表、**完整**请求链（含中间 mod=11/sugrec 步骤，不可只写首页+终态）、响应解析方案、待确认项。

        若「会话洞察」提示录制缺口（如缺少首页 Document），必须在待确认项中说明对复现的影响。

        **本阶段不要输出 Python 代码。**
        """;

    public const string Phase2UserMessage =
        """
        根据你上一轮的【逆向分析报告】和 System Prompt 中的「会话洞察」，生成可直接运行的 Python 实现。

        必须：
        1. **按「建议完整复现请求链」顺序**实现每一步（含 pc_his sugrec、sugrec、mod=11 等中间请求），用 requests.Session；**链路上每个请求独立封装为一个 Python 函数**，由 `main` 按序调用
        2. Session 默认 `Accept-Encoding: gzip, deflate`；实现 `_decode_response(resp)` 检测 br 乱码
        3. 用户输入参数化；动态字段从**同一次运行**的前序响应提取，或按「会话洞察 §13」等价生成（如 isid/rsv_t）
        4. `rsv_pq` 与 `Ps-Dataurlconfigqid` 应来自 sugrec/pc_his 的 queryid；终端 Referer 用上一步搜索 URL
        5. 搜索 XHR 须复现录制中的 `is_xhr`、`is_referer`、`X-Requested-With` 等头（见 §11）
        6. **禁止**将录制中的 rsv_* / isid 等写死为 fallback；提取/生成失败且已排除 br 乱码后才 raise
        7. **仅对「解析目标」步骤**做响应体下限校验与 HTML 解析；中间步骤只记录长度，**禁止** `assert len < 5000`
        8. 提前结束链路的条件：**必须**含 `__real_wd`；**禁止**仅凭 `c-container` 跳过 mod=1（完整 HTML 也有 c-container 但无 `__real_wd`）
        9. 解析校验优先看 h3 数量；`__real_wd` 为 XHR 片段加分项，缺失时不应直接 assert 失败（若 h3≥1 仍可解析）
        10. 若中间步骤已返回含 `__real_wd` 的大 HTML，可直接解析，并跳过剩余中间步骤
        11. 不要调用与业务无关的接口（如 ug.baidu.com/mcp）除非分析报告明确必需
        12. 先输出 ```text 的 requirements.txt，再输出 ```filename 块（仅一行英文大驼峰文件名，如 ComicListPagination.py），最后输出 ```python 完整脚本（含 if __name__ == "__main__" 和校验逻辑）
        13. **禁止**在未经完整 HTTP 尝试（含 Accept-Encoding 修复、pc_his、参数映射）前建议 playwright
        14. 对于分类为「录制快照固定值（来源未知）」的参数，必须按上面系统Prompt中的规则提取为带详细注释的顶层常量。
        15. ```filename 中的名称须概括脚本业务目标，使用英文大驼峰（PascalCase），仅字母与数字，必须以 `.py` 结尾。
        16. **多请求必须分函数**：请求链中有几步请求，就应有几个对应的请求函数（外加可选的 `main`/编排函数）；不要把多个请求写在同一个函数或 `main` 里。

        【自测要求 - 非常重要】
        你生成的脚本**会被 SpiderAgent 自动执行一次**（在用户的 Python 环境中运行）。
        - 请确保在 `if __name__ == "__main__":` 块中，成功执行后打印清晰的成功信号，例如：
          - "共找到 X 个搜索结果"
          - "SUCCESS: 共获取到 N 条数据"
          - 或者其他明确的业务完成提示 + 数量
        - 失败时应让异常自然抛出（或打印 "[错误]" 信息），以便自测能捕获 Traceback。
        - 脚本应当在正常成功路径上以 exit code 0 结束。
        这样 SpiderAgent 才能判断“脚本是否如期获得结果”，失败时会把执行日志反馈给你让你修复。
        """;

    private static List<RecordedRequest> GetOrderedApiRequests(RecordingSession session)
        => session.Requests
            .Where(r => !SkippedResourceTypes.Contains(r.ResourceType ?? string.Empty))
            .OrderBy(r => r.Timestamp)
            .ToList();

    private static string BuildFlowOverview(RecordingSession session)
    {
        var ordered = GetOrderedApiRequests(session);
        var lines = ordered
            .Select((request, index) =>
                $"[{index + 1}] [{request.Timestamp:HH:mm:ss}] {request.Method} {request.StatusCode} {request.ResourceType} {TruncateUrl(request.Url, 160)}");

        return string.Join(Environment.NewLine, lines.Take(50));
    }

    private string BuildDetailedRequests(RecordingSession session, List<RecordedRequest> ordered)
    {
        var builder = new StringBuilder();
        var important = SelectImportantRequests(session, ordered).Take(MaxDetailedRequests).ToList();

        foreach (var (request, displayIndex) in important)
        {
            builder.AppendLine($"### 请求 [{displayIndex + 1}]");
            builder.AppendLine($"时间: {request.Timestamp:O}");
            builder.AppendLine($"方法: {request.Method}");
            builder.AppendLine($"URL: {request.Url}");
            builder.AppendLine($"状态: {request.StatusCode}");
            builder.AppendLine($"类型: {request.ResourceType} / {request.MimeType ?? "(unknown)"}");
            builder.AppendLine($"**响应体长度: {GetResponseLength(request):N0} 字符**");

            if (!string.IsNullOrWhiteSpace(request.RequestHeadersJson))
            {
                builder.AppendLine("请求头:");
                builder.AppendLine(Truncate(request.RequestHeadersJson, MaxHeaderJsonChars));
            }

            if (!string.IsNullOrWhiteSpace(request.RequestBody))
            {
                builder.AppendLine("请求体:");
                builder.AppendLine(Truncate(request.RequestBody, MaxRequestBodyChars));
            }

            if (!string.IsNullOrWhiteSpace(request.ResponseHeadersJson))
            {
                builder.AppendLine("响应头:");
                builder.AppendLine(Truncate(request.ResponseHeadersJson, MaxHeaderJsonChars));
            }

            var preview = GetResponsePreview(request);
            if (!string.IsNullOrWhiteSpace(preview))
            {
                builder.AppendLine("响应体片段:");
                builder.AppendLine(preview);
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildScriptHints(RecordingSession session)
    {
        var scripts = session.Scripts
            .Where(s => !string.IsNullOrWhiteSpace(s.Url))
            .Take(25)
            .Select(s => $"- {s.Url} {(s.Content?.Length > 0 ? $"(已录制 {s.Content.Length} 字符)" : "(未拉取内容)")}");

        return string.Join(Environment.NewLine, scripts);
    }

    private static string BuildHtmlParseHints(RecordingSession session)
    {
        var htmlResponses = session.Requests
            .Where(IsHtmlResponse)
            .OrderByDescending(GetResponseLength)
            .Take(3)
            .ToList();

        if (htmlResponses.Count == 0)
        {
            return "（无 HTML 响应）";
        }

        var builder = new StringBuilder();
        foreach (var request in htmlResponses)
        {
            var html = DecodeResponseBody(request);
            if (string.IsNullOrWhiteSpace(html))
            {
                continue;
            }

            builder.AppendLine($"- URL: {TruncateUrl(request.Url, 140)}");
            builder.AppendLine($"  响应长度: {html.Length} 字符");

            var h3Count = Regex.Matches(html, "<h3\\b", RegexOptions.IgnoreCase).Count;
            var linkCount = Regex.Matches(html, "<a\\b[^>]*href=\"https?://", RegexOptions.IgnoreCase).Count;
            builder.AppendLine($"  统计: h3={h3Count}, 外链 a 标签≈{linkCount}");

            if (html.Contains("百度安全验证", StringComparison.Ordinal) ||
                html.Contains("网络不给力", StringComparison.Ordinal))
            {
                builder.AppendLine("  警告: 疑似风控页");
            }

            var sample = ExtractHtmlSample(html);
            if (!string.IsNullOrWhiteSpace(sample))
            {
                builder.AppendLine("  结构样例:");
                builder.AppendLine(Indent(sample, 4));
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static IEnumerable<(RecordedRequest Request, int Index)> SelectImportantRequests(
        RecordingSession session,
        List<RecordedRequest> ordered)
    {
        var indexMap = ordered
            .Select((r, i) => (r, i))
            .ToDictionary(x => x.r.Id, x => x.i);

        return session.Requests
            .Where(request => !SkippedResourceTypes.Contains(request.ResourceType ?? string.Empty))
            .Where(request => request.ResourceType is not "Script" ||
                              IsHtmlResponse(request) ||
                              IsJsonResponse(request))
            .OrderByDescending(ScoreRequest)
            .DistinctBy(request => $"{request.Method}|{NormalizeUrl(request.Url)}")
            .Where(request => indexMap.ContainsKey(request.Id))
            .Select(request => (request, indexMap[request.Id]))
            .OrderBy(x => x.Item2);
    }

    private static int ScoreRequest(RecordedRequest request)
    {
        var score = 0;
        var url = request.Url ?? string.Empty;
        var type = request.ResourceType ?? string.Empty;

        if (type.Equals("XHR", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("Fetch", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("Document", StringComparison.OrdinalIgnoreCase))
        {
            score += 50;
        }

        if (IsHtmlResponse(request))
        {
            score += 40;
        }

        if (IsJsonResponse(request))
        {
            score += 35;
        }

        if (url.Contains("/s?", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("search", StringComparison.OrdinalIgnoreCase) ||
            url.Contains("api", StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            score += 15;
        }

        score += Math.Min(GetResponseLength(request) / 5000, 20);

        return score;
    }

    private static bool IsHtmlResponse(RecordedRequest request)
    {
        var mime = request.MimeType ?? string.Empty;
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
        var mime = request.MimeType ?? string.Empty;
        if (mime.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var body = DecodeResponseBody(request).TrimStart();
        return body.StartsWith('{') || body.StartsWith('[');
    }

    private static string GetResponsePreview(RecordedRequest request)
    {
        var body = DecodeResponseBody(request);
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        var total = body.Length;
        var preview = Truncate(body, MaxResponsePreviewChars);
        return total > MaxResponsePreviewChars
            ? $"{preview}\n... (共 {total} 字符)"
            : preview;
    }

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

    private static int GetResponseLength(RecordedRequest request)
        => DecodeResponseBody(request).Length;

    private static string ExtractHtmlSample(string html)
    {
        var patterns = new[]
        {
            @"<div[^>]*class=""[^""]*c-container[^""]*""[^>]*>[\s\S]{0,1200}",
            @"<h3[^>]*>[\s\S]{0,800}?</h3>",
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return Truncate(match.Value, 900);
            }
        }

        var h3Index = html.IndexOf("<h3", StringComparison.OrdinalIgnoreCase);
        return h3Index >= 0 ? Truncate(html[h3Index..], 900) : string.Empty;
    }

    private static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        return uri.GetLeftPart(UriPartial.Path);
    }

    private static string TruncateUrl(string url, int maxLength)
        => url.Length <= maxLength ? url : url[..maxLength] + "...";

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private static string Indent(string text, int spaces)
        => string.Join(
            Environment.NewLine,
            text.Split(Environment.NewLine).Select(line => new string(' ', spaces) + line));
}
