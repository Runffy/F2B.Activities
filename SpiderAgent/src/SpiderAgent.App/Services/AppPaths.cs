using System.IO;

namespace SpiderAgent.App.Services;

/// <summary>
/// 应用路径：程序目录下的 SQLite 与 SpiderAgentOutput。
/// </summary>
public sealed class AppPaths
{
    public AppPaths()
    {
        AppRoot = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        DatabasePath = Path.Combine(AppRoot, "SpiderAgent.db");
        OutputRoot = Path.Combine(AppRoot, "SpiderAgentOutput");
        LegacySessionsRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpiderAgent",
            "Sessions");

        Directory.CreateDirectory(OutputRoot);
    }

    public string AppRoot { get; }

    public string DatabasePath { get; }

    public string OutputRoot { get; }

    /// <summary>录制 JSON（session.json、scripts）仍存于此。</summary>
    public string LegacySessionsRoot { get; }

    public string GetSessionOutputDirectory(string sessionId)
        => Path.Combine(OutputRoot, sessionId);

    public void EnsureSessionOutputDirectory(string sessionId)
        => Directory.CreateDirectory(GetSessionOutputDirectory(sessionId));
}
