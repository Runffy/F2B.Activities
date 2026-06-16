using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace SpiderAgent.Chat.Configuration;

/// <summary>
/// 与 Python DeepseekConfig 对齐：优先读环境变量 deepseekapikey。
/// </summary>
internal sealed class DeepSeekOptionsConfigurator : IPostConfigureOptions<ChatModuleOptions>
{
    public const string ApiKeyEnvironmentVariable = "deepseekapikey";

    private readonly IConfiguration _configuration;

    public DeepSeekOptionsConfigurator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void PostConfigure(string? name, ChatModuleOptions options)
    {
        var deepSeek = options.DeepSeek;

        if (string.IsNullOrWhiteSpace(deepSeek.ApiKey))
        {
            deepSeek.ApiKey =
                _configuration[ApiKeyEnvironmentVariable]
                ?? Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable)
                ?? string.Empty;
        }
    }
}
