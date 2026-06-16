using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SpiderAgent.App.Services;
using SpiderAgent.App.Storage;
using SpiderAgent.App.ViewModels;
using SpiderAgent.Chat.Extensions;
using SpiderAgent.Core.Recording;
using SpiderAgent.Recording.Extensions;

namespace SpiderAgent.App;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(config =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables(prefix: "SPIDERAGENT_");
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSpiderAgentChat(context.Configuration.GetSection("Chat"));
                services.AddSpiderAgentRecording(context.Configuration.GetSection("Recording"));
                services.AddSingleton<AppPaths>();
                services.AddSingleton<ISessionDatabase, SqliteSessionDatabase>();
                services.AddSingleton<IRecordingSessionStore, SessionStore>();
                services.AddSingleton<FileLogService>();
                services.AddSingleton<OutputLogService>();
                services.AddSingleton<RequestChainAnalyzer>();
                services.AddSingleton<RequestChainAnalyzer>();
                services.AddSingleton<RecordingSessionInsightsBuilder>();
                services.AddSingleton<RecordingAnalysisContextBuilder>();
                services.AddSingleton<PythonScriptValidator>();
                services.AddSingleton<SessionTitleGenerator>();
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        Services = _host.Services;
        await _host.StartAsync();

        var database = Services.GetRequiredService<ISessionDatabase>();
        await database.InitializeAsync();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            var recordingService = _host.Services.GetService<Recording.ChromeRecordingService>();
            if (recordingService is not null)
            {
                await recordingService.DisposeAsync();
            }

            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
