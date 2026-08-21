using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace F2B.Forms.Engine
{
    /// <summary>
    /// Applies Windows <b>display / UI language</b> to the current thread so DateTimePicker /
    /// MonthCalendar chrome matches Settings → Time &amp; language → Windows display language.
    /// Does not use regional format LCID (GetUserDefaultLCID), which can stay zh-CN on an English UI.
    /// Also overrides host-forced cultures (e.g. OpenRPA zh-CN) for this thread only.
    /// </summary>
    internal static class OsCulture
    {
        private const uint MuiLanguageName = 0x8;

        [DllImport("kernel32.dll")]
        private static extern ushort GetUserDefaultUILanguage();

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetUserPreferredUILanguages(
            uint dwFlags,
            out uint pulNumLanguages,
            char[] pwszLanguagesBuffer,
            ref uint pcchLanguagesBuffer);

        [DllImport("kernel32.dll")]
        private static extern bool SetThreadLocale(int localeId);

        [DllImport("kernel32.dll")]
        private static extern ushort SetThreadUILanguage(ushort langId);

        /// <summary>
        /// Sync thread/.NET/Win32 locale to Windows display language.
        /// Safe to call repeatedly; does not change DefaultThreadCurrent* (avoids affecting the host process).
        /// </summary>
        public static void ApplyUserCultureToCurrentThread()
        {
            try
            {
                CultureInfo ui = ResolveWindowsDisplayCulture();
                if (ui == null)
                {
                    return;
                }

                Thread.CurrentThread.CurrentCulture = ui;
                Thread.CurrentThread.CurrentUICulture = ui;

                // DateTimePicker calendar follows Application.CurrentCulture + Win32 thread locale.
                Application.CurrentCulture = ui;

                int lcid = ui.LCID;
                if (lcid > 0)
                {
                    SetThreadLocale(lcid);
                    SetThreadUILanguage(unchecked((ushort)(lcid & 0xFFFF)));
                }
            }
            catch
            {
                // Keep host culture if lookup/apply fails.
            }
        }

        /// <summary>
        /// Prefer preferred UI language list, then GetUserDefaultUILanguage.
        /// Never uses GetUserDefaultLCID (regional format).
        /// </summary>
        private static CultureInfo ResolveWindowsDisplayCulture()
        {
            foreach (string name in GetPreferredUiLanguageNames())
            {
                CultureInfo culture = TryGetCulture(name);
                if (culture != null)
                {
                    return culture;
                }
            }

            try
            {
                return CultureInfo.GetCultureInfo(GetUserDefaultUILanguage());
            }
            catch
            {
                return null;
            }
        }

        private static CultureInfo TryGetCulture(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            try
            {
                // zh-Hans-CN → zh-CN, etc.
                return CultureInfo.GetCultureInfo(name.Trim());
            }
            catch
            {
                try
                {
                    return CultureInfo.CreateSpecificCulture(name.Trim());
                }
                catch
                {
                    return null;
                }
            }
        }

        private static string[] GetPreferredUiLanguageNames()
        {
            try
            {
                uint num = 0;
                uint len = 0;
                GetUserPreferredUILanguages(MuiLanguageName, out num, null, ref len);
                if (len == 0)
                {
                    return Array.Empty<string>();
                }

                var buffer = new char[len];
                if (!GetUserPreferredUILanguages(MuiLanguageName, out num, buffer, ref len))
                {
                    return Array.Empty<string>();
                }

                return new string(buffer).Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
