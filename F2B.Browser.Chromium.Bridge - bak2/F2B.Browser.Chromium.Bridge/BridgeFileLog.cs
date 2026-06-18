using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace F2B.Browser.Chromium.Bridge
{
    /// <summary>
    /// Shared append-only log for all Bridge consumers (OpenRPA, Inspector, etc.).
    /// Each write opens the file briefly with FileShare.ReadWrite and closes immediately;
    /// no process keeps a long-lived handle. A cross-process mutex only guards one line at a time.
    /// </summary>
    public static class BridgeFileLog
    {
        private const string SharedLogMutexName = @"Local\F2B.Bridge.SharedLog";
        private const int MutexWaitMs = 2000;
        private const int IoRetryCount = 5;
        private const int IoRetryDelayMs = 30;

        private static readonly object InProcessSync = new object();
        private static readonly Mutex SharedLogMutex = new Mutex(false, SharedLogMutexName);
        private static readonly string SharedLogFilePath;

        static BridgeFileLog()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "F2B.Bridge",
                "logs");

            SharedLogFilePath = Path.Combine(folder, "bridge-demo.log");
        }

        public static string LogFilePath => SharedLogFilePath;

        public static void Write(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            var process = Process.GetCurrentProcess();
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") +
                       "  [" + process.ProcessName + ":" + process.Id + "]  " +
                       message + Environment.NewLine;

            try
            {
                AppendSharedLine(line);
            }
            catch
            {
            }
        }

        private static void AppendSharedLine(string line)
        {
            var ownsMutex = false;
            try
            {
                ownsMutex = SharedLogMutex.WaitOne(MutexWaitMs);
                if (!ownsMutex)
                    return;

                for (var attempt = 0; attempt < IoRetryCount; attempt++)
                {
                    try
                    {
                        AppendToFile(SharedLogFilePath, line);
                        return;
                    }
                    catch (IOException)
                    {
                        if (attempt + 1 >= IoRetryCount)
                            return;

                        Thread.Sleep(IoRetryDelayMs);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return;
                    }
                }
            }
            finally
            {
                if (ownsMutex)
                    SharedLogMutex.ReleaseMutex();
            }
        }

        private static void AppendToFile(string path, string line)
        {
            lock (InProcessSync)
            {
                var folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder))
                    Directory.CreateDirectory(folder);

                using (var stream = new FileStream(
                    path,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(line);
                    writer.Flush();
                }
            }
        }
    }
}
