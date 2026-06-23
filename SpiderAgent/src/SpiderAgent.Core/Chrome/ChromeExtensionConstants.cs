namespace SpiderAgent.Core.Chrome;

/// <summary>
/// 固定扩展公钥，保证 unpacked 扩展 ID 始终一致。
/// 公钥写入 chrome-extension/manifest.json 的 key 字段。
/// </summary>
public static class ChromeExtensionConstants
{
    public const string PublicKey =
        "MIIBCgKCAQEAnWqvE1bZWjUf0HjNpDZHR6kEg+FxteY0NmzuHr92dt37F25fKgmi2rnxYVz5gRTtRCgHTlGnvBgCSxIHOZlwijaxOVr0pYeqLWPe75RqHdapDz3YTF05eLRac1QaGe1Xrd+rfjRHxxOWoexaHwjnKlxKwJQyLXnlaXLvfsmw1gb9DTslshHt/8Y3xBNRBNGqmzG6cYyc2DbUC25nnn4DdIZVz+pZjLy7WPJmRMMM4hIU1ETMCMkW54QC22F9EqU4xdeC5s2yTKUKj0UTaH/D7XYHe2IilTrMQHNJLxK44CVc9cBomPbIXSEac2KbrsmC/AuzSjf6zjWR09fOfNxN7QIDAQAB";

    public const string ExtensionId = "gnhppcgfioceonalmhdjlgmogokhodaa";
}
