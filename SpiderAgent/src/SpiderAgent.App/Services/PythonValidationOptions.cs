namespace SpiderAgent.App.Services;

public sealed class PythonValidationOptions
{
    public bool AutoInstallRequirements { get; init; }

    public string PythonInterpreterPath { get; init; } = "python";

    /// <summary>
    /// 自测脚本最长运行时间（毫秒）。0 表示不限制。
    /// </summary>
    public int TimeoutMs { get; init; }

    public Action<string>? Log { get; init; }
}
