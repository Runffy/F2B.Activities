using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpiderAgent.Chat.Abstractions;
using SpiderAgent.Chat.Configuration;
using SpiderAgent.Chat.Services;

namespace SpiderAgent.Chat.Extensions;

public static class ChatServiceCollectionExtensions
{
    /// <summary>
    /// 注册独立 Chat 模块。切换 LLM 只需修改配置中的 Chat:Provider。
    /// </summary>
    public static IServiceCollection AddSpiderAgentChat(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ChatModuleOptions>(configuration);
        services.AddSingleton<IPostConfigureOptions<ChatModuleOptions>, DeepSeekOptionsConfigurator>();
        RegisterCoreServices(services);

        return services;
    }

    /// <summary>
    /// 通过代码指定配置，便于单元测试或宿主应用自定义配置源。
    /// </summary>
    public static IServiceCollection AddSpiderAgentChat(
        this IServiceCollection services,
        Action<ChatModuleOptions> configure)
    {
        services.Configure(configure);
        services.PostConfigure<ChatModuleOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.DeepSeek.ApiKey))
            {
                options.DeepSeek.ApiKey =
                    Environment.GetEnvironmentVariable(DeepSeekOptionsConfigurator.ApiKeyEnvironmentVariable)
                    ?? string.Empty;
            }
        });
        RegisterCoreServices(services);

        return services;
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton<IChatProviderResolver, ChatProviderResolver>();
        services.AddChatProviders();
        services.AddSingleton<IChatClient, ChatClient>();
        services.AddSingleton<IChatSessionFactory, ChatSessionFactory>();
    }
}
