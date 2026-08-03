using System;
using System.Collections.Generic;
using F2B.Browser.Chromium.Cdp.Browser;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class CdpKeyInput
    {
        private static readonly Dictionary<string, int> ModifierBit = new Dictionary<string, int>
        {
            { "\uE00A", 1 },
            { "\uE009", 2 },
            { "\uE03D", 4 },
            { "\uE008", 8 }
        };

        internal static void Type(CdpTabSession session, IList<CdpKey> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return;
            }

            // Process each CdpKey as its own chord. Modifiers must be released before the next
            // key; otherwise Ctrl+A + Backspace + "X" becomes Ctrl+A, Ctrl+Backspace, Ctrl+X (cut).
            foreach (var key in keys)
            {
                if (key == null)
                {
                    continue;
                }

                TypeChord(session, key);
            }
        }

        private static void TypeChord(CdpTabSession session, CdpKey key)
        {
            var pressedModifiers = new List<string>();
            var modifier = 0;

            foreach (var token in key.Tokens)
            {
                if (string.IsNullOrEmpty(token))
                {
                    continue;
                }

                foreach (var character in token)
                {
                    var ch = character.ToString();
                    if (IsModifierToken(ch))
                    {
                        int bit;
                        if (ModifierBit.TryGetValue(ch, out bit))
                        {
                            modifier |= bit;
                            pressedModifiers.Add(ch);
                        }

                        var modifierDown = MakeInputData(modifier, ch, false);
                        if (modifierDown != null)
                        {
                            session.Send("Input.dispatchKeyEvent", modifierDown);
                        }

                        continue;
                    }

                    var down = MakeInputData(modifier, ch, false);
                    if (down != null)
                    {
                        session.Send("Input.dispatchKeyEvent", down);
                        down["type"] = "keyUp";
                        session.Send("Input.dispatchKeyEvent", down);
                    }
                    else
                    {
                        session.Send("Input.dispatchKeyEvent", new Dictionary<string, object>
                        {
                            { "type", "char" },
                            { "text", ch }
                        });
                    }
                }
            }

            for (var i = pressedModifiers.Count - 1; i >= 0; i--)
            {
                var modifierToken = pressedModifiers[i];
                var up = MakeInputData(modifier, modifierToken, true);
                if (up != null)
                {
                    session.Send("Input.dispatchKeyEvent", up);
                }

                int bit;
                if (ModifierBit.TryGetValue(modifierToken, out bit))
                {
                    modifier &= ~bit;
                }
            }
        }

        internal static void InputTextOrKeys(CdpTabSession session, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (text.EndsWith("\n", StringComparison.Ordinal) ||
                text.EndsWith("\uE007", StringComparison.Ordinal) ||
                text.EndsWith("\uE006", StringComparison.Ordinal))
            {
                var body = text.Substring(0, text.Length - 1);
                if (!string.IsNullOrEmpty(body))
                {
                    session.Send("Input.insertText", new Dictionary<string, object> { { "text", body } });
                }

                SendKey(session, 0, "\n");
                return;
            }

            session.Send("Input.insertText", new Dictionary<string, object> { { "text", text } });
        }

        private static void SendKey(CdpTabSession session, int modifier, string key)
        {
            var down = MakeInputData(modifier, key, false);
            if (down != null)
            {
                session.Send("Input.dispatchKeyEvent", down);
                down["type"] = "keyUp";
                session.Send("Input.dispatchKeyEvent", down);
                return;
            }

            session.Send("Input.insertText", new Dictionary<string, object> { { "text", key } });
        }

        private static bool IsModifierToken(string token)
        {
            return token == "\uE009" || token == "\uE008" || token == "\uE00A" || token == "\uE03D";
        }

        private static Dictionary<string, object> MakeInputData(int modifiers, string key, bool keyUp)
        {
            CdpKeyDefinition definition;
            if (!CdpKeyDefinitions.TryGet(key, out definition))
            {
                return null;
            }

            var shift = (modifiers & 8) != 0;
            var result = new Dictionary<string, object>
            {
                { "modifiers", modifiers },
                { "autoRepeat", false }
            };

            if (shift && !string.IsNullOrEmpty(definition.ShiftKey))
            {
                result["key"] = definition.ShiftKey;
                result["text"] = definition.ShiftKey;
            }
            else if (!string.IsNullOrEmpty(definition.Key))
            {
                result["key"] = definition.Key;
            }

            object keyValue;
            if (result.TryGetValue("key", out keyValue) && keyValue != null && keyValue.ToString().Length == 1)
            {
                result["text"] = definition.Key;
            }

            if (shift && definition.ShiftKeyCode.HasValue)
            {
                result["windowsVirtualKeyCode"] = definition.ShiftKeyCode.Value;
            }
            else if (definition.KeyCode.HasValue)
            {
                result["windowsVirtualKeyCode"] = definition.KeyCode.Value;
            }

            if (!string.IsNullOrEmpty(definition.Code))
            {
                result["code"] = definition.Code;
            }

            if (definition.Location.HasValue)
            {
                result["location"] = definition.Location.Value;
                result["isKeypad"] = definition.Location.Value == 3;
            }
            else
            {
                result["location"] = 0;
                result["isKeypad"] = false;
            }

            if (shift && !string.IsNullOrEmpty(definition.ShiftText))
            {
                result["text"] = definition.ShiftText;
                result["unmodifiedText"] = definition.ShiftText;
            }
            else if (!string.IsNullOrEmpty(definition.Text))
            {
                result["text"] = definition.Text;
                result["unmodifiedText"] = definition.Text;
            }

            if ((modifiers & ~8) != 0)
            {
                result["text"] = string.Empty;
            }

            object textValue;
            result["type"] = keyUp
                ? "keyUp"
                : (result.TryGetValue("text", out textValue) && textValue != null && textValue.ToString().Length > 0
                    ? "keyDown"
                    : "rawKeyDown");
            return result;
        }
    }
}
