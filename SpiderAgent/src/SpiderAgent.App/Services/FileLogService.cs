using System.IO;
using System.Text;

namespace SpiderAgent.App.Services;

public sealed class FileLogService
{
    private readonly object _sync = new();
    private readonly string _logDirectory;
    private string? _sessionOutputDirectory;

    public FileLogService()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpiderAgent",
            "Logs");
        Directory.CreateDirectory(_logDirectory);
        CurrentLogFilePath = Path.Combine(_logDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
    }

    public string CurrentLogFilePath { get; }

    public string LogDirectory => _logDirectory;

    public void SetSessionOutputDirectory(string sessionOutputDirectory)
    {
        Directory.CreateDirectory(sessionOutputDirectory);
        _sessionOutputDirectory = sessionOutputDirectory;
    }

    public void WriteLine(string line)
    {
        var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {line}{Environment.NewLine}";
        lock (_sync)
        {
            if (_sessionOutputDirectory is not null)
            {
                File.AppendAllText(
                    Path.Combine(_sessionOutputDirectory, "session.log"),
                    entry,
                    Encoding.UTF8);
                return;
            }

            File.AppendAllText(CurrentLogFilePath, entry, Encoding.UTF8);
        }
    }

    public string SaveAnalysisResponse(string content)
    {
        var directory = _sessionOutputDirectory ?? _logDirectory;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"analysis-{DateTime.Now:yyyyMMddHHmmss}.txt");
        lock (_sync)
        {
            File.WriteAllText(path, content, Encoding.UTF8);
        }

        return path;
    }
}
