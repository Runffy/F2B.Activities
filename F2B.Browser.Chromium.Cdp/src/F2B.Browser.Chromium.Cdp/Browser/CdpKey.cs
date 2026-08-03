using System;
using System.Collections.Generic;
using F2B.Browser.Chromium.Cdp.Exceptions;
using F2B.Browser.Chromium.Cdp.Internal;

namespace F2B.Browser.Chromium.Cdp.Browser
{
    /// <summary>
    /// Special keyboard keys and chords, aligned with DrissionPage <c>Keys</c>.
    /// Use with <see cref="CdpElement.SendKeys"/>.
    /// </summary>
    public sealed class CdpKey
    {
        private static readonly string CtrlComm = ResolveCtrlComm();

        internal CdpKey(params string[] tokens)
        {
            Tokens = tokens ?? new string[0];
        }

        internal string[] Tokens { get; private set; }

        public static readonly CdpKey Null = new CdpKey("\uE000");
        public static readonly CdpKey Cancel = new CdpKey("\uE001");
        public static readonly CdpKey Help = new CdpKey("\uE002");
        public static readonly CdpKey Backspace = new CdpKey("\uE003");
        public static readonly CdpKey Tab = new CdpKey("\uE004");
        public static readonly CdpKey Clear = new CdpKey("\uE005");
        public static readonly CdpKey Return = new CdpKey("\uE006");
        public static readonly CdpKey Enter = new CdpKey("\uE007");
        public static readonly CdpKey Shift = new CdpKey("\uE008");
        public static readonly CdpKey Control = new CdpKey("\uE009");
        public static readonly CdpKey Ctrl = Control;
        public static readonly CdpKey Alt = new CdpKey("\uE00A");
        public static readonly CdpKey Pause = new CdpKey("\uE00B");
        public static readonly CdpKey Escape = new CdpKey("\uE00C");
        public static readonly CdpKey Space = new CdpKey("\uE00D");
        public static readonly CdpKey PageUp = new CdpKey("\uE00E");
        public static readonly CdpKey PageDown = new CdpKey("\uE00F");
        public static readonly CdpKey End = new CdpKey("\uE010");
        public static readonly CdpKey Home = new CdpKey("\uE011");
        public static readonly CdpKey Left = new CdpKey("\uE012");
        public static readonly CdpKey Up = new CdpKey("\uE013");
        public static readonly CdpKey Right = new CdpKey("\uE014");
        public static readonly CdpKey Down = new CdpKey("\uE015");
        public static readonly CdpKey Insert = new CdpKey("\uE016");
        public static readonly CdpKey Delete = new CdpKey("\uE017");
        public static readonly CdpKey Del = Delete;
        public static readonly CdpKey Semicolon = new CdpKey("\uE018");
        public static readonly CdpKey EqualSign = new CdpKey("\uE019");

        public static readonly CdpKey Meta = new CdpKey("\uE03D");
        public static readonly CdpKey Command = Meta;

        public static readonly CdpKey CtrlA = new CdpKey(CtrlComm, "a");
        public static readonly CdpKey CtrlC = new CdpKey(CtrlComm, "c");
        public static readonly CdpKey CtrlX = new CdpKey(CtrlComm, "x");
        public static readonly CdpKey CtrlV = new CdpKey(CtrlComm, "v");
        public static readonly CdpKey CtrlZ = new CdpKey(CtrlComm, "z");
        public static readonly CdpKey CtrlY = new CdpKey(CtrlComm, "y");

        public static readonly CdpKey Numpad0 = new CdpKey("\uE01A");
        public static readonly CdpKey Numpad1 = new CdpKey("\uE01B");
        public static readonly CdpKey Numpad2 = new CdpKey("\uE01C");
        public static readonly CdpKey Numpad3 = new CdpKey("\uE01D");
        public static readonly CdpKey Numpad4 = new CdpKey("\uE01E");
        public static readonly CdpKey Numpad5 = new CdpKey("\uE01F");
        public static readonly CdpKey Numpad6 = new CdpKey("\uE020");
        public static readonly CdpKey Numpad7 = new CdpKey("\uE021");
        public static readonly CdpKey Numpad8 = new CdpKey("\uE022");
        public static readonly CdpKey Numpad9 = new CdpKey("\uE023");
        public static readonly CdpKey Multiply = new CdpKey("\uE024");
        public static readonly CdpKey Add = new CdpKey("\uE025");
        public static readonly CdpKey Subtract = new CdpKey("\uE027");
        public static readonly CdpKey Decimal = new CdpKey("\uE028");
        public static readonly CdpKey Divide = new CdpKey("\uE029");

