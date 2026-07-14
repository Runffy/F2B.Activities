using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace F2B.Browser.Chromium.Cdp.Launcher
{
    internal static class SettingsStore
    {
        private const int MaxHistoryItems = 50;

        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "F2B.Browser.Chromium.Cdp.Launcher");

        private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static LauncherSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return LauncherSettings.CreateDefault();
                }

                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<LauncherSettings>(json, JsonOptions);
                if (settings == null)
                {
                    return LauncherSettings.CreateDefault();
                }

                if (settings.History == null)
                {
                    settings.History = new List<LaunchHistoryEntry>();
                }

                if (settings.Port <= 0)
                {
                    settings.Port = 9222;
                }

                if (string.IsNullOrWhiteSpace(settings.BrowserType))
                {
                    settings.BrowserType = "Chrome";
                }

                if (string.IsNullOrWhiteSpace(settings.UserDataDirRoot))
                {
                    settings.UserDataDirRoot = @"C:\Temp";
                }

                return settings;
            }
            catch
            {
                return LauncherSettings.CreateDefault();
            }
        }

        public static void Save(LauncherSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            if (settings.History != null && settings.History.Count > MaxHistoryItems)
            {
                settings.History = settings.History.GetRange(0, MaxHistoryItems);
            }

            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }

        public static void RememberLaunch(LauncherSettings settings, LaunchHistoryEntry entry)
        {
            if (settings == null || entry == null)
            {
                return;
            }

            if (settings.History == null)
            {
                settings.History = new List<LaunchHistoryEntry>();
            }

            settings.BrowserType = entry.BrowserType;
            settings.ExecutablePath = entry.ExecutablePath;
            settings.Port = entry.Port;
            settings.UserDataDirRoot = entry.UserDataDirRoot;

            settings.History.RemoveAll(item =>
                item != null &&
                string.Equals(item.BrowserType, entry.BrowserType, StringComparison.OrdinalIgnoreCase) &&
                item.Port == entry.Port &&
                string.Equals(Normalize(item.UserDataDirRoot), Normalize(entry.UserDataDirRoot), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Normalize(item.ExecutablePath), Normalize(entry.ExecutablePath), StringComparison.OrdinalIgnoreCase));

            settings.History.Insert(0, entry);
            Save(settings);
        }

        private static string Normalize(string path)
        {
            return (path ?? string.Empty).Trim().TrimEnd('\\', '/');
        }
    }
}
