using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal sealed class CdpDownloadManager
    {
        private readonly CdpBrowser _browser;
        private readonly object _syncRoot = new object();
        private CdpClient _client;
        private readonly Dictionary<string, CdpDownloadMission> _missions = new Dictionary<string, CdpDownloadMission>();
        private bool _running;
        private string _waitingTabId;
        private CdpDownloadSettings _pendingSettings;
        private CdpDownloadMission _currentMission;

        internal CdpDownloadManager(CdpBrowser browser)
        {
            _browser = browser;
        }

        internal void BeginExpectDownload(
            string tabId,
            string savePath,
            string rename,
            string suffix,
            CdpLocalFileExistsAction localFileExists)
        {
            EnsureRunning(savePath);
            lock (_syncRoot)
            {
                _waitingTabId = tabId;
                _pendingSettings = new CdpDownloadSettings
                {
                    Path = savePath,
                    Rename = rename,
                    Suffix = suffix,
                    WhenFileExists = localFileExists
                };
                _currentMission = null;
            }
        }

        internal CdpDownloadResult WaitForCompletion(int timeoutMs)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs <= 0 ? 30000 : timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                lock (_syncRoot)
                {
                    if (_currentMission != null && _currentMission.IsDone)
                    {
                        if (_currentMission.State == "skipped")
                        {
                            return null;
                        }

                        if (!string.IsNullOrEmpty(_currentMission.FinalPath))
                        {
                            return new CdpDownloadResult(_currentMission.FinalPath, _currentMission.SuggestedFileName);
                        }
                    }
                }

                Thread.Sleep(50);
            }

            throw new BrowserException("Download timed out.");
        }

        internal void EndExpectDownload()
        {
            lock (_syncRoot)
            {
                _waitingTabId = null;
                _pendingSettings = null;
            }
        }

        private void EnsureRunning(string downloadPath)
        {
            lock (_syncRoot)
            {
                var absolutePath = Path.GetFullPath(downloadPath ?? Environment.CurrentDirectory);
                Directory.CreateDirectory(absolutePath);

                if (!_running)
                {
                    _client = new CdpClient(_browser.BrowserWebSocketUrl);
                    _client.Start();
                    _client.SetCallback("Browser.downloadWillBegin", OnDownloadWillBegin, immediate: true);
                    _client.SetCallback("Browser.downloadProgress", OnDownloadProgress, immediate: true);
                    _running = true;
                }

                _client.Send("Browser.setDownloadBehavior", new Dictionary<string, object>
                {
                    { "behavior", "allowAndName" },
                    { "downloadPath", absolutePath },
                    { "eventsEnabled", true }
                });
            }
        }

        private void OnDownloadWillBegin(Dictionary<string, object> parameters)
        {
            lock (_syncRoot)
            {
                if (_waitingTabId == null || _pendingSettings == null)
                {
                    return;
                }
            }

            var guid = CdpValueConverter.GetString(parameters, "guid");
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            CdpDownloadSettings settings;
            lock (_syncRoot)
            {
                settings = _pendingSettings;
            }

            var mission = new CdpDownloadMission
            {
                Id = guid,
                Settings = settings
            };
            mission.SuggestedFileName = CdpValueConverter.GetString(parameters, "suggestedFilename") ?? string.Empty;
            var fileName = ResolveFileName(mission);
            var goalPath = Path.Combine(mission.Settings.Path, fileName);
            mission.GoalPath = goalPath;

            if (File.Exists(goalPath))
            {
                if (mission.Settings.WhenFileExists == CdpLocalFileExistsAction.Skip)
                {
                    mission.State = "skipped";
                    mission.IsDone = true;
                    CancelDownload(guid);
                    lock (_syncRoot)
                    {
                        _currentMission = mission;
                        _missions[guid] = mission;
                    }

                    return;
                }

                if (mission.Settings.WhenFileExists == CdpLocalFileExistsAction.AutoRename)
                {
                    goalPath = GetUsablePath(goalPath);
                    mission.GoalPath = goalPath;
                }
            }

            mission.State = "started";
            lock (_syncRoot)
            {
                _currentMission = mission;
                _missions[guid] = mission;
            }
        }

        private void OnDownloadProgress(Dictionary<string, object> parameters)
        {
            var guid = CdpValueConverter.GetString(parameters, "guid");
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            CdpDownloadMission mission;
            lock (_syncRoot)
            {
                if (!_missions.TryGetValue(guid, out mission))
                {
                    return;
                }
            }

            var state = CdpValueConverter.GetString(parameters, "state");
            if (string.Equals(state, "completed", StringComparison.OrdinalIgnoreCase))
            {
                // allowAndName saves the blob under the download guid; rename to the intended file name.
                mission.FinalPath = FinalizeDownloadedFile(mission);
                mission.State = "completed";
                mission.IsDone = true;
            }
            else if (string.Equals(state, "canceled", StringComparison.OrdinalIgnoreCase))
            {
                mission.State = "canceled";
                mission.IsDone = true;
            }
        }

        private static string FinalizeDownloadedFile(CdpDownloadMission mission)
        {
            var directory = mission.Settings == null ? null : mission.Settings.Path;
            if (string.IsNullOrWhiteSpace(directory))
            {
                return mission.GoalPath;
            }

            var sourcePath = Path.Combine(directory, mission.Id ?? string.Empty);
            var destinationPath = string.IsNullOrWhiteSpace(mission.GoalPath)
                ? sourcePath
                : mission.GoalPath;

            if (!File.Exists(sourcePath))
            {
                // Fallback: some Chromium builds may already write the suggested name.
                if (File.Exists(destinationPath))
                {
                    return destinationPath;
                }

                throw new BrowserException(
                    string.Format(
                        "Download completed but file was not found. Expected GUID file '{0}' or '{1}'.",
                        sourcePath,
                        destinationPath));
            }

            if (string.Equals(
                Path.GetFullPath(sourcePath),
                Path.GetFullPath(destinationPath),
                StringComparison.OrdinalIgnoreCase))
            {
                return destinationPath;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? directory);

            if (File.Exists(destinationPath))
            {
                if (mission.Settings.WhenFileExists == CdpLocalFileExistsAction.AutoRename)
                {
                    destinationPath = GetUsablePath(destinationPath);
                }
                else
                {
                    File.Delete(destinationPath);
                }
            }

            MoveFileWithRetry(sourcePath, destinationPath);
            return destinationPath;
        }

        private static void MoveFileWithRetry(string sourcePath, string destinationPath)
        {
            Exception lastError = null;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                try
                {
                    File.Move(sourcePath, destinationPath);
                    return;
                }
                catch (IOException ex)
                {
                    lastError = ex;
                    Thread.Sleep(50);
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastError = ex;
                    Thread.Sleep(50);
                }
            }

            throw new BrowserException(
                string.Format("Failed to rename downloaded file from '{0}' to '{1}'.", sourcePath, destinationPath),
                lastError);
        }

        private void CancelDownload(string guid)
        {
            try
            {
                _client.Send("Browser.cancelDownload", new Dictionary<string, object> { { "guid", guid } });
            }
            catch (BrowserException)
            {
            }
        }

        private static string ResolveFileName(CdpDownloadMission mission)
        {
            var settings = mission.Settings;
            var suggested = mission.SuggestedFileName ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(settings.Rename))
            {
                if (!string.IsNullOrWhiteSpace(settings.Suffix))
                {
                    return settings.Suffix.IndexOf('.') >= 0
                        ? settings.Rename + settings.Suffix.TrimStart('.')
                        : settings.Rename + "." + settings.Suffix;
                }

                var suggestedExt = Path.GetExtension(suggested);
                var renameExt = Path.GetExtension(settings.Rename);
                return string.IsNullOrEmpty(renameExt) && !string.IsNullOrEmpty(suggestedExt)
                    ? settings.Rename + suggestedExt
                    : settings.Rename;
            }

            if (!string.IsNullOrWhiteSpace(settings.Suffix))
            {
                var baseName = Path.GetFileNameWithoutExtension(suggested);
                return string.IsNullOrWhiteSpace(settings.Suffix)
                    ? baseName
                    : baseName + "." + settings.Suffix.TrimStart('.');
            }

            return suggested;
        }

        private static string GetUsablePath(string path)
        {
            if (!File.Exists(path))
            {
                return path;
            }

            var directory = Path.GetDirectoryName(path) ?? ".";
            var name = Path.GetFileNameWithoutExtension(path);
            var ext = Path.GetExtension(path);
            var index = 1;
            string candidate;
            do
            {
                candidate = Path.Combine(directory, string.Format("{0}({1}){2}", name, index, ext));
                index++;
            }
            while (File.Exists(candidate));

            return candidate;
        }

        private sealed class CdpDownloadMission
        {
            internal string Id { get; set; }

            internal CdpDownloadSettings Settings { get; set; }

            internal string SuggestedFileName { get; set; }

            internal string GoalPath { get; set; }

            internal string FinalPath { get; set; }

            internal string State { get; set; }

            internal bool IsDone { get; set; }
        }

        private sealed class CdpDownloadSettings
        {
            internal string Path { get; set; }

            internal string Rename { get; set; }

            internal string Suffix { get; set; }

            internal CdpLocalFileExistsAction WhenFileExists { get; set; }
        }
    }
}
