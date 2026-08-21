using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace F2B.Forms.Engine
{
    /// <summary>
    /// Applies the OS user culture to the current thread so WinForms DateTimePicker / MonthCalendar
    /// follow the machine language instead of a host process forced culture (e.g. OpenRPA zh-CN).
    /// </summary>
    internal static class OsCulture
    {
        [DllImport("kernel32.dll")]
        private static extern int GetUserDefaultLCID();

        [DllImport("kernel32.dll")]
        private static extern ushort GetUserDefaultUILanguage();

        /// <summary>
        /// Sync CurrentCulture / CurrentUICulture (and Application.CurrentCulture) to Windows user settings.
        /// Safe to call repeatedly; does not change DefaultThreadCurrent* (avoids affecting the host process).
        /// </summary>
        public static void ApplyUserCultureToCurrentThread()
        {
            try
            {
                CultureInfo format = CultureInfo.GetCultureInfo(GetUserDefaultLCID());
                CultureInfo ui = CultureInfo.GetCultureInfo(GetUserDefaultUILanguage());

                Thread.CurrentThread.CurrentCulture = format;
                Thread.CurrentThread.CurrentUICulture = ui;

                // WinForms reads this for culture-aware common controls (calendar, etc.).
                Application.CurrentCulture = format;
            }
            catch
            {
                // Keep host culture if native lookup fails.
            }
        }
    }
}
