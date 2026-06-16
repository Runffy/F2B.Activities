namespace SpiderAgent.Chat;

public sealed class ChatProviderException : Exception
{
    public string ProviderName { get; }

    public ChatProviderException(string providerName, string message)
        : base($"[{providerName}] {message}")
    {
        ProviderName = providerName;
    }

    public ChatProviderException(string providerName, string message, Exception innerException)
        : base($"[{providerName}] {message}", innerException)
    {
        ProviderName = providerName;
    }
}
