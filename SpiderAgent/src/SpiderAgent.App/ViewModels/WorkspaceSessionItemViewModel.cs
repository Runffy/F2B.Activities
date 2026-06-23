using SpiderAgent.App.Infrastructure;
using SpiderAgent.Core.Recording;

namespace SpiderAgent.App.ViewModels;

public sealed class WorkspaceSessionItemViewModel : ObservableObject
{
    private string _title;

    public WorkspaceSessionItemViewModel(WorkspaceSessionMetadata metadata)
    {
        SessionId = metadata.SessionId;
        _title = metadata.Title;
        UpdatedAt = metadata.UpdatedAt;
        HasRecording = metadata.HasRecording;
        HasAnalysisHistory = metadata.HasAnalysisHistory;
    }

    public string SessionId { get; }

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value))
            {
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool HasRecording { get; private set; }

    public bool HasAnalysisHistory { get; private set; }

    public string DisplayTitle =>
        string.IsNullOrWhiteSpace(Title) ? $"会话 {SessionId}" : Title;

    public string Subtitle =>
        UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public void ApplyMetadata(WorkspaceSessionMetadata metadata)
    {
        Title = metadata.Title;
        UpdatedAt = metadata.UpdatedAt;
        HasRecording = metadata.HasRecording;
        HasAnalysisHistory = metadata.HasAnalysisHistory;
        OnPropertyChanged(nameof(Subtitle));
    }
}
