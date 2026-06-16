namespace SpiderAgent.App.Services;

public sealed class PythonValidationOptions
{
    public bool AutoInstallRequirements { get; init; }

    public string PythonInterpreterPath { get; init; } = "python";

    public Action<string>? Log { get; init; }
}
