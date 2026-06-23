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
    private const int PipTimeoutMs = 90_000;
    public const string SelfTestEnvironmentVariable = "SPIDER_AGENT_SELF_TEST";

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
        var timeoutMs = options.TimeoutMs;
        if (timeoutMs > 0)
        {
            options.Log?.Invoke($"[自测] 脚本最长运行 {timeoutMs / 1000}s（0 可在 UI 设为不限）");
        }
        else
        {
            options.Log?.Invoke("[自测] 脚本运行不限时（适用于批量下载等长任务）");
        }

        options.Log?.Invoke("[自测] 已启用自测模式（环境变量 SPIDER_AGENT_SELF_TEST=1，脚本应仅验证最小子集）");

        var runResult = await RunPythonAsync(
            pythonExe,
            QuoteIfNeeded(fileName),
            workingDirectory,
            timeoutMs,
            selfTestMode: true,
            cancellationToken);

        var exitCode = runResult.ExitCode;
        var stdOut = runResult.StdOut;
        var stdErr = runResult.StdErr;
        var duration = runResult.Duration;

        var combined = (stdOut + "\n" + stdErr).Trim();

        bool hasTraceback = combined.Contains("Traceback (most recent call last)", StringComparison.OrdinalIgnoreCase)
                            || combined.Contains("AssertionError", StringComparison.OrdinalIgnoreCase);

        bool hasExplicitFailure = HasExplicitFailureOutput(combined);
        bool hasSuccessSignal = HasSuccessSignal(combined);

        // 通过条件：退出码 0、无 Traceback、无显式错误输出，且须出现成功信号（防止仅做环境检查后 return 0）
        bool looksLikeSuccess = exitCode == 0 && !hasTraceback && !hasExplicitFailure && hasSuccessSignal;

        int? itemCount = TryParseItemCount(combined);

        string? failureReason = null;
        if (!looksLikeSuccess)
        {
            if (exitCode != 0)
            {
                if (exitCode == -1 && combined.Contains("执行超时", StringComparison.OrdinalIgnoreCase))
                {
                    failureReason = timeoutMs > 0
                        ? $"自测执行超时（>{timeoutMs / 1000}s）。脚本可能仍在正常下载/处理，可手动运行 python 验证；或调大 UI「自测超时」/设为 0"
                        : "自测执行被取消";
                    if (HasInProgressOutput(combined))
                    {
                        failureReason += "（检测到下载/处理进度输出，并非逻辑立即失败）";
                    }
                }
                else
                {
                    failureReason = $"退出码 {exitCode}";
                }
            }
            else if (hasTraceback)
            {
                failureReason = "检测到 Traceback / AssertionError";
            }
            else if (hasExplicitFailure)
            {
                failureReason = "退出码为 0，但输出包含错误/失败信息（脚本可能用 print+return 代替了 sys.exit(1)）";
            }
            else if (!hasSuccessSignal)
            {
                failureReason = "退出码为 0，但未检测到成功完成信号（如 SUCCESS、共找到 N 个、成功下载 N 集等）";
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
                selfTestMode: false,
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
        bool selfTestMode,
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

        if (selfTestMode)
        {
            psi.Environment[SelfTestEnvironmentVariable] = "1";
        }

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
            if (timeoutMs > 0)
            {
                cts.CancelAfter(timeoutMs);
            }

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

    private static bool HasInProgressOutput(string combined)
        => Regex.IsMatch(
            combined,
            @"(Downloading|Progress:|segments downloaded|Episode \d+ completed|Processing Episode)",
            RegexOptions.IgnoreCase);

    private static bool HasExplicitFailureOutput(string combined)
    {
        if (string.IsNullOrWhiteSpace(combined))
        {
            return false;
        }

        if (HasZeroSuccessOutcome(combined))
        {
            return true;
        }

        foreach (var line in combined.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (Regex.IsMatch(trimmed, @"^错误\s*[:：]", RegexOptions.IgnoreCase)
                || Regex.IsMatch(trimmed, @"^失败\s*[:：]", RegexOptions.IgnoreCase)
                || Regex.IsMatch(trimmed, @"^(ERROR|FAILED)\s*[:：]", RegexOptions.IgnoreCase)
                || trimmed.Contains("请先安装", StringComparison.OrdinalIgnoreCase)
                || Regex.IsMatch(trimmed, @"未找到\s*(ffmpeg|ffprobe|node|chrome)", RegexOptions.IgnoreCase)
                || Regex.IsMatch(trimmed, @"下载第\s*\d+.*失败", RegexOptions.IgnoreCase)
                || trimmed.Contains("ffmpeg合并失败", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("ffmpeg 合并失败", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("合并失败", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("Conversion failed", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("Invalid data found", StringComparison.OrdinalIgnoreCase)
                || trimmed.Contains("Unable to find a suitable output format", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasZeroSuccessOutcome(string combined)
    {
        var fraction = Regex.Match(
            combined,
            @"成功下载\s*(\d+)\s*/\s*(\d+)",
            RegexOptions.IgnoreCase);
        if (fraction.Success
            && int.TryParse(fraction.Groups[1].Value, out var done)
            && done == 0)
        {
            return true;
        }

        var successCount = Regex.Match(
            combined,
            @"SUCCESS\s*[:：]\s*.*?(\d+)",
            RegexOptions.IgnoreCase);
        if (successCount.Success
            && int.TryParse(successCount.Groups[1].Value, out var n)
            && n == 0)
        {
            return true;
        }

        return false;
    }

    private static bool HasSuccessSignal(string combined)
    {
        if (string.IsNullOrWhiteSpace(combined))
        {
            return false;
        }

        if (HasExplicitFailureOutput(combined))
        {
            return false;
        }

        if (Regex.IsMatch(combined, @"SUCCESS\s*[:：]", RegexOptions.IgnoreCase))
        {
            var countMatch = Regex.Match(
                combined,
                @"SUCCESS\s*[:：][^\d]*(\d+)",
                RegexOptions.IgnoreCase);
            if (countMatch.Success
                && int.TryParse(countMatch.Groups[1].Value, out var n)
                && n <= 0)
            {
                return false;
            }

            return true;
        }

        var downloaded = Regex.Match(
            combined,
            @"成功下载\s*(\d+)\s*(?:/|\s*\/\s*|\s*集)",
            RegexOptions.IgnoreCase);
        if (downloaded.Success
            && int.TryParse(downloaded.Groups[1].Value, out var done)
            && done > 0)
        {
            return true;
        }

        var fetched = Regex.Match(combined, @"共获取到\s*(\d+)", RegexOptions.IgnoreCase);
        if (fetched.Success
            && int.TryParse(fetched.Groups[1].Value, out var fetchedCount)
            && fetchedCount > 0)
        {
            return true;
        }

        var searchResults = Regex.Match(combined, @"共找到\s*(\d+)\s*个(?:搜索)?结果", RegexOptions.IgnoreCase);
        if (searchResults.Success
            && int.TryParse(searchResults.Groups[1].Value, out var resultCount)
            && resultCount > 0)
        {
            return true;
        }

        return false;
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
