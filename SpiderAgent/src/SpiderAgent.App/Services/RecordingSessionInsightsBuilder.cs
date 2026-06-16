using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SpiderAgent.Core.Recording;

namespace SpiderAgent.App.Services;

/// <summary>
/// 从录制数据提取缺口检测、完整请求链、目标响应校验规则。
/// </summary>
public sealed class RecordingSessionInsightsBuilder
{
    private static readonly HashSet<string> DynamicParamNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "isid", "rsv_pq", "rsv_t", "rsv_sid", "rsv_dl", "cqid", "ver", "chk", "isbd"
    };

    public SessionInsights Build(
        RecordingSession session,
        IReadOnlyList<RecordedRequest> ordered,
        RequestChainAnalysis chainAnalysis)
    {
        var gaps = DetectRecordingGaps(session, ordered);
        var searchFlow = BuildSearchFlowSteps(ordered);
        var validations = BuildTerminalValidations(ordered, searchFlow);
        var fullChain = BuildFullReplicationChain(ordered, chainAnalysis.TerminalRequests);
        var headerTemplates = BuildSearchRequestHeaderTemplates(ordered, fullChain);
        var jsOnlyParams = DetectJsOnlyDynamicParams(ordered, fullChain);
        var extractionHints = BuildIntermediateExtractionHints(ordered, fullChain);

        return new SessionInsights
        {
            RecordingGaps = gaps,
            SearchFlowSteps = searchFlow,
            TerminalValidations = validations,
            FullReplicationChainIndices = fullChain,
            SearchRequestHeaderTemplates = headerTemplates,
            JsOnlyDynamicParams = jsOnlyParams,
            IntermediateExtractionHints = extractionHints
        };
    }

    public string ToMarkdown(SessionInsights insights, IReadOnlyList<RecordedRequest> ordered)
    {
        var builder = new StringBuilder();

        builder.AppendLine("### 5. 录制缺口检测（必读）");
        if (insights.RecordingGaps.Count == 0)
        {
            builder.AppendLine("- 未发现明显缺口。");
        }
        else
        {
            foreach (var gap in insights.RecordingGaps)
            {
                builder.AppendLine($"- ⚠ {gap}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### 6. 同类业务请求序列（含响应体大小，禁止跳过中间步骤）");
        if (insights.SearchFlowSteps.Count == 0)
        {
            builder.AppendLine("- （未识别到明显的搜索/提交类请求序列）");
        }
        else
        {
            builder.AppendLine("| 序号 | 方法 | 响应字符数 | 角色 | URL(截断) |");
            builder.AppendLine("|------|------|------------|------|-----------|");
            foreach (var step in insights.SearchFlowSteps)
            {
                var url = Truncate(step.Url, 80).Replace("|", "\\|");
                builder.AppendLine($"| [{step.Index + 1}] | {step.Method} | **{step.ResponseLength:N0}** | {step.Role} | {url} |");
            }

            builder.AppendLine();
            builder.AppendLine("**说明**: 以「解析目标」步骤的响应做业务解析；中间步骤长度仅供参考，**禁止**对中间步骤写 `assert len < 5000` 等硬编码上限。");
        }

        builder.AppendLine();
        builder.AppendLine("### 7. 逐步响应预期（按录制，代码必须参考）");
        if (insights.SearchFlowSteps.Count == 0)
        {
            builder.AppendLine("- （无）");
        }
        else
        {
            builder.AppendLine("| 序号 | 录制字符数 | 角色 | 代码中如何处理 |");
            builder.AppendLine("|------|------------|------|----------------|");
            foreach (var step in insights.SearchFlowSteps)
            {
                var url = Truncate(step.Url, 60).Replace("|", "\\|");
                var handling = step.IsParseTarget
                    ? "**唯一解析目标**；校验 len 与标记"
                    : "仅执行请求，记录 len；**不要** assert 上限，不要解析";
                builder.AppendLine($"| [{step.Index + 1}] | {step.ResponseLength:N0} | {step.Role} | {handling} |");
            }

            var parseTarget = insights.SearchFlowSteps.FirstOrDefault(s => s.IsParseTarget);
            if (parseTarget is not null)
            {
                builder.AppendLine();
                builder.AppendLine($"**解析目标**: 请求 **[{parseTarget.Index + 1}]**（录制响应 {parseTarget.ResponseLength:N0} 字符）");
                builder.AppendLine("**提前解析条件**：仅当响应含 `__real_wd` 且 h3≥1 才可跳过剩余步骤；**禁止**仅凭 `c-container` 判定（完整 HTML 页有 c-container 但无 `__real_wd`）。");
                builder.AppendLine("中间 mod=11 正常约 **300 字符**且含 `__sugPreInfo`；若 mod=11 意外变大但无 `__real_wd`，**必须继续 mod=1**。");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### 8. 终端响应校验（仅针对解析目标步骤）");
        foreach (var rule in insights.TerminalValidations)
        {
            builder.AppendLine($"- 目标请求 **[{rule.RequestIndex + 1}]**: 响应体长度应 ≥ **{rule.MinResponseLength:N0}** 字符（录制时 {rule.RecordedLength:N0}）");
            if (rule.RequiredBodyMarkers.Count > 0)
            {
                builder.AppendLine($"  - 响应体应包含标记: `{string.Join("`, `", rule.RequiredBodyMarkers)}`");
            }

            if (rule.MinH3Count > 0)
            {
                builder.AppendLine($"  - 解析前应检测到 h3 标签约 ≥ {rule.MinH3Count} 个（录制时参考值）");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### 9. 建议完整复现请求链（按时间顺序，含中间步骤）");
        foreach (var index in insights.FullReplicationChainIndices)
        {
            if (index < 0 || index >= ordered.Count)
            {
                continue;
            }

            var request = ordered[index];
            var len = GetResponseLength(request);
            builder.AppendLine($"- **[{index + 1}]** {request.Method} 响应 {len:N0} 字符 — {Truncate(request.Url, 150)}");
        }

        builder.AppendLine();
        builder.AppendLine("### 10. HTTP 客户端与响应解压（必读，常见失败根因）");
        builder.AppendLine("- Python `requests` **默认不解压 Brotli（br）**；若 Session 全局设置了 `Accept-Encoding: gzip, deflate, br` 且未安装 `brotlicffi`，XHR 响应会呈现 **~80–120 字节二进制乱码**，被误判为「参数缺失/需 playwright」");
        builder.AppendLine("- **默认做法**：Session 仅设 `Accept-Encoding: gzip, deflate`（不要带 `br`）；或在 requirements.txt 加入 `brotlicffi`");
        builder.AppendLine("- 生成代码须实现 `_decode_response(resp)`：若 `Content-Encoding` 含 `br` 且 `resp.text` 不可读，应 **明确报错**「需 brotli 或去掉 Accept-Encoding 中的 br」，而非直接解析乱码");
        builder.AppendLine("- 中间 mod=11 正常响应约 **300 字符** HTML（含 `__sugPreInfo`）；终端 mod=1 正常响应 **≥10 万字符**。若得到 ~90 字节乱码，**优先检查解压**。");

        builder.AppendLine();
        builder.AppendLine("### 11. 搜索 XHR 必需请求头（从录制复制，勿省略）");
        if (insights.SearchRequestHeaderTemplates.Count == 0)
        {
            builder.AppendLine("- （未识别到搜索 XHR 特有头）");
        }
        else
        {
            foreach (var template in insights.SearchRequestHeaderTemplates)
            {
                builder.AppendLine($"- 请求 **[{template.RequestIndex + 1}]** 录制头（除 User-Agent 外应复现）:");
                foreach (var (name, value) in template.Headers)
                {
                    builder.AppendLine($"  - `{name}`: `{Truncate(value, 80)}`");
                }
            }

            builder.AppendLine("- 百度搜索链常见：`X-Requested-With: XMLHttpRequest`、`is_xhr: 1`、`is_referer: https://www.baidu.com/`、`Ps-Dataurlconfigqid`（与当前 queryid 同步）");
            builder.AppendLine("- **终端搜索**的 `Referer` 通常是**上一步搜索 URL**（含 rsv_pq 等），不是固定首页");
        }

        builder.AppendLine();
        builder.AppendLine("### 12. 中间响应提取规则（纯 HTTP 可闭环）");
        if (insights.IntermediateExtractionHints.Count == 0)
        {
            builder.AppendLine("- （无）");
        }
        else
        {
            foreach (var hint in insights.IntermediateExtractionHints)
            {
                builder.AppendLine($"- {hint}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("### 13. JS/会话态参数（录制中未出现在前序响应体，但可 HTTP 等价获取）");
        if (insights.JsOnlyDynamicParams.Count == 0)
        {
            builder.AppendLine("- 目标请求的关键动态参数均能在前序 HTTP 响应中找到线索。");
        }
        else
        {
            foreach (var item in insights.JsOnlyDynamicParams)
            {
                builder.AppendLine($"- `{item.Name}`: {item.Hint}");
            }

            builder.AppendLine("- **禁止**因上述参数未在 HTML 正则命中就放弃纯 HTTP；应按上表策略生成/映射后继续请求链");
        }

        builder.AppendLine();
        builder.AppendLine("### 14. 代码硬性约束");
        builder.AppendLine("- **禁止**将录制中的 `rsv_*` / `isid` / `cqid` 等动态参数写死为 fallback 默认值");
        builder.AppendLine("- 动态参数必须从**同一次运行**的前序响应/HTML/Cookie 提取或按 §13 等价生成；**禁止**在未排查 Accept-Encoding/br 乱码前就建议 playwright");
        builder.AppendLine("- 凡被标记为「录制快照固定值（来源未知）」的参数，**必须**在生成的Python顶部以带醒目注释的常量形式出现，注释需明确说明其不属于前序响应 / JS固定值 / JS计算 三种来源。");
        builder.AppendLine("- **仅对解析目标步骤**做 `len(text)` 下限校验与 HTML 解析");
        builder.AppendLine("- **禁止**对中间步骤使用 `assert len(text) < 5000` 或「mod=11 不能太大」类逻辑");
        builder.AppendLine("- 若中间步骤已返回含结果标记的大 HTML，可直接解析，并跳过剩余中间步骤");
        builder.AppendLine("- requirements.txt 默认至少：`requests`, `beautifulsoup4`, `lxml`；**不要**默认加 `br` 除非同时加 `brotlicffi`");

        return builder.ToString();
    }

    public static bool IsDynamicTrackingParam(string name)
        => DynamicParamNames.Contains(name) ||
           name.StartsWith("rsv_", StringComparison.OrdinalIgnoreCase);

    private static List<string> DetectRecordingGaps(RecordingSession session, IReadOnlyList<RecordedRequest> ordered)
    {
        var gaps = new List<string>();

        var hasHomeDocument = session.Requests.Any(r =>
            IsHomepageUrl(r.Url) &&
            string.Equals(r.ResourceType, "Document", StringComparison.OrdinalIgnoreCase) &&
            GetResponseLength(r) > 5000);

        var hasHomeAny = session.Requests.Any(r => IsHomepageUrl(r.Url) && GetResponseLength(r) > 5000);

        if (!hasHomeAny)
        {
            gaps.Add("录制中**未包含**站点首页 Document 请求（如 https://www.baidu.com/）。会话可能在录制开始前已打开，页面级 queryid/isid 由 JS 初始化。纯 HTTP 复现：先 GET 首页拿 Cookie，再请求录制中出现的 `prod=pc_his` sugrec 获取 `queryid` 作为 `rsv_pq`/`Ps-Dataurlconfigqid`；`isid`/`rsv_t` 可按 §13 会话等价生成。**不要**因此直接放弃 HTTP。");
        }
        else if (!hasHomeDocument)
        {
            gaps.Add("有首页相关资源但未捕获首页 Document 主文档，Cookie/内嵌参数来源可能不完整。");
        }

        var terminals = ordered
            .Select((r, i) => (r, i))
            .Where(x => GetResponseLength(x.r) > 100_000)
            .ToList();

        foreach (var (request, index) in terminals)
        {
            var priorSameFlow = ordered
                .Take(index)
                .Count(r => IsSameFlowFamily(r.Url, request.Url));

            if (priorSameFlow == 0 && index > 0)
            {
                gaps.Add($"终端请求 [{index + 1}] 之前无同族中间请求，但浏览器实际可能存在未录到的前置步骤。");
                break;
            }
        }

        if (ordered.Count > 0 && ordered[0].Timestamp > session.StartedAt.AddSeconds(2))
        {
            gaps.Add("首条 API 请求距录制开始有时间差，之前发生的导航/输入可能未被捕获。");
        }

        return gaps;
    }

    private static List<SearchFlowStep> BuildSearchFlowSteps(IReadOnlyList<RecordedRequest> ordered)
    {
        var steps = new List<SearchFlowStep>();

        for (var i = 0; i < ordered.Count; i++)
        {
            var request = ordered[i];
            var url = request.Url ?? "";
            if (!IsSearchFlowUrl(url))
            {
                continue;
            }

            var len = GetResponseLength(request);
            steps.Add(new SearchFlowStep
            {
                Index = i,
                Method = request.Method,
                Url = url,
                ResponseLength = len,
                Role = ClassifySearchStepRole(url, len),
                IsParseTarget = false
            });
        }

        if (steps.Count == 0)
        {
            return steps;
        }

        var maxLength = steps.Max(s => s.ResponseLength);
        if (maxLength < 10_000)
        {
            return steps;
        }

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step.ResponseLength != maxLength)
            {
                continue;
            }

            steps[i] = step with
            {
                IsParseTarget = true,
                Role = step.Role.Contains("终端", StringComparison.Ordinal)
                    ? step.Role
                    : "**终端搜索结果（解析目标）**"
            };
        }

        return steps;
    }

    private static string ClassifySearchStepRole(string url, int responseLength)
    {
        if (url.Contains("sugrec", StringComparison.OrdinalIgnoreCase))
        {
            return "搜索建议 JSON";
        }

        if (url.Contains("/s?", StringComparison.OrdinalIgnoreCase))
        {
            if (responseLength > 50_000)
            {
                return url.Contains("mod=11", StringComparison.OrdinalIgnoreCase)
                    ? "**终端搜索结果（mod=11，解析目标）**"
                    : "**终端搜索结果**";
            }

            if (responseLength < 2000)
            {
                return "中间态 XHR（非最终结果）";
            }

            if (url.Contains("mod=11", StringComparison.OrdinalIgnoreCase))
            {
                return "输入预加载/建议（中间态）";
            }

            return "搜索 XHR（偏小，可能非最终页）";
        }

        return responseLength > 50_000 ? "大响应 API（候选终端）" : "辅助 API";
    }

    private static List<TerminalValidationRule> BuildTerminalValidations(
        IReadOnlyList<RecordedRequest> ordered,
        IReadOnlyList<SearchFlowStep> searchFlow)
    {
        var rules = new List<TerminalValidationRule>();
        var parseTargets = searchFlow.Where(s => s.IsParseTarget).Take(2).ToList();
        if (parseTargets.Count == 0)
        {
            parseTargets = searchFlow
                .OrderByDescending(s => s.ResponseLength)
                .Take(1)
                .Select(s => s with { IsParseTarget = true })
                .ToList();
        }

        foreach (var target in parseTargets.Where(t => t.ResponseLength > 10_000))
        {
            if (target.Index < 0 || target.Index >= ordered.Count)
            {
                continue;
            }

            var request = ordered[target.Index];
            var body = DecodeResponseBody(request);
            var markers = new List<string>();

            foreach (var marker in new[] { "__real_wd", "c-container", "EC_result", "result" })
            {
                if (body.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    markers.Add(marker);
                }
            }

            // __real_wd 是 XHR 结果片段的强标记；c-container 单独不足以证明是终端 XHR
            if (markers.Contains("__real_wd"))
            {
                markers.RemoveAll(m => m.Equals("c-container", StringComparison.OrdinalIgnoreCase));
                markers.Insert(0, "__real_wd");
            }

            var h3Count = Regex.Matches(body, "<h3\\b", RegexOptions.IgnoreCase).Count;
            var minLen = Math.Max(10_000, (int)(target.ResponseLength * 0.25));

            rules.Add(new TerminalValidationRule
            {
                RequestIndex = target.Index,
                RecordedLength = target.ResponseLength,
                MinResponseLength = minLen,
                RequiredBodyMarkers = markers,
                MinH3Count = h3Count > 0 ? Math.Max(1, h3Count / 3) : 0
            });
        }

        return rules;
    }

    private static List<int> BuildFullReplicationChain(
        IReadOnlyList<RecordedRequest> ordered,
        IReadOnlyList<TerminalRequestCandidate> terminals)
    {
        if (terminals.Count == 0)
        {
            return ordered
                .Select((r, i) => (r, i))
                .Where(x => IsApiLike(x.r))
                .Select(x => x.i)
                .ToList();
        }

        var indices = new HashSet<int>();
        var primary = terminals.OrderByDescending(t => t.ResponseLength).First();

        for (var i = 0; i <= primary.Index; i++)
        {
            var request = ordered[i];
            if (!IsApiLike(request))
            {
                continue;
            }

            var url = request.Url ?? "";
            if (!IsSameFlowFamily(url, primary.Url))
            {
                continue;
            }

            indices.Add(i);
        }

        indices.Add(primary.Index);
        return indices.OrderBy(i => i).ToList();
    }

    private static List<SearchRequestHeaderTemplate> BuildSearchRequestHeaderTemplates(
        IReadOnlyList<RecordedRequest> ordered,
        IReadOnlyList<int> chainIndices)
    {
        var templates = new List<SearchRequestHeaderTemplate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var index in chainIndices)
        {
            if (index < 0 || index >= ordered.Count)
            {
                continue;
            }

            var request = ordered[index];
            var url = request.Url ?? "";
            if (!url.Contains("/s?", StringComparison.OrdinalIgnoreCase) &&
                !url.Contains("sugrec", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var headers = ParseRequestHeaders(request.RequestHeadersJson)
                .Where(h => !IsSkippableClientHeader(h.Key))
                .ToDictionary(h => h.Key, h => h.Value, StringComparer.OrdinalIgnoreCase);

            if (headers.Count == 0)
            {
                continue;
            }

            var key = string.Join("|", headers.OrderBy(h => h.Key).Select(h => $"{h.Key}={h.Value}"));
            if (!seen.Add(key))
            {
                continue;
            }

            templates.Add(new SearchRequestHeaderTemplate
            {
                RequestIndex = index,
                Headers = headers
            });

            if (templates.Count >= 3)
            {
                break;
            }
        }

        return templates;
    }

    private static List<JsOnlyDynamicParam> DetectJsOnlyDynamicParams(
        IReadOnlyList<RecordedRequest> ordered,
        IReadOnlyList<int> chainIndices)
    {
        var results = new List<JsOnlyDynamicParam>();
        if (chainIndices.Count == 0)
        {
            return results;
        }

        var terminalIndex = chainIndices.Max();
        if (terminalIndex < 0 || terminalIndex >= ordered.Count)
        {
            return results;
        }

        var terminalUrl = ordered[terminalIndex].Url ?? "";
        var priorBodies = ordered
            .Take(terminalIndex)
            .Select(DecodeResponseBody)
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .ToList();

        var hasPcHis = ordered
            .Take(terminalIndex)
            .Any(r => (r.Url ?? "").Contains("prod=pc_his", StringComparison.OrdinalIgnoreCase));

        foreach (var (name, value) in ExtractQueryParams(terminalUrl))
        {
            if (!IsDynamicTrackingParam(name) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (priorBodies.Any(body => body.Contains(value, StringComparison.Ordinal)))
            {
                continue;
            }

            var hint = name.ToLowerInvariant() switch
            {
                "rsv_pq" => hasPcHis
                    ? "录制有 pc_his sugrec：其 JSON 的 queryid 或页面级 qid 应映射为 rsv_pq；与 Ps-Dataurlconfigqid 保持一致"
                    : "先请求 sugrec（prod=pc_his 或首次 pre=1 sugrec）取 queryid，赋值 rsv_pq 与 Ps-Dataurlconfigqid",
                "isid" => "未出现在前序响应；可用 secrets.token_hex(8).upper() 生成 16 位 hex 会话 id（同一会话内保持不变）",
                "rsv_t" => "未出现在前序响应；可用 secrets.token_urlsafe(32) 生成（同一会话内保持不变）",
                "rsv_sid" or "sugsid" => "从 sugrec URL 的 sugsid/sid 参数或 Cookie H_PS_PSSID 提取，下划线/逗号格式互转",
                _ when name.StartsWith("rsv_", StringComparison.OrdinalIgnoreCase) =>
                    "未出现在前序响应；检查是否可由 queryid/__sugPreInfo 或上一步 URL 推导",
                _ => "未出现在前序响应；检查中间 HTML 的 __sugPreInfo / __queryId 或上一步响应"
            };

            results.Add(new JsOnlyDynamicParam { Name = name, Hint = hint });
        }

        return results
            .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Take(8)
            .ToList();
    }

    private static List<string> BuildIntermediateExtractionHints(
        IReadOnlyList<RecordedRequest> ordered,
        IReadOnlyList<int> chainIndices)
    {
        var hints = new List<string>();
        var hasPcHis = ordered.Any(r => (r.Url ?? "").Contains("prod=pc_his", StringComparison.OrdinalIgnoreCase));
        if (hasPcHis)
        {
            hints.Add("录制含 `sugrec?prod=pc_his`：在首次输入建议前请求，从 JSON 取 `queryid` → 写入 `rsv_pq` 与 `Ps-Dataurlconfigqid`");
        }

        hints.Add("sugrec JSON/JSONP 响应中的 `queryid` → 下一步 XHR 的 `Ps-Dataurlconfigqid` 头");
        hints.Add("mod=11 小响应 HTML 中 `<script id=\"__sugPreInfo\">` 的 JSON → 更新 queryid；`__querySign` 供后续步骤");
        hints.Add("mod=11 成功中间态约 300 字符且 `__status` 为 -12；若仅 ~90 字节乱码 → 检查 Brotli/Accept-Encoding");
        hints.Add("mod=11 中间态应用 `rsv_dl=tb_pre`（录制如此）；终端 mod=1 用 `rsv_dl=tb_enter` + `prefixsug`");
        hints.Add("**禁止**用 `c-container` 单独判定可解析：须 `__real_wd` 或继续请求 mod=1");

        foreach (var index in chainIndices)
        {
            if (index < 0 || index >= ordered.Count)
            {
                continue;
            }

            var body = DecodeResponseBody(ordered[index]);
            if (body.Contains("__sugPreInfo", StringComparison.Ordinal))
            {
                hints.Add($"请求 **[{index + 1}]** 响应含 `__sugPreInfo`，应用 regex/json 提取 queryid 供后续步骤");
                break;
            }
        }

        return hints;
    }

    private static Dictionary<string, string> ParseRequestHeaders(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.GetString() ?? "", StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool IsSkippableClientHeader(string name)
        => name.Equals("User-Agent", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("Accept-Encoding", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("Accept-Language", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("Connection", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("Host", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<(string Name, string Value)> ExtractQueryParams(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            yield break;
        }

        var query = uri.Query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var name = Uri.UnescapeDataString(part[..eq]);
            var value = Uri.UnescapeDataString(part[(eq + 1)..]);
            yield return (name, value);
        }
    }

    private static bool IsHomepageUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var path = uri.AbsolutePath;
        return path is "" or "/" &&
               !uri.Query.Contains("s?", StringComparison.Ordinal) &&
               !uri.Host.Contains("ug.baidu", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSearchFlowUrl(string url)
        => url.Contains("sugrec", StringComparison.OrdinalIgnoreCase) ||
           url.Contains("/s?", StringComparison.OrdinalIgnoreCase) ||
           (url.Contains("search", StringComparison.OrdinalIgnoreCase) &&
            !url.EndsWith(".js", StringComparison.OrdinalIgnoreCase) &&
            !url.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

    private static bool IsSameFlowFamily(string url, string terminalUrl)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u1) ||
            !Uri.TryCreate(terminalUrl, UriKind.Absolute, out var u2))
        {
            return false;
        }

        if (!string.Equals(u1.Host, u2.Host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(u1.Host, u2.Host, StringComparison.OrdinalIgnoreCase) &&
               IsSearchFlowUrl(url);
    }

    private static bool IsApiLike(RecordedRequest request)
    {
        var type = request.ResourceType ?? "";
        return type.Equals("XHR", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("Fetch", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("Document", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("Other", StringComparison.OrdinalIgnoreCase);
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

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "...";
}

public sealed class SessionInsights
{
    public required List<string> RecordingGaps { get; init; }

    public required List<SearchFlowStep> SearchFlowSteps { get; init; }

    public required List<TerminalValidationRule> TerminalValidations { get; init; }

    public required List<int> FullReplicationChainIndices { get; init; }

    public required List<SearchRequestHeaderTemplate> SearchRequestHeaderTemplates { get; init; }

    public required List<JsOnlyDynamicParam> JsOnlyDynamicParams { get; init; }

    public required List<string> IntermediateExtractionHints { get; init; }
}

public sealed record SearchFlowStep
{
    public required int Index { get; init; }

    public required string Method { get; init; }

    public required string Url { get; init; }

    public required int ResponseLength { get; init; }

    public required string Role { get; init; }

    public required bool IsParseTarget { get; init; }
}

public sealed class TerminalValidationRule
{
    public required int RequestIndex { get; init; }

    public required int RecordedLength { get; init; }

    public required int MinResponseLength { get; init; }

    public required List<string> RequiredBodyMarkers { get; init; }

    public required int MinH3Count { get; init; }
}

public sealed class SearchRequestHeaderTemplate
{
    public required int RequestIndex { get; init; }

    public required Dictionary<string, string> Headers { get; init; }
}

public sealed class JsOnlyDynamicParam
{
    public required string Name { get; init; }

    public required string Hint { get; init; }
}
