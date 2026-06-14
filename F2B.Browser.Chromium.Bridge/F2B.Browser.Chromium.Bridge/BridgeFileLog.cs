using System;
using System.IO;

namespace F2B.Browser.Chromium.Bridge
{
    public static class BridgeFileLog
    {
        private static readonly object Sync = new object();

        static BridgeFileLog()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "F2B.Bridge",
                "logs");

            LogFilePath = Path.Combine(folder, "bridge-demo.log");
        }

        public static string LogFilePath { get; }

        public static void Write(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine;

            lock (Sync)
            {
                var folder = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                using (var stream = new FileStream(
                    LogFilePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(line);
                }
            }
        }
    }
}
