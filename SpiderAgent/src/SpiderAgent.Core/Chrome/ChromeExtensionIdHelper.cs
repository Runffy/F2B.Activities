using System.Security.Cryptography;
using System.Text;

namespace SpiderAgent.Core.Chrome;

public static class ChromeExtensionIdHelper
{
    public static string ComputeExtensionId(ReadOnlySpan<byte> publicKeyDer)
    {
        var hash = SHA256.HashData(publicKeyDer);
        var builder = new StringBuilder(32);
        for (var i = 0; i < 16; i++)
        {
            builder.Append((char)('a' + (hash[i] >> 4)));
            builder.Append((char)('a' + (hash[i] & 0x0F)));
        }

        return builder.ToString();
    }

    public static string ComputeExtensionIdFromBase64Key(string publicKeyBase64)
        => ComputeExtensionId(Convert.FromBase64String(publicKeyBase64));
}
