using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>Runs MSHTML work in x86 ComHost when the OpenRPA plugin is x64.</summary>
    internal static class IeComHostDomBridge
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue
        };

        public static bool IsRemoteDomRequired => Environment.Is64BitProcess;

        public static IeComHostDomResponse Execute(IntPtr hwnd, IeComHostDomRequest request)
        {
            if (hwnd == IntPtr.Zero)
                throw new ArgumentException("HWND is required.", nameof(hwnd));

            var hostExe = IeComHostLocator.ResolveComHostPath();
            var workDir = IeComHostLocator.ResolveComHostWorkingDirectory();
            var reqFile = Path.Combine(Path.GetTempPath(), "f2b-dom-req-" + Guid.NewGuid().ToString("N") + ".json");
            var respFile = Path.Combine(Path.GetTempPath(), "f2b-dom-resp-" + Guid.NewGuid().ToString("N") + ".json");

            try
            {
                File.WriteAllText(reqFile, Serializer.Serialize(request));

                var args = string.Format(
                    CultureInfo.InvariantCulture,
                    "dom {0} \"{1}\" \"{2}\"",
                    hwnd.ToInt64(),
                    reqFile,
                    respFile);

                var psi = new ProcessStartInfo
                {
                    FileName = hostExe,
                    Arguments = args,
                    WorkingDirectory = workDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        throw new InvalidOperationException("Failed to start " + hostExe);

                    var stdout = process.StandardOutput.ReadToEnd();
                    var stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        var detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
                        throw new InvalidOperationException(
                            "IExplore.ComHost dom failed (exit " + process.ExitCode + "): " + detail.Trim());
                    }

                    if (!File.Exists(respFile))
                        throw new InvalidOperationException("ComHost dom produced no response file.");

                    return Serializer.Deserialize<IeComHostDomResponse>(File.ReadAllText(respFile));
                }
            }
            finally
            {
                TryDelete(reqFile);
                TryDelete(respFile);
            }
        }

        public static string SerializeLocatorElements(IELocator[] locators)
        {
            if (locators == null || locators.Length == 0)
                throw new ArgumentException("At least one locator is required.", nameof(locators));

            var elements = new List<string>(locators.Length);
            for (int i = 0; i < locators.Length; i++)
            {
                if (locators[i] == null)
                    throw new ArgumentException("Locator at index " + i + " is null.", nameof(locators));
                elements.Add(locators[i].Element);
            }

            return Serializer.Serialize(elements);
        }

        public static void EnsureOk(IeComHostDomResponse response, string operation)
        {
            if (response == null)
                throw new InvalidOperationException(operation + ": empty ComHost response.");
            if (!response.Ok)
                throw new InvalidOperationException(operation + " failed: " + (response.Error ?? "unknown error"));
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (path != null && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignore
            }
        }
    }
}
