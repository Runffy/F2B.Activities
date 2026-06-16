using System.Security.Cryptography;
using SpiderAgent.Core.Chrome;

var computed = ChromeExtensionIdHelper.ComputeExtensionIdFromBase64Key(ChromeExtensionConstants.PublicKey);
Console.WriteLine($"Expected: {ChromeExtensionConstants.ExtensionId}");
Console.WriteLine($"Computed: {computed}");
Console.WriteLine(computed == ChromeExtensionConstants.ExtensionId ? "OK" : "MISMATCH");
