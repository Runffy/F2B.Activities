using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace F2B.Forms.Engine
{
    /// <summary>
    /// Applies culture to the current thread for DateTimePicker / MonthCalendar.
    /// Explicit override (form Culture property) wins; otherwise Windows display language.
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

        /// <param name="overrideCultureName">
        /// Optional BCP-47 name from form property (e.g. en-US). Empty = Windows display language.
        /// </param>
        public static void ApplyToCurrentThread(string overrideCultureName = null)
        {
            try
            {
                CultureInfo culture = Resolve(overrideCultureName);
                if (culture == null)
                {
                    return;
                }

                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                Application.CurrentCulture = culture;

                int lcid = culture.LCID;
                if (lcid > 0)
                {
                    SetThreadLocale(lcid);
                    SetThreadUILanguage(unchecked((ushort)(lcid & 0xFFFF)));
                }
            }
            catch
            {
                // Keep host culture if apply fails.
            }
        }

        /// <summary>Backward-compatible alias. </summary>
        public static void ApplyUserCultureToCurrentThread()
        {
            ApplyToCurrentThread(null);
        }

        public static CultureInfo Resolve(string overrideCultureName)
        {
            CultureInfo explicitCulture = TryGetCulture(overrideCultureName);
            if (explicitCulture != null)
            {
                return explicitCulture;
            }

            foreach (string name in GetPreferredUiLanguageNames())
            {
                CultureInfo preferred = TryGetCulture(name);
                if (preferred != null)
                {
                    return preferred;
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

    /// <summary>
    /// Property-grid dropdown for form Culture: (System) / en-US / zh-CN / ...
    /// </summary>
    public sealed class FormCultureTypeConverter : TypeConverter
    {
        public static readonly string[] StandardCultures =
        {
            "",
            "en-US",
            "en-GB",
            "zh-CN",
            "zh-TW",
            "ja-JP",
            "ko-KR",
            "fr-FR",
            "de-DE",
            "es-ES"
        };

        public override bool GetStandardValuesSupported(ITypeDescriptorContext context)
        {
            return true;
        }

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context)
        {
            return false;
        }

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context)
        {
            return new StandardValuesCollection(StandardCultures);
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string s)
            {
                s = s.Trim();
                if (string.IsNullOrEmpty(s)
                    || string.Equals(s, "(System)", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s, "System", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                return s;
            }

            return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(
            ITypeDescriptorContext context,
            CultureInfo culture,
            object value,
            Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                string s = value as string;
                return string.IsNullOrWhiteSpace(s) ? "(System)" : s.Trim();
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
