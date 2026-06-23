using SpiderAgent.Chat.Abstractions;
using SpiderAgent.Chat.Models;

namespace SpiderAgent.App.Services;

/// <summary>
/// 根据用户首句 Prompt 调用 Agent 生成简短会话标题。
/// </summary>
public sealed class SessionTitleGenerator
{
    private readonly IChatSessionFactory _chatSessionFactory;

    public SessionTitleGenerator(IChatSessionFactory chatSessionFactory)
    {
        _chatSessionFactory = chatSessionFactory;
    }

    public async Task<string> GenerateTitleAsync(
        string firstPrompt,
        CancellationToken cancellationToken = default)
    {
        var session = _chatSessionFactory.Create(new ChatSessionOptions
        {
            SystemPrompt =
                "你是会话标题生成器。根据用户的爬虫/数据采集需求描述，生成一个简短中文标题。" +
                "要求：6-16个汉字，无标点、无引号、无换行，只输出标题本身。",
            CompletionOptions = new ChatCompletionOptions
            {
                Temperature = 0.2,
                MaxTokens = 32,
                Stream = false
            }
        });

        var response = await session.SendAsync(
            $"用户需求：\n{firstPrompt.Trim()}",
            cancellationToken: cancellationToken);

        return SanitizeTitle(response.Content);
    }

    private static string SanitizeTitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "未命名会话";
        }

        var line = raw
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0]
            .Trim()
            .Trim('"', '"', '"', '\'', '「', '」', '『', '』');

        if (line.Length == 0)
        {
            return "未命名会话";
        }

        return line.Length > 40 ? line[..40] : line;
    }
}
