using System;
using System.Collections.Generic;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal sealed class CdpKeyDefinition
    {
        internal int? KeyCode { get; set; }

        internal int? ShiftKeyCode { get; set; }

        internal string Key { get; set; }

        internal string ShiftKey { get; set; }

        internal string Code { get; set; }

        internal string Text { get; set; }

        internal string ShiftText { get; set; }

        internal int? Location { get; set; }
    }

    internal static class CdpKeyDefinitions
    {
        private static readonly Dictionary<string, CdpKeyDefinition> Definitions = BuildDefinitions();

        internal static bool TryGet(string key, out CdpKeyDefinition definition)
        {
            return Definitions.TryGetValue(key, out definition);
        }

        internal static bool IsSupported(string key)
        {
            return !string.IsNullOrEmpty(key) && Definitions.ContainsKey(key);
        }

        private static Dictionary<string, CdpKeyDefinition> BuildDefinitions()
        {
            var definitions = new Dictionary<string, CdpKeyDefinition>();

            for (var i = 0; i <= 9; i++)
            {
                var digit = i.ToString();
                Add(definitions, digit, 48 + i, "Digit" + i, digit);
            }

            for (var i = 0; i < 26; i++)
            {
                var lower = ((char)('a' + i)).ToString();
                var upper = ((char)('A' + i)).ToString();
                Add(definitions, lower, 65 + i, "Key" + upper, lower);
                Add(definitions, upper, 65 + i, "Key" + upper, upper);
            }

            Add(definitions, " ", 32, "Space", " ");
            Add(definitions, ";", 186, "Semicolon", ";", ":", 186);
            Add(definitions, "=", 187, "Equal", "=");
            Add(definitions, ",", 188, "Comma", ",", "<", 188);
            Add(definitions, "-", 189, "Minus", "-", "_", 189);
            Add(definitions, ".", 190, "Period", ".", ">", 190);
            Add(definitions, "/", 191, "Slash", "/", "?", 191);
            Add(definitions, "`", 192, "Backquote", "`", "~", 192);
            Add(definitions, "[", 219, "BracketLeft", "[", "{", 219);
            Add(definitions, "\\", 220, "Backslash", "\\", "|", 220);
            Add(definitions, "]", 221, "BracketRight", "]", "}", 221);
            Add(definitions, "'", 222, "Quote", "'", "\"", 222);

            // Shifted symbols (aligned with DrissionPage keyDefinitions)
            Add(definitions, "!", 49, "Digit1", "!");
            Add(definitions, "@", 50, "Digit2", "@");
            Add(definitions, "#", 51, "Digit3", "#");
            Add(definitions, "$", 52, "Digit4", "$");
            Add(definitions, "%", 53, "Digit5", "%");
            Add(definitions, "^", 54, "Digit6", "^");
            Add(definitions, "&", 55, "Digit7", "&");
            Add(definitions, "*", 56, "Digit8", "*");
            Add(definitions, "(", 57, "Digit9", "(");
            Add(definitions, ")", 48, "Digit0", ")");
            Add(definitions, ":", 186, "Semicolon", ":");
            Add(definitions, "<", 188, "Comma", "<");
            Add(definitions, "_", 189, "Minus", "_");
            Add(definitions, ">", 190, "Period", ">");
            Add(definitions, "?", 191, "Slash", "?");
            Add(definitions, "~", 192, "Backquote", "~");
            Add(definitions, "{", 219, "BracketLeft", "{");
            Add(definitions, "|", 220, "Backslash", "|");
            Add(definitions, "}", 221, "BracketRight", "}");
            Add(definitions, "\"", 222, "Quote", "\"");

            Add(definitions, "\n", 13, "Enter", "Enter", text: "\r");
            Add(definitions, "\r", 13, "Enter", "Enter", text: "\r");
            Add(definitions, "\uE007", 13, "Enter", "Enter", text: "\r");
            Add(definitions, "\uE006", 13, "NumpadEnter", "Enter", text: "\r", location: 3);
            Add(definitions, "\uE003", 8, "Backspace", "Backspace");
            Add(definitions, "\uE00D", 32, "Space", " ");
            Add(definitions, "\uE00E", 33, "PageUp", "PageUp");
            Add(definitions, "\uE00F", 34, "PageDown", "PageDown");
            Add(definitions, "\uE008", 16, "ShiftLeft", "Shift", location: 1);
            Add(definitions, "\uE009", 17, "ControlLeft", "Control", location: 1);
            Add(definitions, "\uE00A", 18, "AltLeft", "Alt", location: 1);
            Add(definitions, "\uE03D", 91, "MetaLeft", "Meta");
            Add(definitions, "\uE011", 36, "Home", "Home");
            Add(definitions, "\uE012", 37, "ArrowLeft", "ArrowLeft");
            Add(definitions, "\uE013", 38, "ArrowUp", "ArrowUp");
            Add(definitions, "\uE014", 39, "ArrowRight", "ArrowRight");
            Add(definitions, "\uE015", 40, "ArrowDown", "ArrowDown");
            Add(definitions, "\uE001", 3, "Abort", "Cancel");
            Add(definitions, "\uE002", 6, "Help", "Help");
            Add(definitions, "\uE004", 9, "Tab", "Tab", text: "\t");
            Add(definitions, "\uE005", 12, "Numpad5", "Clear", shiftKey: "5", shiftKeyCode: 101, location: 3);
            Add(definitions, "\uE00B", 19, "Pause", "Pause");
            Add(definitions, "\uE00C", 27, "Escape", "Escape");
            Add(definitions, "\uE010", 35, "End", "End");
            Add(definitions, "\uE016", 45, "Insert", "Insert");
            Add(definitions, "\uE017", 46, "Delete", "Delete");
            Add(definitions, "\uE018", 186, "Semicolon", ";", ":", 186);
            Add(definitions, "\uE019", 187, "NumpadEqual", "=", location: 3);

            Add(definitions, "\uE01A", 48, "Digit0", "0", ")", 48);
            Add(definitions, "\uE01B", 49, "Digit1", "1", "!", 49);
            Add(definitions, "\uE01C", 50, "Digit2", "2", "@", 50);
            Add(definitions, "\uE01D", 51, "Digit3", "3", "#", 51);
            Add(definitions, "\uE01E", 52, "Digit4", "4", "$", 52);
            Add(definitions, "\uE01F", 53, "Digit5", "5", "%", 53);
            Add(definitions, "\uE020", 54, "Digit6", "6", "^", 54);
            Add(definitions, "\uE021", 55, "Digit7", "7", "&", 55);
            Add(definitions, "\uE022", 56, "Digit8", "8", "*", 56);
            Add(definitions, "\uE023", 57, "Digit9", "9", "(", 57);
            Add(definitions, "\uE024", 106, "NumpadMultiply", "*", location: 3);
            Add(definitions, "\uE025", 107, "NumpadAdd", "+", location: 3);
            Add(definitions, "\uE027", 109, "NumpadSubtract", "-", location: 3);
            Add(definitions, "\uE028", 46, "NumpadDecimal", "\u0000", shiftKey: ".", shiftKeyCode: 110, location: 3);
            Add(definitions, "\uE029", 111, "NumpadDivide", "/", location: 3);

            Add(definitions, "\uE031", 112, "F1", "F1");
            Add(definitions, "\uE032", 113, "F2", "F2");
            Add(definitions, "\uE033", 114, "F3", "F3");
            Add(definitions, "\uE034", 115, "F4", "F4");
            Add(definitions, "\uE035", 116, "F5", "F5");
            Add(definitions, "\uE036", 117, "F6", "F6");
            Add(definitions, "\uE037", 118, "F7", "F7");
            Add(definitions, "\uE038", 119, "F8", "F8");
            Add(definitions, "\uE039", 120, "F9", "F9");
            Add(definitions, "\uE03A", 121, "F10", "F10");
            Add(definitions, "\uE03B", 122, "F11", "F11");
            Add(definitions, "\uE03C", 123, "F12", "F12");

            Add(definitions, "*", 106, "NumpadMultiply", "*", location: 3);
            Add(definitions, "+", 107, "NumpadAdd", "+", location: 3);
            Add(definitions, "-", 109, "NumpadSubtract", "-", location: 3);
            Add(definitions, "/", 111, "NumpadDivide", "/", location: 3);

            return definitions;
        }

        private static void Add(
            IDictionary<string, CdpKeyDefinition> definitions,
            string token,
            int keyCode,
            string code,
            string key,
            string shiftKey = null,
            int? shiftKeyCode = null,
            string text = null,
            int? location = null)
        {
            definitions[token] = new CdpKeyDefinition
            {
                KeyCode = keyCode,
                ShiftKeyCode = shiftKeyCode,
                Key = key,
                ShiftKey = shiftKey,
                Code = code,
                Text = text,
                Location = location
            };
        }
    }
}
