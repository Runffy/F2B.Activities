using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SpiderAgent.App.Services;

/// <summary>
/// 负责在本地 Python 环境中执行 Agent 生成的爬虫脚本，判断是否“跑通”并返回详细结果。
/// 用于实现“生成后自测 -> 失败则迭代修复”的闭环。
/// </summary>
public sealed class PythonScriptValidator
{
    private const int DefaultTimeoutMs = 120_000;
    private const int PipTimeoutMs = 90_000;

    public sealed record ValidationResult(
        bool Success,
        int ExitCode,
        string StdOut,
        string StdErr,
        TimeSpan Duration,
        string? FailureReason,
        int? ParsedItemCount);

    public async Task<ValidationResult> ValidateAsync(
        string scriptPath,
        string? requirementsPath,
        string workingDirectory,
        PythonValidationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new PythonValidationOptions();
        var pythonExe = string.IsNullOrWhiteSpace(options.PythonInterpreterPath)
            ? "python"
            : options.PythonInterpreterPath.Trim();

        Directory.CreateDirectory(workingDirectory);

        if (options.AutoInstallRequirements
            && !string.IsNullOrWhiteSpace(requirementsPath)
            && File.Exists(requirementsPath))
        {
            await InstallRequirementsAsync(
                pythonExe,
                requirementsPath,
                workingDirectory,
                options.Log,
                cancellationToken);
        }

        var fileName = Path.GetFileName(scriptPath);
        var runResult = await RunPythonAsync(
            pythonExe,
            QuoteIfNeeded(fileName),
            workingDirectory,
            DefaultTimeoutMs,
            cancellationToken);

        var exitCode = runResult.ExitCode;
        var stdOut = runResult.StdOut;
        var stdErr = runResult.StdErr;
        var duration = runResult.Duration;

        var combined = (stdOut + "\n" + stdErr).Trim();

        bool hasTraceback = combined.Contains("Traceback (most recent call last)", StringComparison.OrdinalIgnoreCase)
                            || combined.Contains("AssertionError", StringComparison.OrdinalIgnoreCase);

        bool looksLikeSuccess = exitCode == 0 && !hasTraceback;

        int? itemCount = TryParseItemCount(combined);
        if (itemCount is > 0)
        {
            looksLikeSuccess = exitCode == 0 && !hasTraceback;
        }

        string? failureReason = null;
        if (!looksLikeSuccess)
        {
            if (exitCode != 0)
            {
                failureReason = $"退出码 {exitCode}";
            }
            else if (hasTraceback)
            {
                failureReason = "检测到 Traceback / AssertionError";
            }
            else
            {
                failureReason = "脚本未产生预期成功信号";
            }

            var keyError = ExtractKeyErrorSnippet(combined);
            if (!string.IsNullOrWhiteSpace(keyError))
            {
                failureReason += $"\n关键错误:\n{keyError}";
            }
        }

        return new ValidationResult(
            Success: looksLikeSuccess,
            ExitCode: exitCode,
            StdOut: stdOut,
            StdErr: stdErr,
            Duration: duration,
            FailureReason: failureReason,
            ParsedItemCount: itemCount);
    }

    private static async Task InstallRequirementsAsync(
        string pythonExe,
        string requirementsPath,
        string workingDir,
        Action<string>? log,
        CancellationToken ct)
    {
        var fullRequirementsPath = Path.GetFullPath(requirementsPath);
        var arguments = $"-m pip install -r {QuoteIfNeeded(fullRequirementsPath)} --disable-pip-version-check";
        log?.Invoke(
            $"[自测] 安装依赖: {QuoteIfNeeded(pythonExe)} {arguments}");

        try
        {
            var pipResult = await RunPythonAsync(
                pythonExe,
                arguments,
                workingDir,
                PipTimeoutMs,
                ct);

            if (pipResult.ExitCode != 0)
            {
                log?.Invoke($"[自测] pip install 失败（退出码 {pipResult.ExitCode}）");
                if (!string.IsNullOrWhiteSpace(pipResult.StdErr))
                {
                    log?.Invoke($"[自测] pip stderr: {pipResult.StdErr.Trim()}");
                }
            }
            else
            {
                log?.Invoke("[自测] pip install 完成。");
            }
        }
        catch (Exception ex)
        {
            log?.Invoke($"[自测] 安装依赖异常: {ex.Message}");
        }
    }

    private static string QuoteIfNeeded(string value)
        => value.Contains(' ') ? $"\"{value}\"" : value;

    private static async Task<(int ExitCode, string StdOut, string StdErr, TimeSpan Duration)> RunPythonAsync(
        string pythonExe,
        string arguments,
        string workingDirectory,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var start = DateTime.UtcNow;

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        if (!process.Start())
        {
            return (-1, string.Empty, $"无法启动 Python 进程「{pythonExe}」（请确认解释器路径正确）", TimeSpan.Zero);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            await process.WaitForExitAsync(cts.Token);

            var duration = DateTime.UtcNow - start;
            return (process.ExitCode, stdout.ToString(), stderr.ToString(), duration);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignore
            }

            var duration = DateTime.UtcNow - start;
            return (-1, stdout.ToString(), stderr.ToString() + "\n[Validator] 执行超时或被取消", duration);
        }
    }

    private static int? TryParseItemCount(string output)
    {
        var m = Regex.Match(output, @"共找到\s*(\d+)\s*个|找到\s*(\d+)\s*个|results?\s*[:：]?\s*(\d+)", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            for (int i = 1; i < m.Groups.Count; i++)
            {
                if (int.TryParse(m.Groups[i].Value, out var n) && n > 0)
                {
                    return n;
                }
            }
        }

        var m2 = Regex.Match(output, @"found\s+(\d+)|(\d+)\s+results?", RegexOptions.IgnoreCase);
        if (m2.Success && int.TryParse(m2.Groups[1].Success ? m2.Groups[1].Value : m2.Groups[2].Value, out var n2) && n2 > 0)
        {
            return n2;
        }

        return null;
    }

    private static string ExtractKeyErrorSnippet(string combined)
    {
        var tbIndex = combined.LastIndexOf("Traceback", StringComparison.OrdinalIgnoreCase);
        if (tbIndex >= 0)
        {
            var snippet = combined.Substring(tbIndex);
            return snippet.Length > 800 ? snippet[..800] + "..." : snippet;
        }

        var lines = combined.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var errorLines = lines
            .Where(l => l.Contains("错误", StringComparison.OrdinalIgnoreCase)
                        || l.Contains("失败", StringComparison.OrdinalIgnoreCase)
                        || l.Contains("Assertion", StringComparison.OrdinalIgnoreCase)
                        || l.Contains("Exception", StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        if (errorLines.Count > 0)
        {
            return string.Join("\n", errorLines);
        }

        return combined.Length > 300 ? combined[^300..] : combined;
    }
}
