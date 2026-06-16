using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using SpiderAgent.App.Infrastructure;
using SpiderAgent.App.Services;
using SpiderAgent.Chat.Abstractions;
using SpiderAgent.Chat.Models;
using SpiderAgent.Core.Chrome;
using SpiderAgent.Core.Recording;
using SpiderAgent.Recording;

namespace SpiderAgent.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private const int AgentDisplayMaxChars = 6000;

    private readonly ChromeRecordingService _recordingService;
    private readonly IRecordingSessionStore _sessionStore;
    private readonly AppPaths _appPaths;
    private readonly OutputLogService _outputLog;
    private readonly FileLogService _fileLog;
    private readonly IChatSessionFactory _chatSessionFactory;
    private readonly RecordingAnalysisContextBuilder _analysisContextBuilder;
    private readonly PythonScriptValidator _pythonValidator;
    private readonly SessionTitleGenerator _sessionTitleGenerator;

    private readonly StringBuilder _streamBuffer = new();
    private readonly object _streamLock = new();
    private CancellationTokenSource? _analyzeCts;
    private IChatSession? _analysisSession;
    private string? _currentWorkspaceSessionId;
    private bool _suppressSessionSelectionChange;

    private RecordingStatus _recordingStatus = RecordingStatus.Idle;
    private bool _isBridgeConnected;
    private bool _isAnalyzing;
    private string _recordingStepsPrompt = string.Empty;
    private string _spiderGoalPrompt = string.Empty;
    private int _recordedRequestCount;
    private string _outputText = string.Empty;

    public MainWindowViewModel(
        ChromeRecordingService recordingService,
        IRecordingSessionStore sessionStore,
        AppPaths appPaths,
        OutputLogService outputLog,
        FileLogService fileLog,
        IChatSessionFactory chatSessionFactory,
        RecordingAnalysisContextBuilder analysisContextBuilder,
        PythonScriptValidator pythonValidator,
        SessionTitleGenerator sessionTitleGenerator)
    {
        _recordingService = recordingService;
        _sessionStore = sessionStore;
        _appPaths = appPaths;
        _outputLog = outputLog;
        _fileLog = fileLog;
        _chatSessionFactory = chatSessionFactory;
        _analysisContextBuilder = analysisContextBuilder;
        _pythonValidator = pythonValidator;
        _sessionTitleGenerator = sessionTitleGenerator;

        WorkspaceSessions = new ObservableCollection<WorkspaceSessionItemViewModel>();

        OutputText = _outputLog.Text;

        StartRecordingCommand = new AsyncRelayCommand(StartRecordingAsync, () => CanStartRecording);
        StopRecordingCommand = new AsyncRelayCommand(StopRecordingAsync, () => CanStopRecording);
        AnalyzeOrStopCommand = new RelayCommand(OnAnalyzeOrStop, () => CanAnalyzeOrStop);
        OpenSessionOutputDirectoryCommand = new RelayCommand(OpenSessionOutputDirectory);
        OpenExtensionFolderCommand = new RelayCommand(OpenExtensionFolder);
        ClearOutputCommand = new RelayCommand(OnClearOutput);
        CreateNewSessionCommand = new RelayCommand(CreateNewSession, () => CanCreateNewSession);
        DeleteCurrentWorkspaceSessionCommand = new AsyncRelayCommand(
            DeleteCurrentWorkspaceSessionAsync,
            () => CanDeleteWorkspaceSession);

        _outputLog.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(OutputLogService.Text))
            {
                OutputText = _outputLog.Text;
            }
        };

        _recordingService.LogReceived += (_, message) =>
            RunOnUiThread(() => AppendOutput($"[录制] {message}"));
        _recordingService.StatusChanged += (_, status) =>
            RunOnUiThread(() =>
            {
                RecordingStatus = status;
                RecordedRequestCount = _recordingService.CurrentSession?.Requests.Count ?? 0;
            });
        _recordingService.RequestCountChanged += (_, count) =>
            RunOnUiThread(() => RecordedRequestCount = count);
        _recordingService.BridgeConnectionChanged += (_, connected) =>
            RunOnUiThread(() => IsBridgeConnected = connected);

        _recordingService.StartBridgeServer();

        _ = InitializeWorkspaceSessionsAsync();
    }

    public ObservableCollection<WorkspaceSessionItemViewModel> WorkspaceSessions { get; }

    private WorkspaceSessionItemViewModel? _selectedWorkspaceSession;

    public WorkspaceSessionItemViewModel? SelectedWorkspaceSession
    {
        get => _selectedWorkspaceSession;
        set
        {
            if (!SetProperty(ref _selectedWorkspaceSession, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanDeleteWorkspaceSession));
            DeleteCurrentWorkspaceSessionCommand.RaiseCanExecuteChanged();

            if (_suppressSessionSelectionChange || value is null)
            {
                return;
            }

            if (value.SessionId == _currentWorkspaceSessionId)
            {
                return;
            }

            if (!CanSwitchWorkspaceSession)
            {
                RestoreSelectedSessionItem();
                return;
            }

            _ = SwitchToWorkspaceSessionAsync(value.SessionId);
        }
    }

    public bool CanSwitchWorkspaceSession => CanCreateNewSession;

    public bool CanDeleteWorkspaceSession =>
        CanCreateNewSession && SelectedWorkspaceSession is not null;

    public string OutputText
    {
        get => _outputText;
        private set => SetProperty(ref _outputText, value);
    }

    public RecordingStatus RecordingStatus
    {
        get => _recordingStatus;
        private set
        {
            if (SetProperty(ref _recordingStatus, value))
            {
                OnPropertyChanged(nameof(RecordingStatusText));
                OnPropertyChanged(nameof(CanCreateNewSession));
                OnPropertyChanged(nameof(CanSwitchWorkspaceSession));
                OnPropertyChanged(nameof(CanDeleteWorkspaceSession));
                OnPropertyChanged(nameof(CreateNewSessionTooltip));
                RaiseCommandStates();
            }
        }
    }

    public string RecordingStatusText => RecordingStatus switch
    {
        RecordingStatus.Idle => "未录制",
        RecordingStatus.Recording => "录制中",
        RecordingStatus.Stopped => "已停止",
        _ => RecordingStatus.ToString()
    };

    public bool IsBridgeConnected
    {
        get => _isBridgeConnected;
        private set
        {
            if (SetProperty(ref _isBridgeConnected, value))
            {
                OnPropertyChanged(nameof(BridgeStatusText));
                RaiseCommandStates();
            }
        }
    }

    public string BridgeStatusText =>
        IsBridgeConnected ? "Chrome 扩展：已连接" : "Chrome 扩展：未连接";

    public int RecordedRequestCount
    {
        get => _recordedRequestCount;
        private set => SetProperty(ref _recordedRequestCount, value);
    }

    public string RecordingStepsPrompt
    {
        get => _recordingStepsPrompt;
        set
        {
            if (SetProperty(ref _recordingStepsPrompt, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string SpiderGoalPrompt
    {
        get => _spiderGoalPrompt;
        set
        {
            if (SetProperty(ref _spiderGoalPrompt, value))
            {
                RaiseCommandStates();
            }
        }
    }

    private int _maxSelfTestAttempts = 3;
    private bool _autoInstallRequirementsOnTest = true;
    private string _pythonInterpreterPath = "python";

    /// <summary>
    /// 自测最大重试次数。0 表示不限制（一直尝试直到成功或用户手动停止）。
    /// 每次“开始分析”（包括后续的继续优化）都会独立使用当前这个值，不跨轮次累加。
    /// </summary>
    public int MaxSelfTestAttempts
    {
        get => _maxSelfTestAttempts;
        set
        {
            if (value < 0) value = 0;
            SetProperty(ref _maxSelfTestAttempts, value);
        }
    }

    /// <summary>
    /// 自测前是否使用指定 Python 解释器执行 pip install -r requirements.txt。
    /// </summary>
    public bool AutoInstallRequirementsOnTest
    {
        get => _autoInstallRequirementsOnTest;
        set => SetProperty(ref _autoInstallRequirementsOnTest, value);
    }

    /// <summary>
    /// 自测与 pip install 使用的 Python 解释器路径（默认 python，即 PATH 中的解释器）。
    /// </summary>
    public string PythonInterpreterPath
    {
        get => _pythonInterpreterPath;
        set => SetProperty(ref _pythonInterpreterPath, string.IsNullOrWhiteSpace(value) ? "python" : value.Trim());
    }

    public string ChromeExtensionId => ChromeExtensionConstants.ExtensionId;

    public string AnalyzeButtonText => IsAnalyzing ? "停止分析" : "开始分析";

    public bool CanStartRecording =>
        RecordingStatus == RecordingStatus.Idle && !IsAnalyzing;

    public bool CanStopRecording => RecordingStatus == RecordingStatus.Recording;

    public bool CanStartAnalyze =>
        RecordingStatus == RecordingStatus.Stopped
        && _recordingService.CurrentSession is not null
        && HasValidCombinedPrompt();

    public bool CanAnalyzeOrStop => IsAnalyzing || CanStartAnalyze;

    private bool HasValidCombinedPrompt()
        => !string.IsNullOrWhiteSpace(RecordingStepsPrompt)
           && !string.IsNullOrWhiteSpace(SpiderGoalPrompt);

    private string BuildCombinedUserPrompt()
    {
        var steps = RecordingStepsPrompt.Trim();
        var goal = SpiderGoalPrompt.Trim();
        return $"【录制过程中的操作步骤】\n{steps}\n\n【期望爬虫实现的目标】\n{goal}";
    }

    private void ClearPromptFields()
    {
        RecordingStepsPrompt = string.Empty;
        SpiderGoalPrompt = string.Empty;
    }

    /// <summary>
    /// 仅当未在录制、未在分析时允许创建新会话（须用户先手动停止当前操作）。
    /// </summary>
    public bool CanCreateNewSession =>
        RecordingStatus != RecordingStatus.Recording && !IsAnalyzing;

    public string CreateNewSessionTooltip
    {
        get
        {
            if (IsAnalyzing)
            {
                return "当前正在分析中。请先点击「停止分析」，再创建新会话。";
            }

            if (RecordingStatus == RecordingStatus.Recording)
            {
                return "当前正在录制中。请先点击「停止录制」，再创建新会话。";
            }

            return "清空当前工作区（内存中的录制会话与分析上下文），开始捕获新的目标。请先停止录制并保存后再使用。";
        }
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        private set
        {
            if (SetProperty(ref _isAnalyzing, value))
            {
                OnPropertyChanged(nameof(AnalyzeButtonText));
                OnPropertyChanged(nameof(CanCreateNewSession));
                OnPropertyChanged(nameof(CanSwitchWorkspaceSession));
                OnPropertyChanged(nameof(CanDeleteWorkspaceSession));
                OnPropertyChanged(nameof(CreateNewSessionTooltip));
                RaiseCommandStates();
            }
        }
    }

    public AsyncRelayCommand StartRecordingCommand { get; }
    public AsyncRelayCommand StopRecordingCommand { get; }
    public RelayCommand AnalyzeOrStopCommand { get; }
    public RelayCommand OpenSessionOutputDirectoryCommand { get; }
    public RelayCommand OpenExtensionFolderCommand { get; }
    public RelayCommand ClearOutputCommand { get; }
    public RelayCommand CreateNewSessionCommand { get; }
    public AsyncRelayCommand DeleteCurrentWorkspaceSessionCommand { get; }

    private void OnAnalyzeOrStop()
    {
        if (IsAnalyzing)
        {
            if (!ConfirmStopAnalysis())
            {
                return;
            }

            _analyzeCts?.Cancel();
            return;
        }

        _ = RunAnalyzeAsync();
    }

    private static bool ConfirmStopAnalysis()
    {
        return MessageBox.Show(
            "确定要停止当前分析吗？\n\n已生成的部分内容可能会保留在 Output 中。",
            "停止分析",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private async Task StartRecordingAsync()
    {
        try
        {
            if (!IsBridgeConnected)
            {
                AppendOutput("[系统] 扩展尚未连接。请确认 Chrome 已加载扩展，SpiderAgent 启动后约 1 秒内连接。");
            }

            await _recordingService.StartRecordingAsync(_currentWorkspaceSessionId);
            RecordedRequestCount = 0;
            _analysisSession = null;
        }
        catch (Exception ex)
        {
            AppendOutput($"[错误] 开始录制失败: {ex.Message}");
        }
    }

    private async Task StopRecordingAsync()
    {
        try
        {
            await _recordingService.StopRecordingAsync();
            RecordedRequestCount = _recordingService.CurrentSession?.Requests.Count ?? 0;
            if (_currentWorkspaceSessionId is not null)
            {
                await MarkWorkspaceHasRecordingAsync(_currentWorkspaceSessionId);
            }

            RaiseCommandStates();
        }
        catch (Exception ex)
        {
            AppendOutput($"[错误] 停止录制失败: {ex.Message}");
        }
    }

    private async Task RunAnalyzeAsync()
    {
        var session = _recordingService.CurrentSession;
        if (session is null)
        {
            return;
        }

        var prompt = BuildCombinedUserPrompt();
        _analyzeCts?.Cancel();
        _analyzeCts?.Dispose();
        _analyzeCts = new CancellationTokenSource();
        var cancellationToken = _analyzeCts.Token;

        IsAnalyzing = true;
        AppendUserPromptToOutput(prompt);

        try
        {
            var outputDirectory = RequireCurrentSessionOutputDirectory();
            Directory.CreateDirectory(outputDirectory);

            bool isRefinement = _analysisSession != null;

            if (!isRefinement)
            {
                // 首次分析：创建全新聊天会话 + 完整两阶段
                _analysisSession = _chatSessionFactory.Create(new ChatSessionOptions
                {
                    SystemPrompt = _analysisContextBuilder.BuildSystemPrompt(session, prompt),
                    CompletionOptions = new ChatCompletionOptions
                    {
                        Temperature = 0.1,
                        MaxTokens = 8192,
                        Stream = true
                    }
                });

                var analysisSession = _analysisSession;

                AppendOutput("[Agent] 第一阶段：逆向分析...");
                _outputLog.BeginStreamingBlock();
                var phase1Buffer = new StringBuilder();
                await StreamAnalysisAsync(
                    analysisSession,
                    RecordingAnalysisContextBuilder.BuildPhase1UserMessage(prompt),
                    phase1Buffer,
                    cancellationToken).ConfigureAwait(true);
                _outputLog.EndStreamingBlock("[Agent] 逆向分析完成");
                if (phase1Buffer.Length > 0)
                {
                    var planPath = _fileLog.SaveAnalysisResponse(phase1Buffer.ToString());
                    AppendOutput($"[系统] 逆向分析报告: {planPath}");
                }

                if (_currentWorkspaceSessionId is not null)
                {
                    await UpdateSessionTitleFromFirstPromptAsync(prompt, cancellationToken);
                }

                AppendOutput("[Agent] 第二阶段：生成 Python（生成后将自动自测）...");
                _outputLog.BeginStreamingBlock();
                lock (_streamLock)
                {
                    _streamBuffer.Clear();
                }

                await StreamAnalysisAsync(
                    analysisSession,
                    RecordingAnalysisContextBuilder.Phase2UserMessage,
                    _streamBuffer,
                    cancellationToken,
                    useStreamLock: true).ConfigureAwait(true);
            }
            else
            {
                // 继续优化 / 迭代：复用已有聊天上下文，直接发新 prompt 作为跟进指令
                if (_analysisSession is null)
                {
                    AppendOutput("[错误] 检测到需要继续优化，但聊天会话已丢失，将作为全新分析处理。");
                    // 回退到创建新会话逻辑（简化处理）
                    _analysisSession = _chatSessionFactory.Create(new ChatSessionOptions
                    {
                        SystemPrompt = _analysisContextBuilder.BuildSystemPrompt(session, prompt),
                        CompletionOptions = new ChatCompletionOptions
                        {
                            Temperature = 0.1,
                            MaxTokens = 8192,
                            Stream = true
                        }
                    });
                }

                AppendOutput("[Agent] 继续优化（基于上一次分析和代码 + 新Prompt）...");
                _outputLog.BeginStreamingBlock();
                lock (_streamLock)
                {
                    _streamBuffer.Clear();
                }

                // 构造针对性的跟进消息，让Agent在已有历史基础上输出新版本
                string refinementUserMessage =
                    $"用户新的优化/修复要求如下：\n{prompt}\n\n" +
                    "请基于你之前输出的完整【逆向分析报告】、参数溯源表、会话洞察，以及已生成的 Python 代码（包括之前的所有 requirements 和脚本），" +
                    "结合本次新要求，输出**更新后的** requirements.txt（如果有变化）和完整的 Python 脚本。\n" +
                    "要求：\n" +
                    "- 保留之前正确、已验证可运行的部分，只针对新要求做必要修改或增强。\n" +
                    "- 继续严格遵守系统Prompt中的所有硬性规则（包括对「录制快照固定值（来源未知）」的注释要求、多请求分函数封装、自测信号打印等）。\n" +
                    "- 直接输出 ```text requirements、```filename（英文大驼峰 .py 文件名）和 ```python 脚本块，不要重复整篇分析报告。\n" +
                    "- 脚本成功时应打印清晰的 SUCCESS 信号。";

                await StreamAnalysisAsync(
                    _analysisSession!,
                    refinementUserMessage,
                    _streamBuffer,
                    cancellationToken,
                    useStreamLock: true).ConfigureAwait(true);
            }

            var fullResponse = GetStreamSnapshot();
            if (fullResponse.Length == 0)
            {
                _outputLog.EndStreamingBlock("[Agent] （无内容返回）");
                return;
            }

            var analysisLogPath = _fileLog.SaveAnalysisResponse(fullResponse);
            _outputLog.EndStreamingBlock($"[Agent] 分析完成（{fullResponse.Length} 字符）");
            AppendOutput($"[系统] 完整 Agent 回复: {analysisLogPath}");

            var pythonContent = ExtractPythonCode(fullResponse);
            var requirements = ExtractRequirementsText(fullResponse);

            string? preferredScriptFileName = null;
            if (isRefinement && _currentWorkspaceSessionId is not null)
            {
                var metadata = await _sessionStore.LoadWorkspaceMetadataAsync(
                    _currentWorkspaceSessionId,
                    cancellationToken);
                if (!string.IsNullOrWhiteSpace(metadata?.LastOutputScriptPath))
                {
                    preferredScriptFileName = Path.GetFileName(metadata.LastOutputScriptPath);
                }
            }

            // 新增：持久化 + 自测 + 必要时迭代修复
            var persistResult = await PersistAndSelfValidateWithRepairAsync(
                pythonContent,
                requirements,
                fullResponse,
                preferredScriptFileName,
                cancellationToken);

            if (_currentWorkspaceSessionId is not null)
            {
                await SaveWorkspaceAnalysisStateAsync(persistResult.ScriptPath);
                await PersistCurrentSessionOutputAsync();
            }

            RunOnUiThread(() => ShowAnalysisCompletedMessage(persistResult));
        }
        catch (OperationCanceledException)
        {
            var partial = GetStreamSnapshot();
            if (partial.Length > 0)
            {
                var partialPath = _fileLog.SaveAnalysisResponse(partial);
                _outputLog.EndStreamingBlock($"[Agent] 分析已取消（已接收 {partial.Length} 字符）");
                AppendOutput($"[系统] 已保存部分 Agent 回复: {partialPath}");
            }
            else
            {
                _outputLog.EndStreamingBlock("[Agent] 分析已取消。");
            }

            AppendOutput("[系统] 分析已取消。");
        }
        catch (Exception ex)
        {
            _outputLog.EndStreamingBlock($"[Agent] 分析失败: {ex.Message}");
            AppendOutput($"[错误] 分析失败: {ex.Message}");
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    private string GetStreamSnapshot()
    {
        lock (_streamLock)
        {
            return _streamBuffer.ToString();
        }
    }

    private string GetStreamPreview()
    {
        var full = GetStreamSnapshot();
        return FormatAgentDisplay(full);
    }

    private static string FormatAgentDisplay(string full)
    {
        if (full.Length <= AgentDisplayMaxChars)
        {
            return full;
        }

        var omitted = full.Length - AgentDisplayMaxChars;
        return $"...(已省略 {omitted} 字符，完整内容将写入日志文件)\n{full[^AgentDisplayMaxChars..]}";
    }

    private async Task StreamAnalysisAsync(
        IChatSession analysisSession,
        string message,
        StringBuilder targetBuffer,
        CancellationToken cancellationToken,
        bool useStreamLock = false)
    {
        await Task.Run(async () =>
        {
            await foreach (var chunk in analysisSession.StreamSendAsync(
                               message,
                               cancellationToken: cancellationToken)
                           .ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrEmpty(chunk.Delta))
                {
                    if (chunk.IsFinished)
                    {
                        break;
                    }

                    continue;
                }

                if (useStreamLock)
                {
                    lock (_streamLock)
                    {
                        targetBuffer.Append(chunk.Delta);
                    }
                }
                else
                {
                    lock (targetBuffer)
                    {
                        targetBuffer.Append(chunk.Delta);
                    }
                }

                var preview = useStreamLock
                    ? FormatAgentDisplay(GetStreamSnapshot())
                    : FormatAgentDisplay(targetBuffer.ToString());
                _outputLog.UpdateStreamingBlock($"[Agent] {preview}");

                if (chunk.IsFinished)
                {
                    break;
                }
            }
        }, cancellationToken).ConfigureAwait(true);
    }

    private static string ExtractPythonCode(string content)
    {
        const string start = "```python";
        const string end = "```";
        var startIndex = content.IndexOf(start, StringComparison.OrdinalIgnoreCase);
        if (startIndex >= 0)
        {
            startIndex += start.Length;
            var endIndex = content.IndexOf(end, startIndex, StringComparison.Ordinal);
            if (endIndex > startIndex)
            {
                return content[startIndex..endIndex].Trim();
            }
        }

        return content.Trim();
    }

    private static string? ExtractRequirementsText(string content)
    {
        foreach (var fence in new[] { "```text", "```txt" })
        {
            var startIndex = content.IndexOf(fence, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0)
            {
                continue;
            }

            startIndex += fence.Length;
            var endIndex = content.IndexOf("```", startIndex, StringComparison.Ordinal);
            if (endIndex > startIndex)
            {
                return content[startIndex..endIndex].Trim();
            }
        }

        return null;
    }

    private sealed record AnalysisPersistResult(
        string? ScriptPath,
        bool SelfTestPassed,
        bool HasPythonScript);

    /// <summary>
    /// 将生成的代码写入输出目录，并执行自测。
    /// 若自测失败，则利用同一 ChatSession 继续对话，让 Agent 基于失败日志修复代码，最多重试几次。
    /// 只有当某次自测通过，或达到最大重试次数，才结束。
    /// </summary>
    private async Task<AnalysisPersistResult> PersistAndSelfValidateWithRepairAsync(
        string initialPython,
        string? initialRequirements,
        string fullPhase2Response,
        string? preferredScriptFileName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(initialPython))
        {
            AppendOutput("[系统] 未从 Agent 回复中提取到 Python 代码。");
            return new AnalysisPersistResult(null, false, false);
        }

        int maxAttempts = MaxSelfTestAttempts;   // 从UI读取，每次分析独立使用

        var outputDirectory = RequireCurrentSessionOutputDirectory();
        Directory.CreateDirectory(outputDirectory);

        var extractedFileName = AgentScriptFileNameResolver.Extract(fullPhase2Response);
        var finalFileName = AgentScriptFileNameResolver.Resolve(extractedFileName, preferredScriptFileName);
        if (string.IsNullOrWhiteSpace(extractedFileName))
        {
            if (!string.IsNullOrWhiteSpace(preferredScriptFileName))
            {
                AppendOutput($"[系统] Agent 未提供脚本文件名，沿用已有文件名: {finalFileName}");
            }
            else
            {
                AppendOutput($"[系统] Agent 未提供脚本文件名，使用默认名: {finalFileName}");
            }
        }

        var scriptPath = Path.Combine(outputDirectory, finalFileName);
        var reqPath = Path.Combine(outputDirectory, "requirements.txt");

        string currentPython = initialPython;
        string? currentRequirements = initialRequirements;
        string lastFullResponseForContext = fullPhase2Response;

        await WriteScriptFilesAsync(scriptPath, reqPath, currentPython, currentRequirements, cancellationToken);

        AppendOutput($"[系统] Python 已生成: {scriptPath}");
        if (!string.IsNullOrWhiteSpace(currentRequirements))
        {
            AppendOutput($"[系统] requirements.txt 已生成: {reqPath}");
        }

        int attempt = 1;
        // maxAttempts == 0 表示不限制重试次数
        while (true)
        {
            string attemptLabel = (maxAttempts > 0)
                ? $"第 {attempt}/{maxAttempts} 次"
                : $"第 {attempt} 次（不限）";

            AppendOutput($"[自测] {attemptLabel} 执行验证...");

            var result = await _pythonValidator.ValidateAsync(
                scriptPath,
                File.Exists(reqPath) ? reqPath : null,
                outputDirectory,
                new PythonValidationOptions
                {
                    AutoInstallRequirements = AutoInstallRequirementsOnTest,
                    PythonInterpreterPath = PythonInterpreterPath,
                    Log = AppendOutput
                },
                cancellationToken);

            var durationSec = result.Duration.TotalSeconds.ToString("F1");

            if (result.Success)
            {
                var countInfo = result.ParsedItemCount.HasValue
                    ? $"（解析到约 {result.ParsedItemCount} 条结果）"
                    : "";
                AppendOutput($"[自测] ✓ 通过（退出码 0，耗时 {durationSec}s）{countInfo}。脚本已验证可运行。");
                return new AnalysisPersistResult(scriptPath, true, true);
            }

            // 失败
            AppendOutput($"[自测] ✗ {attemptLabel} 失败（退出码 {result.ExitCode}，耗时 {durationSec}s）");
            if (!string.IsNullOrWhiteSpace(result.FailureReason))
            {
                AppendOutput($"[自测] 失败原因: {result.FailureReason}");
            }

            bool reachedLimit = (maxAttempts > 0 && attempt >= maxAttempts);
            if (reachedLimit)
            {
                AppendOutput("[自测] 已达最大重试次数，最后生成的版本已保存到输出目录（可能仍需手动调整）。");
                return new AnalysisPersistResult(scriptPath, false, true);
            }

            attempt++;   // 进入下一次自测尝试（每次分析轮次独立计数）

            // 准备修复提示，继续用同一个 _analysisSession
            if (_analysisSession is null)
            {
                AppendOutput("[自测] 无法继续对话修复（会话已丢失），直接结束。");
                return new AnalysisPersistResult(scriptPath, false, true);
            }

            AppendOutput("[Agent] 根据自测失败日志请求修复...");

            var repairPrompt = BuildRepairPrompt(lastFullResponseForContext, result);

            _outputLog.BeginStreamingBlock();
            lock (_streamLock)
            {
                _streamBuffer.Clear();
            }

            try
            {
                await StreamAnalysisAsync(
                    _analysisSession,
                    repairPrompt,
                    _streamBuffer,
                    cancellationToken,
                    useStreamLock: true).ConfigureAwait(true);

                var newResponse = GetStreamSnapshot();
                if (string.IsNullOrWhiteSpace(newResponse))
                {
                    _outputLog.EndStreamingBlock("[Agent] 修复阶段无内容返回");
                    AppendOutput("[自测] 修复阶段未返回新代码，保留上一次版本。");
                    return new AnalysisPersistResult(scriptPath, false, true);
                }

                // 提取新代码并覆盖文件
                var newPython = ExtractPythonCode(newResponse);
                var newReq = ExtractRequirementsText(newResponse);

                if (string.IsNullOrWhiteSpace(newPython))
                {
                    _outputLog.EndStreamingBlock("[Agent] 修复阶段未提取到 Python 代码");
                    AppendOutput("[自测] 未能从修复回复中提取到可执行代码。");
                    return new AnalysisPersistResult(scriptPath, false, true);
                }

                currentPython = newPython;
                currentRequirements = newReq ?? currentRequirements;
                lastFullResponseForContext = newResponse;

                await WriteScriptFilesAsync(scriptPath, reqPath, currentPython, currentRequirements, cancellationToken);

                AppendOutput("[系统] 已用修复后的代码覆盖目标脚本文件，准备下一轮自测。");

                _outputLog.EndStreamingBlock($"[Agent] 修复回复已接收（{newResponse.Length} 字符）");
            }
            catch (Exception ex)
            {
                _outputLog.EndStreamingBlock($"[Agent] 修复阶段异常: {ex.Message}");
                AppendOutput($"[错误] 修复阶段失败: {ex.Message}");
                return new AnalysisPersistResult(scriptPath, false, true);
            }
        }
    }

    private static void ShowAnalysisCompletedMessage(AnalysisPersistResult result)
    {
        string message;
        MessageBoxImage icon;

        if (!result.HasPythonScript)
        {
            message = "分析已完成，但未从 Agent 回复中提取到可执行的 Python 脚本。\n\n请查看 Output 中的分析报告与完整回复。";
            icon = MessageBoxImage.Warning;
        }
        else if (result.SelfTestPassed)
        {
            message = $"分析已完成，脚本已通过自测。\n\n脚本路径：\n{result.ScriptPath}";
            icon = MessageBoxImage.Information;
        }
        else
        {
            message =
                "分析已完成，但自测未通过或已达最大重试次数。\n\n" +
                $"脚本路径：\n{result.ScriptPath}\n\n" +
                "请查看 Output 中的自测失败详情，或继续输入 Prompt 让 Agent 优化。";
            icon = MessageBoxImage.Warning;
        }

        MessageBox.Show(message, "分析完成", MessageBoxButton.OK, icon);
    }

    private static async Task WriteScriptFilesAsync(
        string scriptPath,
        string reqPath,
        string pythonContent,
        string? requirements,
        CancellationToken ct)
    {
        await File.WriteAllTextAsync(scriptPath, pythonContent, ct);
        if (!string.IsNullOrWhiteSpace(requirements))
        {
            await File.WriteAllTextAsync(reqPath, requirements, ct);
        }
    }

    private static string BuildRepairPrompt(string previousFullResponse, PythonScriptValidator.ValidationResult failure)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SpiderAgent 刚刚对你上一次生成的脚本进行了自动执行自测，失败了。");
        sb.AppendLine();
        sb.AppendLine("【上一次完整生成内容（含 requirements + python）】");
        sb.AppendLine("```");
        sb.AppendLine(previousFullResponse.Length > 12000 ? previousFullResponse[..12000] + "\n...(已截断)" : previousFullResponse);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("【自测执行结果】");
        sb.AppendLine($"- 退出码: {failure.ExitCode}");
        sb.AppendLine($"- 持续时间: {failure.Duration.TotalSeconds:F1} 秒");
        if (failure.ParsedItemCount.HasValue)
        {
            sb.AppendLine($"- 尝试解析到的结果数量: {failure.ParsedItemCount}");
        }
        sb.AppendLine();
        sb.AppendLine("标准输出（最后部分）:");
        sb.AppendLine("```");
        var outTrim = failure.StdOut.Length > 2000 ? failure.StdOut[^2000..] : failure.StdOut;
        sb.AppendLine(string.IsNullOrWhiteSpace(outTrim) ? "(无)" : outTrim);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("标准错误 / 关键错误信息:");
        sb.AppendLine("```");
        var errTrim = (failure.StdErr + "\n" + (failure.FailureReason ?? "")).Trim();
        if (errTrim.Length > 2500) errTrim = errTrim[^2500..];
        sb.AppendLine(string.IsNullOrWhiteSpace(errTrim) ? "(无)" : errTrim);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("请根据以上失败信息，**只输出修正后的完整代码**：");
        sb.AppendLine("- 先输出 ```text 块（requirements.txt，如果有更新）");
        sb.AppendLine("- 再输出 ```python 块（完整可运行脚本）");
        sb.AppendLine("- 修复阶段无需重复输出 ```filename；将覆盖已有脚本文件。");
        sb.AppendLine("- 必须继续遵守之前所有硬性规则（Accept-Encoding、参数提取、__real_wd 判定、rsv_dl=tb_pre、多请求分函数封装等）。");
        sb.AppendLine("- 确保脚本在成功时能打印清晰的成功信号（如“共找到 N 个...”），并正常退出（exit code 0）。");
        sb.AppendLine("- 不要重复整个逆向分析报告，只给可直接落地的修复版本。");

        return sb.ToString();
    }

    private void OpenSessionOutputDirectory()
    {
        if (_currentWorkspaceSessionId is null)
        {
            MessageBox.Show("请先选择或创建一个会话。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var directory = _sessionStore.GetSessionOutputDirectory(_currentWorkspaceSessionId);
            Directory.CreateDirectory(directory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppendOutput($"[错误] 无法打开会话目录: {ex.Message}");
        }
    }

    private void OnClearOutput()
    {
        _outputLog.Clear();
        _ = PersistCurrentSessionOutputAsync();
    }

    private string RequireCurrentSessionOutputDirectory()
    {
        if (_currentWorkspaceSessionId is null)
        {
            throw new InvalidOperationException("当前没有活动会话。");
        }

        var directory = _sessionStore.GetSessionOutputDirectory(_currentWorkspaceSessionId);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private async Task PersistCurrentSessionOutputAsync()
    {
        if (_currentWorkspaceSessionId is null)
        {
            return;
        }

        await _sessionStore.SaveOutputLogTextAsync(_currentWorkspaceSessionId, _outputLog.Text);
    }

    private async Task LoadSessionWorkspaceAsync(string sessionId)
    {
        _appPaths.EnsureSessionOutputDirectory(sessionId);
        var outputDirectory = _sessionStore.GetSessionOutputDirectory(sessionId);
        _fileLog.SetSessionOutputDirectory(outputDirectory);

        var outputText = await _sessionStore.LoadOutputLogTextAsync(sessionId);
        RunOnUiThread(() =>
        {
            _outputLog.LoadText(outputText);
            OutputText = _outputLog.Text;
        });

        if (string.IsNullOrWhiteSpace(outputText))
        {
            AppendStartupMessagesIfNeeded();
        }
    }

    private void AppendStartupMessagesIfNeeded()
    {
        if (_outputLog.Text.Length > 0)
        {
            return;
        }

        AppendOutput("[系统] SpiderAgent 已启动（WebSocket Bridge 模式）。");
        AppendOutput("[系统] Bridge 地址: ws://127.0.0.1:17654/");
        AppendOutput($"[系统] 固定扩展 ID: {ChromeExtensionConstants.ExtensionId}");
        AppendOutput($"[系统] 录制数据目录: {_sessionStore.GetSessionsRootDirectory()}");
        AppendOutput($"[系统] 会话输出目录: {_appPaths.OutputRoot}");
        AppendOutput($"[系统] 数据库: {_appPaths.DatabasePath}");
        AppendOutput($"[系统] Chrome 扩展目录: {_recordingService.GetExtensionDirectory()}");
        AppendOutput("[系统] 扩展会自动重连（与 F2B Bridge 相同机制），SpiderAgent 启动后约 1 秒内连接。");
        AppendOutput("[系统] 首次使用：在 Chrome 加载扩展（仅需一次），然后保持 Chrome 打开即可。");
    }

    private void OpenExtensionFolder()
    {
        var dir = _recordingService.GetExtensionDirectory();
        if (!Directory.Exists(dir))
        {
            AppendOutput($"[错误] 扩展目录不存在: {dir}");
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = dir,
            UseShellExecute = true
        });
    }

    /// <summary>
    /// 创建新会话：新建工作区条目并清空当前录制与分析上下文（不自动停止录制/分析）。
    /// </summary>
    private async void CreateNewSession()
    {
        if (!CanCreateNewSession)
        {
            return;
        }

        try
        {
            var sessionId = DateTime.Now.ToString("yyyyMMddHHmmss");
            var metadata = new WorkspaceSessionMetadata
            {
                SessionId = sessionId,
                CreatedAt = DateTimeOffset.Now,
                UpdatedAt = DateTimeOffset.Now
            };

            await _sessionStore.SaveWorkspaceMetadataAsync(metadata);
            _appPaths.EnsureSessionOutputDirectory(sessionId);

            var item = new WorkspaceSessionItemViewModel(metadata);
            WorkspaceSessions.Insert(0, item);

            await PersistCurrentSessionOutputAsync();

            _recordingService.ResetToIdleForNewSession(emitLog: false);
            _analysisSession = null;
            _currentWorkspaceSessionId = sessionId;
            RecordedRequestCount = 0;
            ClearPromptFields();

            _suppressSessionSelectionChange = true;
            SelectedWorkspaceSession = item;
            _suppressSessionSelectionChange = false;

            await LoadSessionWorkspaceAsync(sessionId);
            RaiseCommandStates();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"创建新会话失败：{ex.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task InitializeWorkspaceSessionsAsync()
    {
        try
        {
            var sessions = await _sessionStore.ListWorkspaceSessionsAsync();
            RunOnUiThread(() =>
            {
                WorkspaceSessions.Clear();
                foreach (var metadata in sessions)
                {
                    WorkspaceSessions.Add(new WorkspaceSessionItemViewModel(metadata));
                }
            });

            if (sessions.Count == 0)
            {
                CreateNewSession();
                return;
            }

            var latest = sessions[0];
            _suppressSessionSelectionChange = true;
            RunOnUiThread(() =>
            {
                SelectedWorkspaceSession = WorkspaceSessions.FirstOrDefault(item => item.SessionId == latest.SessionId)
                    ?? WorkspaceSessions[0];
            });
            _suppressSessionSelectionChange = false;

            await SwitchToWorkspaceSessionAsync(latest.SessionId);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"加载会话列表失败：{ex.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            CreateNewSession();
        }
    }

    private async Task SwitchToWorkspaceSessionAsync(string sessionId)
    {
        try
        {
            await PersistCurrentSessionOutputAsync();

            _currentWorkspaceSessionId = sessionId;
            await LoadSessionWorkspaceAsync(sessionId);

            var recording = await _sessionStore.LoadSessionAsync(sessionId);
            _recordingService.ResetToIdleForNewSession(emitLog: false);

            if (recording is not null)
            {
                _recordingService.LoadSession(recording, emitLog: false);
            }

            var history = await _sessionStore.LoadChatHistoryAsync(sessionId);

            _analysisSession = null;
            if (history is { Count: > 0 })
            {
                var messages = history
                    .Select(MapPersistedToChatMessage)
                    .ToList();

                _analysisSession = _chatSessionFactory.CreateFromHistory(
                    new ChatSessionOptions
                    {
                        CompletionOptions = new ChatCompletionOptions
                        {
                            Temperature = 0.1,
                            MaxTokens = 8192,
                            Stream = true
                        }
                    },
                    messages);
            }

            RecordedRequestCount = recording?.Requests.Count ?? 0;
            ClearPromptFields();

            RaiseCommandStates();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"加载会话失败：{ex.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task UpdateSessionTitleFromFirstPromptAsync(
        string firstPrompt,
        CancellationToken cancellationToken)
    {
        if (_currentWorkspaceSessionId is null)
        {
            return;
        }

        try
        {
            var metadata = await _sessionStore.LoadWorkspaceMetadataAsync(_currentWorkspaceSessionId)
                ?? new WorkspaceSessionMetadata
                {
                    SessionId = _currentWorkspaceSessionId,
                    CreatedAt = DateTimeOffset.Now
                };

            if (!string.IsNullOrWhiteSpace(metadata.Title) && metadata.Title != metadata.SessionId)
            {
                return;
            }

            metadata.FirstPrompt = firstPrompt;
            var title = await _sessionTitleGenerator.GenerateTitleAsync(firstPrompt, cancellationToken);
            metadata.Title = title;
            metadata.UpdatedAt = DateTimeOffset.Now;

            await _sessionStore.SaveWorkspaceMetadataAsync(metadata);
            UpdateWorkspaceSessionListItem(metadata);
            AppendOutput($"[系统] 会话标题已更新为：{title}");
        }
        catch (Exception ex)
        {
            AppendOutput($"[警告] 自动生成会话标题失败: {ex.Message}");
        }
    }

    private async Task SaveWorkspaceAnalysisStateAsync(string? lastScriptPath = null)
    {
        if (_currentWorkspaceSessionId is null)
        {
            return;
        }

        try
        {
            var metadata = await _sessionStore.LoadWorkspaceMetadataAsync(_currentWorkspaceSessionId)
                ?? new WorkspaceSessionMetadata
                {
                    SessionId = _currentWorkspaceSessionId,
                    CreatedAt = DateTimeOffset.Now
                };

            metadata.UpdatedAt = DateTimeOffset.Now;
            metadata.HasAnalysisHistory = _analysisSession is { History.Count: > 0 };
            metadata.HasRecording = _recordingService.CurrentSession is not null;

            if (!string.IsNullOrWhiteSpace(lastScriptPath))
            {
                metadata.LastOutputScriptPath = lastScriptPath;
            }

            if (_analysisSession is not null)
            {
                var persisted = _analysisSession.History
                    .Select(MapChatMessageToPersisted)
                    .ToList();
                await _sessionStore.SaveChatHistoryAsync(_currentWorkspaceSessionId, persisted);
            }

            await _sessionStore.SaveWorkspaceMetadataAsync(metadata);
            UpdateWorkspaceSessionListItem(metadata);
        }
        catch (Exception ex)
        {
            AppendOutput($"[警告] 保存会话状态失败: {ex.Message}");
        }
    }

    private async Task MarkWorkspaceHasRecordingAsync(string sessionId)
    {
        var metadata = await _sessionStore.LoadWorkspaceMetadataAsync(sessionId)
            ?? new WorkspaceSessionMetadata
            {
                SessionId = sessionId,
                CreatedAt = DateTimeOffset.Now
            };

        metadata.HasRecording = true;
        metadata.UpdatedAt = DateTimeOffset.Now;
        await _sessionStore.SaveWorkspaceMetadataAsync(metadata);
        UpdateWorkspaceSessionListItem(metadata);
    }

    private void UpdateWorkspaceSessionListItem(WorkspaceSessionMetadata metadata)
    {
        RunOnUiThread(() =>
        {
            var item = WorkspaceSessions.FirstOrDefault(s => s.SessionId == metadata.SessionId);
            item?.ApplyMetadata(metadata);
        });
    }

    private void RestoreSelectedSessionItem()
    {
        _suppressSessionSelectionChange = true;
        SelectedWorkspaceSession = WorkspaceSessions.FirstOrDefault(s => s.SessionId == _currentWorkspaceSessionId);
        _suppressSessionSelectionChange = false;
    }

    private static PersistedChatMessage MapChatMessageToPersisted(ChatMessage message)
        => new()
        {
            Role = message.Role.ToString(),
            Content = message.Content
        };

    private static ChatMessage MapPersistedToChatMessage(PersistedChatMessage message)
        => new()
        {
            Role = Enum.TryParse<ChatRole>(message.Role, ignoreCase: true, out var role)
                ? role
                : ChatRole.User,
            Content = message.Content
        };

    private void AppendOutput(string line) => _outputLog.Append(line);

    private void AppendUserPromptToOutput(string combinedPrompt)
    {
        AppendOutput("[用户] 整合后的 Prompt：");
        AppendOutput(combinedPrompt);
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void RaiseCommandStates()
    {
        StartRecordingCommand.RaiseCanExecuteChanged();
        StopRecordingCommand.RaiseCanExecuteChanged();
        AnalyzeOrStopCommand.RaiseCanExecuteChanged();
        CreateNewSessionCommand.RaiseCanExecuteChanged();
        DeleteCurrentWorkspaceSessionCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanSwitchWorkspaceSession));
        OnPropertyChanged(nameof(CanDeleteWorkspaceSession));
    }

    private async Task DeleteCurrentWorkspaceSessionAsync()
    {
        if (SelectedWorkspaceSession is null)
        {
            return;
        }

        await DeleteWorkspaceSessionAsync(SelectedWorkspaceSession);
    }

    private async Task DeleteWorkspaceSessionAsync(WorkspaceSessionItemViewModel item)
    {
        if (!CanDeleteWorkspaceSession)
        {
            return;
        }

        var displayTitle = item.DisplayTitle;

        var firstConfirm = MessageBox.Show(
            $"确定要删除会话「{displayTitle}」吗？\n\n此操作不可撤销。",
            "删除会话",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (firstConfirm != MessageBoxResult.Yes)
        {
            return;
        }

        var secondConfirm = MessageBox.Show(
            $"请再次确认：将永久删除会话「{displayTitle}」及其所有本地文件\n" +
            "（录制数据、对话历史、工作区元数据等）。",
            "再次确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (secondConfirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var sessionId = item.SessionId;
            var isCurrent = sessionId == _currentWorkspaceSessionId;

            await _sessionStore.DeleteSessionAsync(sessionId);

            RunOnUiThread(() =>
            {
                _suppressSessionSelectionChange = true;
                WorkspaceSessions.Remove(item);
                _suppressSessionSelectionChange = false;
            });

            if (isCurrent)
            {
                _currentWorkspaceSessionId = null;
                _analysisSession = null;
                _recordingService.ResetToIdleForNewSession(emitLog: false);
                RecordedRequestCount = 0;
                ClearPromptFields();

                if (WorkspaceSessions.Count > 0)
                {
                    var next = WorkspaceSessions[0];
                    _suppressSessionSelectionChange = true;
                    RunOnUiThread(() => SelectedWorkspaceSession = next);
                    _suppressSessionSelectionChange = false;
                    await SwitchToWorkspaceSessionAsync(next.SessionId);
                }
                else
                {
                    CreateNewSession();
                }
            }

            RaiseCommandStates();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"删除会话失败：{ex.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
