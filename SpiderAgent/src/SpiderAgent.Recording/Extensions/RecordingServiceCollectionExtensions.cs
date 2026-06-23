using Microsoft.Extensions.DependencyInjection;
using SpiderAgent.Core.Recording;
using SpiderAgent.Recording.Bridge;
using SpiderAgent.Recording.Configuration;
using SpiderAgent.Recording.Storage;

namespace SpiderAgent.Recording.Extensions;

public static class RecordingServiceCollectionExtensions
{
    public static IServiceCollection AddSpiderAgentRecording(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        services.Configure<RecordingOptions>(configuration.GetSection(RecordingOptions.SectionName));
        services.AddSingleton<RecordingSessionStore>();
        services.AddSingleton<ExtensionBridgeWebSocketServer>();
        services.AddSingleton<ChromeRecordingService>();

        return services;
    }
}
