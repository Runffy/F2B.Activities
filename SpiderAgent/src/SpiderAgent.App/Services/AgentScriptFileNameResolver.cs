using System.Text;
using System.Text.RegularExpressions;

namespace SpiderAgent.App.Services;

/// <summary>
/// 从 Agent 回复中提取并规范化 Python 脚本文件名（大驼峰英文 + .py）。
/// </summary>
public static class AgentScriptFileNameResolver
{
    private const string DefaultBaseName = "GeneratedSpider";
    private const int MaxBaseNameLength = 64;

    public static string? Extract(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        const string fence = "```filename";
        var fenceIndex = content.IndexOf(fence, StringComparison.OrdinalIgnoreCase);
        if (fenceIndex >= 0)
        {
            var startIndex = fenceIndex + fence.Length;
            var endIndex = content.IndexOf("```", startIndex, StringComparison.Ordinal);
            if (endIndex > startIndex)
            {
                var fromFence = content[startIndex..endIndex].Trim();
                if (!string.IsNullOrWhiteSpace(fromFence))
                {
                    return fromFence;
                }
            }
        }

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("SCRIPT_FILENAME:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = trimmed["SCRIPT_FILENAME:".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    public static string Resolve(string? rawName, string? fallbackRawName = null)
    {
        var baseName = NormalizeBaseName(rawName);
        if (string.IsNullOrEmpty(baseName))
        {
            baseName = NormalizeBaseName(fallbackRawName);
        }

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = DefaultBaseName;
        }

        return baseName.EndsWith(".py", StringComparison.OrdinalIgnoreCase)
            ? baseName
            : $"{baseName}.py";
    }

    private static string NormalizeBaseName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var value = raw.Trim().Trim('"', '\'', '`', ' ', '\r');
        if (value.EndsWith(".py", StringComparison.OrdinalIgnoreCase))
        {
            value = value[..^3];
        }

        if (value.Contains('_') || value.Contains('-') || value.Contains(' '))
        {
            var parts = value.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries);
            value = string.Concat(parts.Select(ToPascalCasePart));
        }

        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
        }

        var result = sb.ToString();
        if (result.Length == 0)
        {
            return string.Empty;
        }

        if (!char.IsLetter(result[0]))
        {
            result = "Spider" + result;
        }

        if (char.IsLower(result[0]))
        {
            result = char.ToUpperInvariant(result[0]) + result[1..];
        }

        if (result.Length > MaxBaseNameLength)
        {
            result = result[..MaxBaseNameLength];
        }

        return Regex.IsMatch(result, @"^[A-Z][a-zA-Z0-9]*$")
            ? result
            : string.Empty;
    }

    private static string ToPascalCasePart(string part)
    {
        part = part.Trim();
        if (part.Length == 0)
        {
            return string.Empty;
        }

        if (part.Length == 1)
        {
            return char.ToUpperInvariant(part[0]).ToString();
        }

        return char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant();
    }
}