        public static readonly CdpKey F1 = new CdpKey("\uE031");
        public static readonly CdpKey F2 = new CdpKey("\uE032");
        public static readonly CdpKey F3 = new CdpKey("\uE033");
        public static readonly CdpKey F4 = new CdpKey("\uE034");
        public static readonly CdpKey F5 = new CdpKey("\uE035");
        public static readonly CdpKey F6 = new CdpKey("\uE036");
        public static readonly CdpKey F7 = new CdpKey("\uE037");
        public static readonly CdpKey F8 = new CdpKey("\uE038");
        public static readonly CdpKey F9 = new CdpKey("\uE039");
        public static readonly CdpKey F10 = new CdpKey("\uE03A");
        public static readonly CdpKey F11 = new CdpKey("\uE03B");
        public static readonly CdpKey F12 = new CdpKey("\uE03C");

        /// <summary>
        /// Creates a custom key or key chord from predefined <see cref="CdpKey"/> parts and/or supported characters.
        /// Supported characters include letters, digits, and symbols such as <c>,</c> <c>.</c> <c>/</c> <c>&lt;</c> <c>&gt;</c> <c>?</c> <c>!</c> <c>@</c> etc.
        /// </summary>
        /// <example>
        /// CdpKey.Custom(CdpKey.Ctrl, "D") — Ctrl+D
        /// CdpKey.Custom("D") — single D key
        /// </example>
        public static CdpKey Custom(params object[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                throw new ArgumentException("At least one key part is required.", "parts");
            }

            var tokens = new List<string>();
            foreach (var part in parts)
            {
                AppendCustomPart(tokens, part);
            }

            return new CdpKey(tokens.ToArray());
        }

        /// <summary>
        /// Creates a custom key from a single supported character or special key token string.
        /// </summary>
        public static CdpKey Custom(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException("key");
            }

            return Custom((object)key);
        }

        public override string ToString()
        {
            return Tokens.Length == 0 ? string.Empty : string.Join(string.Empty, Tokens);
        }

        private static string ResolveCtrlComm()
        {
            var platform = Environment.OSVersion.Platform;
            if (platform == PlatformID.MacOSX || platform == PlatformID.Unix)
            {
                var version = Environment.OSVersion.VersionString ?? string.Empty;
                if (version.IndexOf("Darwin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    version.IndexOf("macOS", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "\uE03D";
                }
            }

            return "\uE009";
        }

        private static void AppendCustomPart(ICollection<string> tokens, object part)
        {
            if (part == null)
            {
                throw new ArgumentNullException("parts");
            }

            var key = part as CdpKey;
            if (key != null)
            {
                foreach (var token in key.Tokens)
                {
                    tokens.Add(token);
                }

                return;
            }

            if (part is char)
            {
                AppendCustomText(tokens, ((char)part).ToString());
                return;
            }

            var text = part as string;
            if (text != null)
            {
                AppendCustomText(tokens, text);
                return;
            }

            throw new ArgumentException(
                string.Format("Unsupported key part type: {0}. Use CdpKey or string.", part.GetType().Name),
                "parts");
        }

        private static void AppendCustomText(ICollection<string> tokens, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                throw new ArgumentException("Key text cannot be empty.", "parts");
            }

            if (text.Length == 1)
            {
                EnsureSupported(text);
                tokens.Add(text);
                return;
            }

            foreach (var character in text)
            {
                var token = character.ToString();
                EnsureSupported(token);
                tokens.Add(token);
            }
        }

        private static void EnsureSupported(string token)
        {
            if (!CdpKeyDefinitions.IsSupported(token))
            {
                throw new BrowserException(string.Format("Unsupported key: '{0}'.", token));
            }
        }
    }
}
