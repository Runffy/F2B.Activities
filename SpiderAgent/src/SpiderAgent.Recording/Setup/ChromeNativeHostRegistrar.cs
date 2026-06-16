using Microsoft.Win32;
using SpiderAgent.Core.Bridge;
using SpiderAgent.Core.Chrome;

namespace SpiderAgent.Recording.Setup;

public static class ChromeNativeHostRegistrar
{
    private const string ChromeNativeHostsKey =
        @"Software\Google\Chrome\NativeMessagingHosts";

    public static void EnsureRegistered(string nativeHostExePath, string? manifestDirectory = null)
    {
        manifestDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SpiderAgent",
            "NativeHost");

        Register(nativeHostExePath, ChromeExtensionConstants.ExtensionId, manifestDirectory);
    }

    public static void Register(string nativeHostExePath, string extensionId, string manifestDirectory)
    {
        if (string.IsNullOrWhiteSpace(extensionId))
        {
            throw new InvalidOperationException("Chrome 扩展 ID 未配置。");
        }

        Directory.CreateDirectory(manifestDirectory);

        var manifestPath = Path.Combine(manifestDirectory, $"{BridgeConstants.NativeHostName}.json");
        var manifest = $$"""
        {
          "name": "{{BridgeConstants.NativeHostName}}",
          "description": "SpiderAgent Chrome Recorder Native Host",
          "path": "{{nativeHostExePath.Replace("\\", "\\\\")}}",
          "type": "stdio",
          "allowed_origins": [
            "chrome-extension://{{extensionId}}/"
          ]
        }
        """;

        File.WriteAllText(manifestPath, manifest);

        using var key = Registry.CurrentUser.CreateSubKey(
            $@"{ChromeNativeHostsKey}\{BridgeConstants.NativeHostName}",
            writable: true);

        key.SetValue(string.Empty, manifestPath);
    }

    public static bool IsRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            $@"{ChromeNativeHostsKey}\{BridgeConstants.NativeHostName}");
        return key?.GetValue(string.Empty) is string path && File.Exists(path);
    }
}
