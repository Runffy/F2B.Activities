using SpiderAgent.Core.Chrome;

namespace SpiderAgent.Recording.Configuration;

public sealed class RecordingOptions
{
    public const string SectionName = "Recording";

    public string ChromeExtensionRelativePath { get; set; } = "../../../chrome-extension";

    /// <summary>
    /// 固定扩展 ID，与 chrome-extension/manifest.json 中的 key 对应。
    /// </summary>
    public string ChromeExtensionId { get; set; } = ChromeExtensionConstants.ExtensionId;
}
