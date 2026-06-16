using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SpiderAgent.Chat.Abstractions;
using SpiderAgent.Chat.Configuration;
using SpiderAgent.Chat.Constants;
using SpiderAgent.Chat.Providers;

namespace SpiderAgent.Chat.Services;

/// <summary>
/// 按名称解析 Chat 提供商，便于将来做 A/B 测试或多模型路由。
/// </summary>
public interface IChatProviderResolver
{
    IChatProvider Resolve(string? providerName = null);
}

internal sealed class ChatProviderResolver : IChatProviderResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ChatModuleOptions _options;

    public ChatProviderResolver(
        IServiceProvider serviceProvider,
        IOptions<ChatModuleOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
    }

    public IChatProvider Resolve(string? providerName = null)
    {
        var name = string.IsNullOrWhiteSpace(providerName)
            ? _options.Provider
            : providerName;

        return name.ToLowerInvariant() switch
        {
            "deepseek" => _serviceProvider.GetRequiredService<DeepSeekChatProvider>(),
            "internalmodel" => _serviceProvider.GetRequiredService<InternalModelChatProvider>(),
            _ => throw new InvalidOperationException(
                $"未找到 Chat 提供商 '{name}'。可选: {ChatProviderNames.DeepSeek}, {ChatProviderNames.InternalModel}")
        };
    }
}

internal static class ChatProviderRegistration
{
    public static IServiceCollection AddChatProviders(this IServiceCollection services)
    {
        services.AddSingleton<DeepSeekChatProvider>();
        services.AddSingleton<InternalModelChatProvider>();
        services.AddSingleton<IChatProvider>(sp =>
            sp.GetRequiredService<IChatProviderResolver>().Resolve());

        return services;
    }
}
