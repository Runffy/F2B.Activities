namespace SpiderAgent.Chat.Configuration;

public sealed class ChatModuleOptions
{
    public const string SectionName = "Chat";

    /// <summary>
    /// 当前启用的提供商名称，对应 <see cref="Constants.ChatProviderNames"/> 中的常量。
    /// </summary>
    public string Provider { get; set; } = Constants.ChatProviderNames.DeepSeek;

    public DeepSeekProviderOptions DeepSeek { get; set; } = new();

    public InternalModelProviderOptions InternalModel { get; set; } = new();
}
