using System;

namespace F2B.Terminal.PCOMM
{
    public enum PcommKey
    {
        Enter,
        Tab,
        Backspace,
        F1,
        F2,
        F3,
        F4,
        F5,
        F6,
        F7,
        F8,
        F9,
        F10,
        F11,
        F12
    }

    internal static class PcommKeyHelper
    {
        internal static string ToSendKeysValue(PcommKey key)
        {
            switch (key)
            {
                case PcommKey.Enter:
                    return "[enter]";
                case PcommKey.Tab:
                    return "[tab]";
                case PcommKey.Backspace:
                    return "[backspace]";
                case PcommKey.F1:
                    return "[pf1]";
                case PcommKey.F2:
                    return "[pf2]";
                case PcommKey.F3:
                    return "[pf3]";
                case PcommKey.F4:
                    return "[pf4]";
                case PcommKey.F5:
                    return "[pf5]";
                case PcommKey.F6:
                    return "[pf6]";
                case PcommKey.F7:
                    return "[pf7]";
                case PcommKey.F8:
                    return "[pf8]";
                case PcommKey.F9:
                    return "[pf9]";
                case PcommKey.F10:
                    return "[pf10]";
                case PcommKey.F11:
                    return "[pf11]";
                case PcommKey.F12:
                    return "[pf12]";
                default:
                    throw new ArgumentOutOfRangeException(nameof(key), key, "Unsupported PCOMM key.");
            }
        }

        internal static string ToDisplayName(PcommKey key)
        {
            switch (key)
            {
                case PcommKey.Enter:
                    return "ENTER";
                case PcommKey.Tab:
                    return "TAB";
                case PcommKey.Backspace:
                    return "BACKSPACE";
                default:
                    return key.ToString().ToUpperInvariant();
            }
        }

        internal static bool TryParseDisplayName(string text, out PcommKey key)
        {
            key = default(PcommKey);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            switch (text.Trim().ToUpperInvariant())
            {
                case "ENTER":
                    key = PcommKey.Enter;
                    return true;
                case "TAB":
                    key = PcommKey.Tab;
                    return true;
                case "BACKSPACE":
                    key = PcommKey.Backspace;
                    return true;
                case "F1":
                    key = PcommKey.F1;
                    return true;
                case "F2":
                    key = PcommKey.F2;
                    return true;
                case "F3":
                    key = PcommKey.F3;
                    return true;
                case "F4":
                    key = PcommKey.F4;
                    return true;
                case "F5":
                    key = PcommKey.F5;
                    return true;
                case "F6":
                    key = PcommKey.F6;
                    return true;
                case "F7":
                    key = PcommKey.F7;
                    return true;
                case "F8":
                    key = PcommKey.F8;
                    return true;
                case "F9":
                    key = PcommKey.F9;
                    return true;
                case "F10":
                    key = PcommKey.F10;
                    return true;
                case "F11":
                    key = PcommKey.F11;
                    return true;
                case "F12":
                    key = PcommKey.F12;
                    return true;
                default:
                    return false;
            }
        }
    }
}
