using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace F2B.Browser.IExplore.COM;

public sealed class IEWindowController
{
	public sealed class IeScreenHitResult
	{
		public IEDomElement Element { get; set; }

		public IList<object> FramePath { get; set; }
	}

	private sealed class IeDocCandidate
	{
		public IntPtr TopHwnd { get; set; }

		public IntPtr DocHwnd { get; set; }

		public long Area { get; set; }
	}

	private sealed class ViewportOffsetCandidate
	{
		public int X { get; set; }

		public int Y { get; set; }
	}

	public sealed class EmbeddedIEComWindow
	{
		public IntPtr HWND { get; private set; }

		public object Document { get; private set; }

		public string TopTitle { get; private set; }

		public string TopClassName { get; private set; }

		public IntPtr DocHWND { get; private set; }

		public string FullName { get; private set; }

		public string LocationURL { get; private set; }

		internal EmbeddedIEComWindow(IntPtr topHwnd, string topTitle, string topClassName, IntPtr docHwnd, object document)
		{
			HWND = topHwnd;
			Document = document;
			TopTitle = topTitle ?? string.Empty;
			TopClassName = topClassName ?? string.Empty;
			DocHWND = docHwnd;
			FullName = "embedded-mshtml";
			LocationURL = ReadDocumentUrl(document);
		}

		public object refresh_document()
		{
			if (DocHWND != IntPtr.Zero && TryGetHtmlDocumentFromHwnd(DocHWND, out var document))
			{
				Update(document);
				return document;
			}
			foreach (IntPtr item in IterEmbeddedIeDocuments(HWND))
			{
				if (!TryGetHtmlDocumentFromHwnd(item, out document))
				{
					continue;
				}
				DocHWND = item;
				Update(document);
				return document;
			}
			return null;
		}

		private void Update(object document)
		{
			Document = document;
			TopTitle = SafeGetWindowText(HWND);
			TopClassName = SafeGetClassName(HWND);
			LocationURL = ReadDocumentUrl(document);
		}
	}

	public sealed class IEDomElement
	{
		private readonly IEWindowController _controller;

		private readonly object _element;

		private readonly object _document;

		public object raw => _element;

		internal IEDomElement(IEWindowController controller, object element, object document)
		{
			_controller = controller;
			_element = element;
			_document = document;
		}

		public IList<object> build_frame_path()
		{
			List<object> list = new List<object>();
			object instance = _controller.document_object();
			object obj = ReadDynamicProperty(_document, "parentWindow");
			object obj2 = ReadDynamicProperty(instance, "parentWindow");
			while (obj != null && obj2 != null && obj != obj2)
			{
				object obj3 = ReadDynamicProperty(obj, "frameElement");
				if (obj3 == null)
				{
					break;
				}
				object innerDocument = ReadDynamicProperty(obj, "document");
				list.Insert(0, ResolveFrameReference(obj3, innerDocument));
				obj = ReadDynamicProperty(obj, "parent");
			}
			return list;
		}

		public void set_value(object value, bool trigger_events = true, int delay_before = 0)
		{
			string text = SafeToString(value);
			DebugLog("set_value", "begin element={0}, value={1}, trigger_events={2}", DescribeSelf(), FormatAny(text), trigger_events);
			if (delay_before > 0)
			{
				Thread.Sleep(ToSleepMilliseconds(delay_before));
			}
			SafeRead(() => InvokeDynamicMethod(_element, "focus"));
			SetDynamicProperty(_element, "value", text);
			SafeRead(() => InvokeDynamicMethod(_element, "setAttribute", "value", text));
			if (trigger_events)
			{
				TriggerInputEvents();
			}
			string text2 = SafeToString(ReadDynamicProperty(_element, "value"));
			if (!IsEquivalentValue(text2, text))
			{
				string text3 = SafeToString(SafeRead(() => InvokeDynamicMethod(_element, "getAttribute", "value")));
				if (!IsEquivalentValue(text3, text))
				{
					DebugLog("set_value", "verify failed element={0}, actual.value={1}, actual.attr={2}", DescribeSelf(), FormatAny(text2), FormatAny(text3));
					throw new InvalidOperationException($"set_value did not take effect: expected={FormatAny(text)}, actual.value={FormatAny(text2)}, actual.attr.value={FormatAny(text3)}, element={DescribeSelf()}");
				}
			}
			DebugLog("set_value", "verify success element={0}, actual.value={1}", DescribeSelf(), FormatAny(text2));
		}

		public void native_click(int delay_before = 0, MouseButton button = MouseButton.Left)
		{
			DebugLog("native_click", "begin element={0}, button={1}", DescribeSelf(), button);
			if (delay_before > 0)
			{
				Thread.Sleep(ToSleepMilliseconds(delay_before));
			}
			SafeRead(() => InvokeDynamicMethod(_element, "focus"));
			if (button == MouseButton.Left)
			{
				bool flag = false;
				try
				{
					InvokeDynamicMethod(_element, "click");
					flag = true;
				}
				catch
				{
					flag = false;
				}
				if (!flag)
				{
					DispatchMouseClickEvents(MouseButton.Left);
				}
			}
			else
			{
				DispatchMouseClickEvents(button);
			}
			DebugLog("native_click", "completed element={0}, button={1}", DescribeSelf(), button);
		}

		public void select_option(string text = null, string value = null, int? index = null, string text_contains = null, string text_re = null, bool trigger_events = true, bool trigger_dblclick = false, int delay_before = 0)
		{
			if (delay_before > 0)
			{
				Thread.Sleep(ToSleepMilliseconds(delay_before));
			}
			SafeRead(() => InvokeDynamicMethod(_element, "focus"));
			List<object> list = new List<object>();
			object collection = ReadDynamicProperty(_element, "options") ?? _element;
			foreach (object item in EnumerateIndexedCollection(collection))
			{
				list.Add(item);
			}
			if (list.Count == 0)
			{
				throw new InvalidOperationException("Element is not a selectable dropdown or has no available options");
			}
			int? num = ResolveOptionIndex(list, text, value, index, text_contains, text_re);
			if (!num.HasValue)
			{
				throw new InvalidOperationException(string.Format("No matching option found: text={0}, value={1}, index={2}, text_contains={3}, text_re={4}", FormatAny(text), FormatAny(value), index.HasValue ? index.Value.ToString() : "null", FormatAny(text_contains), FormatAny(text_re)));
			}
			SetDynamicProperty(_element, "selectedIndex", num.Value);
			for (int i = 0; i < list.Count; i++)
			{
				SetDynamicProperty(list[i], "selected", i == num.Value);
			}
			int? num2 = SafeInt(ReadDynamicProperty(_element, "selectedIndex"));
			if (!num2.HasValue || num2.Value != num.Value)
			{
				throw new InvalidOperationException(string.Format("select_option did not take effect: expected.selectedIndex={0}, actual.selectedIndex={1}, element={2}", num.Value, num2.HasValue ? num2.Value.ToString() : "null", DescribeSelf()));
			}
			if (trigger_events)
			{
				TriggerSelectEvents(trigger_dblclick);
			}
		}

		public void check(bool trigger_events = true, int delay_before = 0)
		{
			SetCheckedState(targetChecked: true, trigger_events, delay_before);
		}

		public void uncheck(bool trigger_events = true, int delay_before = 0)
		{
			SetCheckedState(targetChecked: false, trigger_events, delay_before);
		}

		private void SetCheckedState(bool targetChecked, bool triggerEvents, int delayBefore)
		{
			if (delayBefore > 0)
			{
				Thread.Sleep(ToSleepMilliseconds(delayBefore));
			}
			SafeRead(() => InvokeDynamicMethod(_element, "focus"));
			string a = SafeToString(ReadDynamicProperty(_element, "type"));
			string a2 = SafeToString(ReadDynamicProperty(_element, "tagName"));
			if (!string.Equals(a2, "input", StringComparison.OrdinalIgnoreCase) || (!string.Equals(a, "checkbox", StringComparison.OrdinalIgnoreCase) && !string.Equals(a, "radio", StringComparison.OrdinalIgnoreCase)))
			{
				throw new InvalidOperationException("Target element is not a checkbox/radio; cannot check/uncheck");
			}
			bool flag = IsChecked();
			if (flag != targetChecked)
			{
				SetDynamicProperty(_element, "checked", targetChecked);
				SafeRead(() => InvokeDynamicMethod(_element, "setAttribute", "checked", targetChecked ? "checked" : null));
				if (triggerEvents)
				{
					TriggerCheckEvents();
				}
				bool flag2 = IsChecked();
				if (flag2 != targetChecked)
				{
					throw new InvalidOperationException(string.Format("{0} did not take effect: expected.checked={1}, actual.checked={2}, element={3}", targetChecked ? "check" : "uncheck", targetChecked, flag2, DescribeSelf()));
				}
			}
		}

		private bool IsChecked()
		{
			try
			{
				object obj = ReadDynamicProperty(_element, "checked");
				if (obj == null)
				{
					return false;
				}
				return Convert.ToBoolean(obj);
			}
			catch
			{
				return false;
			}
		}

		private void TriggerCheckEvents()
		{
			FireLegacyEvent("onclick");
			FireLegacyEvent("onchange");
			DispatchStandardEvent("click");
			DispatchStandardEvent("input");
			DispatchStandardEvent("change");
		}

		public string get_text()
		{
			string text = NormalizeText(SafeToString(ReadDynamicProperty(_element, "innerText")));
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
			text = NormalizeText(SafeToString(ReadDynamicProperty(_element, "textContent")));
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
			text = SafeToString(ReadDynamicProperty(_element, "value"));
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
			return string.Empty;
		}

		public object get_attribute(string name, object defaultValue = null)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return defaultValue;
			}
			object obj = SafeRead(() => InvokeDynamicMethod(_element, "getAttribute", name));
			if (obj != null && !string.IsNullOrEmpty(SafeToString(obj)))
			{
				return obj;
			}
			obj = ReadDynamicProperty(_element, name);
			return obj ?? defaultValue;
		}

		public string get_tag_name()
		{
			string text = SafeToString(ReadDynamicProperty(_element, "tagName"));
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text.Trim();
			}
			text = SafeToString(ReadDynamicProperty(_element, "nodeName"));
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text.Trim().TrimStart('#');
			}
			return string.Empty;
		}

		public void set_attribute(string name, object value, bool trigger_events = true, int delay_before = 0)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				throw new ArgumentNullException("name");
			}
			DebugLog("set_attribute", "begin element={0}, name={1}, value={2}, trigger_events={3}", DescribeSelf(), FormatAny(name), FormatAny(value), trigger_events);
			if (delay_before > 0)
			{
				Thread.Sleep(ToSleepMilliseconds(delay_before));
			}
			SafeRead(() => InvokeDynamicMethod(_element, "focus"));
			SetDynamicProperty(_element, name, value);
			SafeRead(() => InvokeDynamicMethod(_element, "setAttribute", name, value));
			string text = SafeToString(value);
			string text2 = SafeToString(ReadDynamicProperty(_element, name));
			string text3 = SafeToString(SafeRead(() => InvokeDynamicMethod(_element, "getAttribute", name)));
			if (!IsEquivalentValue(text2, text) && !IsEquivalentValue(text3, text))
			{
				DebugLog("set_attribute", "verify failed element={0}, actual.property={1}, actual.attribute={2}", DescribeSelf(), FormatAny(text2), FormatAny(text3));
				throw new InvalidOperationException($"set_attribute did not take effect: name={FormatAny(name)}, expected={FormatAny(text)}, actual.property={FormatAny(text2)}, actual.attribute={FormatAny(text3)}, element={DescribeSelf()}");
			}
			DebugLog("set_attribute", "verify success element={0}, actual.property={1}, actual.attribute={2}", DescribeSelf(), FormatAny(text2), FormatAny(text3));
			if (trigger_events)
			{
				FireLegacyEvent("onchange");
				DispatchStandardEvent("change");
			}
		}

		private static bool IsEquivalentValue(string actual, string expected)
		{
			return string.Equals(NormalizeText(actual), NormalizeText(expected), StringComparison.Ordinal);
		}

		private string DescribeSelf()
		{
			string value = SafeToString(ReadDynamicProperty(_element, "tagName"));
			string value2 = SafeToString(ReadDynamicProperty(_element, "type"));
			string value3 = SafeToString(ReadDynamicProperty(_element, "id"));
			string value4 = SafeToString(ReadDynamicProperty(_element, "name"));
			return $"tag={FormatAny(value)}, type={FormatAny(value2)}, id={FormatAny(value3)}, name={FormatAny(value4)}";
		}

		public void press_keys(object keys, int delay_before = 0)
		{
			if (delay_before > 0)
			{
				Thread.Sleep(ToSleepMilliseconds(delay_before));
			}
			SafeRead(() => InvokeDynamicMethod(_element, "focus"));
			List<string> list = NormalizeKeyTokens(keys);
			if (list.Count == 0)
			{
				return;
			}
			bool ctrl = false;
			bool alt = false;
			bool shift = false;
			bool meta = false;
			string text = null;
			for (int i = 0; i < list.Count; i++)
			{
				string text2 = list[i];
				if (IsCtrlToken(text2))
				{
					ctrl = true;
				}
				else if (IsAltToken(text2))
				{
					alt = true;
				}
				else if (IsShiftToken(text2))
				{
					shift = true;
				}
				else if (IsMetaToken(text2))
				{
					meta = true;
				}
				else
				{
					text = text2;
				}
			}
			if (string.IsNullOrWhiteSpace(text))
			{
				text = list[list.Count - 1];
			}
			int keyCode = ResolveKeyCode(text);
			TriggerKeyboardEvents(keyCode, ctrl, alt, shift, meta);
			if (ShouldAppendTypedCharacter(text, ctrl, alt, meta))
			{
				string text3 = SafeToString(ReadDynamicProperty(_element, "value"));
				SetDynamicProperty(_element, "value", text3 + text);
			}
		}

		public void double_click(int delay_before = 0, MouseButton button = MouseButton.Left)
		{
			if (delay_before > 0)
			{
				Thread.Sleep(ToSleepMilliseconds(delay_before));
			}
			SafeRead(() => InvokeDynamicMethod(_element, "focus"));
			if (button == MouseButton.Left)
			{
				SafeRead(() => InvokeDynamicMethod(_element, "click"));
				SafeRead(() => InvokeDynamicMethod(_element, "click"));
				FireLegacyMouseEvent("ondblclick", MouseButton.Left);
				DispatchMouseEvent("dblclick", MouseButton.Left);
			}
			else
			{
				DispatchMouseClickEvents(button);
				DispatchMouseClickEvents(button);
				FireLegacyMouseEvent("ondblclick", button);
				DispatchMouseEvent("dblclick", button);
			}
		}

		public IEDomElement get_parent()
		{
			object obj = ReadDynamicProperty(_element, "parentElement") ?? ReadDynamicProperty(_element, "parentNode");
			if (obj == null)
			{
				return null;
			}
			return WrapElement(obj);
		}

		public IEDomElement[] get_children()
		{
			object collection = ReadDynamicProperty(_element, "children") ?? ReadDynamicProperty(_element, "childNodes");
			List<IEDomElement> list = new List<IEDomElement>();
			foreach (object item in EnumerateIndexedCollection(collection))
			{
				if (item != null)
				{
					list.Add(WrapElement(item));
				}
			}
			return list.ToArray();
		}

		public bool is_checked()
		{
			string a = SafeToString(ReadDynamicProperty(_element, "tagName"));
			string a2 = SafeToString(ReadDynamicProperty(_element, "type"));
			if (string.Equals(a, "input", StringComparison.OrdinalIgnoreCase) && (string.Equals(a2, "checkbox", StringComparison.OrdinalIgnoreCase) || string.Equals(a2, "radio", StringComparison.OrdinalIgnoreCase)))
			{
				return IsChecked();
			}
			if (string.Equals(a, "select", StringComparison.OrdinalIgnoreCase) || string.Equals(a2, "select-one", StringComparison.OrdinalIgnoreCase) || string.Equals(a2, "select-multiple", StringComparison.OrdinalIgnoreCase) || string.Equals(a2, "combobox", StringComparison.OrdinalIgnoreCase))
			{
				int? num = SafeInt(ReadDynamicProperty(_element, "selectedIndex"));
				return num.HasValue && num.Value >= 0;
			}
			throw new InvalidOperationException("is_checked only supports radio/checkbox and combobox (select)");
		}

		public string[] get_selected_options()
		{
			string a = SafeToString(ReadDynamicProperty(_element, "tagName"));
			if (!string.Equals(a, "select", StringComparison.OrdinalIgnoreCase))
			{
				throw new InvalidOperationException("get_selected_options only supports dropdown (select)");
			}
			object collection = ReadDynamicProperty(_element, "options");
			List<string> list = new List<string>();
			foreach (object item in EnumerateIndexedCollection(collection))
			{
				if (item == null)
				{
					continue;
				}
				bool flag = false;
				try
				{
					flag = Convert.ToBoolean(ReadDynamicProperty(item, "selected"));
				}
				catch
				{
					flag = false;
				}
				if (flag)
				{
					string text = NormalizeText(SafeToString(ReadDynamicProperty(item, "text")));
					if (string.IsNullOrWhiteSpace(text))
					{
						text = NormalizeText(SafeToString(ReadDynamicProperty(item, "innerText")));
					}
					if (string.IsNullOrWhiteSpace(text))
					{
						text = SafeToString(ReadDynamicProperty(item, "value"));
					}
					list.Add(text);
				}
			}
			return list.ToArray();
		}

		private IEDomElement WrapElement(object element)
		{
			object document = ReadDynamicProperty(element, "ownerDocument") ?? ReadDynamicProperty(element, "document") ?? _document;
			return new IEDomElement(_controller, element, document);
		}

		private static List<string> NormalizeKeyTokens(object keys)
		{
			List<string> list = new List<string>();
			if (keys == null)
			{
				return list;
			}
			if (keys is string text)
			{
				string[] array = text.Split(new char[1] { '+' }, StringSplitOptions.RemoveEmptyEntries);
				for (int i = 0; i < array.Length; i++)
				{
					string text2 = array[i].Trim();
					if (!string.IsNullOrWhiteSpace(text2))
					{
						list.Add(text2);
					}
				}
				if (list.Count == 0 && !string.IsNullOrWhiteSpace(text))
				{
					list.Add(text.Trim());
				}
				return list;
			}
			if (!(keys is IEnumerable enumerable))
			{
				list.Add(SafeToString(keys));
				return list;
			}
			foreach (object item in enumerable)
			{
				string text3 = SafeToString(item).Trim();
				if (!string.IsNullOrWhiteSpace(text3))
				{
					list.Add(text3);
				}
			}
			return list;
		}

		private static bool IsCtrlToken(string token)
		{
			return string.Equals(token, "CTRL", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "CONTROL", StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsAltToken(string token)
		{
			return string.Equals(token, "ALT", StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsShiftToken(string token)
		{
			return string.Equals(token, "SHIFT", StringComparison.OrdinalIgnoreCase);
		}

		private static bool IsMetaToken(string token)
		{
			return string.Equals(token, "META", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "WIN", StringComparison.OrdinalIgnoreCase) || string.Equals(token, "WINDOWS", StringComparison.OrdinalIgnoreCase);
		}

		private static int ResolveKeyCode(string key)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				return 0;
			}
			string text = key.Trim().ToUpperInvariant();
			if (text.Length == 1)
			{
				return Convert.ToInt32(text[0]);
			}
			if (text.StartsWith("F", StringComparison.Ordinal) && text.Length <= 3 && int.TryParse(text.Substring(1), out var result) && result >= 1 && result <= 12)
			{
				return 111 + result;
			}
			switch (text)
			{
			case "ENTER":
				return 13;
			case "TAB":
				return 9;
			case "ESC":
			case "ESCAPE":
				return 27;
			case "SPACE":
				return 32;
			case "LEFT":
				return 37;
			case "UP":
				return 38;
			case "RIGHT":
				return 39;
			case "DOWN":
				return 40;
			case "DELETE":
			case "DEL":
				return 46;
			case "BACKSPACE":
				return 8;
			case "HOME":
				return 36;
			case "END":
				return 35;
			case "PAGEUP":
				return 33;
			case "PAGEDOWN":
				return 34;
			case "INSERT":
				return 45;
			default:
				return 0;
			}
		}

		private static bool ShouldAppendTypedCharacter(string key, bool ctrl, bool alt, bool meta)
		{
			if (ctrl || alt || meta)
			{
				return false;
			}
			if (string.IsNullOrWhiteSpace(key) || key.Length != 1)
			{
				return false;
			}
			return true;
		}

		private void TriggerKeyboardEvents(int keyCode, bool ctrl, bool alt, bool shift, bool meta)
		{
			FireLegacyKeyboardEvent("onkeydown", keyCode, ctrl, alt, shift, meta);
			FireLegacyKeyboardEvent("onkeypress", keyCode, ctrl, alt, shift, meta);
			FireLegacyKeyboardEvent("onkeyup", keyCode, ctrl, alt, shift, meta);
			DispatchStandardEvent("keydown");
			DispatchStandardEvent("keypress");
			DispatchStandardEvent("keyup");
		}

		private void FireLegacyKeyboardEvent(string eventName, int keyCode, bool ctrl, bool alt, bool shift, bool meta)
		{
			SafeRead(delegate
			{
				object obj = InvokeDynamicMethod(_document, "createEventObject");
				if (obj != null)
				{
					SetDynamicProperty(obj, "keyCode", keyCode);
					SetDynamicProperty(obj, "which", keyCode);
					SetDynamicProperty(obj, "ctrlKey", ctrl);
					SetDynamicProperty(obj, "altKey", alt);
					SetDynamicProperty(obj, "shiftKey", shift);
					SetDynamicProperty(obj, "metaKey", meta);
				}
				return InvokeDynamicMethod(_element, "fireEvent", eventName, obj);
			});
		}

		private int? ResolveOptionIndex(IList<object> options, string text, string value, int? index, string textContains, string textRegex)
		{
			if (options == null || options.Count == 0)
			{
				return null;
			}
			if (index.HasValue)
			{
				if (index.Value >= 0 && index.Value < options.Count)
				{
					return index.Value;
				}
				return null;
			}
			bool flag = !string.IsNullOrWhiteSpace(text);
			bool flag2 = !string.IsNullOrWhiteSpace(value);
			bool flag3 = !string.IsNullOrWhiteSpace(textContains);
			bool flag4 = !string.IsNullOrWhiteSpace(textRegex);
			Regex regex = null;
			if (flag4)
			{
				try
				{
					regex = new Regex(textRegex, RegexOptions.IgnoreCase | RegexOptions.Compiled);
				}
				catch (Exception ex)
				{
					throw new InvalidOperationException("text_re is not a valid regular expression: " + ex.Message, ex);
				}
			}
			for (int i = 0; i < options.Count; i++)
			{
				object instance = options[i];
				string text2 = NormalizeText(SafeToString(ReadDynamicProperty(instance, "text")));
				if (string.IsNullOrWhiteSpace(text2))
				{
					text2 = NormalizeText(SafeToString(ReadDynamicProperty(instance, "innerText")));
				}
				string a = SafeToString(ReadDynamicProperty(instance, "value"));
				if ((!flag || string.Equals(text2, text, StringComparison.Ordinal)) && (!flag2 || string.Equals(a, value, StringComparison.Ordinal)) && (!flag3 || text2.IndexOf(textContains, StringComparison.OrdinalIgnoreCase) >= 0) && (regex == null || regex.IsMatch(text2)))
				{
					return i;
				}
			}
			if (!flag && !flag2 && !flag3 && !flag4)
			{
				return 0;
			}
			return null;
		}

		private void TriggerSelectEvents(bool triggerDblclick)
		{
			FireLegacyEvent("onchange");
			FireLegacyEvent("onclick");
			DispatchStandardEvent("input");
			DispatchStandardEvent("change");
			DispatchStandardEvent("click");
			if (triggerDblclick)
			{
				FireLegacyEvent("ondblclick");
				DispatchStandardEvent("dblclick");
			}
		}

		private void TriggerInputEvents()
		{
			string[] array = new string[3] { "oninput", "onchange", "onblur" };
			for (int i = 0; i < array.Length; i++)
			{
				FireLegacyEvent(array[i]);
			}
			DispatchStandardEvent("input");
			DispatchStandardEvent("change");
		}

		private void FireLegacyEvent(string eventName)
		{
			FireLegacyMouseEvent(eventName, MouseButton.Left);
		}

		private void FireLegacyMouseEvent(string eventName, MouseButton button)
		{
			SafeRead(delegate
			{
				object obj = InvokeDynamicMethod(_document, "createEventObject");
				if (obj != null)
				{
					SetDynamicProperty(obj, "button", ToIeLegacyButton(button));
				}
				return InvokeDynamicMethod(_element, "fireEvent", eventName, obj);
			});
		}

		private void DispatchStandardEvent(string eventName)
		{
			DispatchMouseEvent(eventName, MouseButton.Left);
		}

		private void DispatchMouseEvent(string eventName, MouseButton button)
		{
			SafeRead(delegate
			{
				object evt = InvokeDynamicMethod(_document, "createEvent", "MouseEvents");
				if (evt != null)
				{
					object view = ReadDynamicProperty(_document, "parentWindow") ?? ReadDynamicProperty(_document, "defaultView");
					int w3cButton = ToW3cMouseButton(button);
					SafeRead(() => InvokeDynamicMethod(evt, "initMouseEvent", eventName, true, true, view, 0, 0, 0, 0, 0, false, false, false, false, w3cButton, null));
					return InvokeDynamicMethod(_element, "dispatchEvent", evt);
				}
				object fallback = InvokeDynamicMethod(_document, "createEvent", "HTMLEvents");
				if (fallback != null)
				{
					SafeRead(() => InvokeDynamicMethod(fallback, "initEvent", eventName, true, true));
					return InvokeDynamicMethod(_element, "dispatchEvent", fallback);
				}
				return (object)null;
			});
		}

		private void DispatchMouseClickEvents(MouseButton button)
		{
			FireLegacyMouseEvent("onmousedown", button);
			DispatchMouseEvent("mousedown", button);
			FireLegacyMouseEvent("onmouseup", button);
			DispatchMouseEvent("mouseup", button);
			FireLegacyMouseEvent("onclick", button);
			DispatchMouseEvent("click", button);
			if (button == MouseButton.Right)
			{
				FireLegacyMouseEvent("oncontextmenu", button);
				DispatchMouseEvent("contextmenu", button);
			}
		}

		private static int ToIeLegacyButton(MouseButton button)
		{
			return button switch
			{
				MouseButton.Right => 2, 
				MouseButton.Middle => 4, 
				_ => 1, 
			};
		}

		private static int ToW3cMouseButton(MouseButton button)
		{
			return button switch
			{
				MouseButton.Middle => 1, 
				MouseButton.Right => 2, 
				_ => 0, 
			};
		}
	}

	private static class NativeMethods
	{
		internal delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

		internal struct POINT
		{
			public int X;

			public int Y;
		}

		internal struct RECT
		{
			public int Left;

			public int Top;

			public int Right;

			public int Bottom;
		}

		internal const uint SmtoAbortIfHung = 2u;

		[DllImport("user32.dll")]
		internal static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

		[DllImport("user32.dll")]
		internal static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		internal static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

		[DllImport("user32.dll")]
		internal static extern bool IsWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		internal static extern bool IsWindowVisible(IntPtr hWnd);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		internal static extern uint RegisterWindowMessage(string lpString);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		internal static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

		[DllImport("oleacc.dll")]
		internal static extern int ObjectFromLresult(IntPtr lResult, ref Guid riid, uint wParam, [MarshalAs(UnmanagedType.Interface)] out object ppvObject);

		[DllImport("user32.dll")]
		internal static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("user32.dll")]
		internal static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

		[DllImport("user32.dll")]
		internal static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

		[DllImport("user32.dll")]
		internal static extern IntPtr WindowFromPoint(POINT point);

		[DllImport("user32.dll")]
		internal static extern IntPtr GetParent(IntPtr hWnd);

		[DllImport("user32.dll")]
		internal static extern bool PhysicalToLogicalPointForPerMonitor(IntPtr hWnd, ref POINT lpPoint);
	}

	[CompilerGenerated]
	private sealed class _003C_003Ec__DisplayClass104_0
	{
		public List<IntPtr> children;

		internal bool _003CIterWindowTree_003Eb__0(IntPtr childHwnd, IntPtr _)
		{
			children.Add(childHwnd);
			return true;
		}
	}

	[CompilerGenerated]
	private sealed class _003CEnumerateIndexedCollection_003Ed__79 : IEnumerable<object>, IEnumerable, IEnumerator<object>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private object _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private object collection;

		public object _003C_003E3__collection;

		private int? _003Ccount_003E5__1;

		private IEnumerable _003Cenumerable_003E5__2;

		private int _003Ci_003E5__3;

		private object _003Citem_003E5__4;

		private IEnumerator _003C_003Es__5;

		private object _003Citem_003E5__6;

		object IEnumerator<object>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CEnumerateIndexedCollection_003Ed__79(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			int num = _003C_003E1__state;
			if (num == -3 || num == 2)
			{
				try
				{
				}
				finally
				{
					_003C_003Em__Finally1();
				}
			}
			_003Cenumerable_003E5__2 = null;
			_003Citem_003E5__4 = null;
			_003C_003Es__5 = null;
			_003Citem_003E5__6 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			try
			{
				switch (_003C_003E1__state)
				{
				default:
					return false;
				case 0:
					_003C_003E1__state = -1;
					if (collection == null)
					{
						return false;
					}
					_003Ccount_003E5__1 = SafeInt(ReadDynamicProperty(collection, "length"));
					if (_003Ccount_003E5__1.HasValue)
					{
						_003Ci_003E5__3 = 0;
						goto IL_0110;
					}
					_003Cenumerable_003E5__2 = collection as IEnumerable;
					if (_003Cenumerable_003E5__2 == null)
					{
						return false;
					}
					_003C_003Es__5 = _003Cenumerable_003E5__2.GetEnumerator();
					_003C_003E1__state = -3;
					break;
				case 1:
					_003C_003E1__state = -1;
					goto IL_00f6;
				case 2:
					{
						_003C_003E1__state = -3;
						_003Citem_003E5__6 = null;
						break;
					}
					IL_0110:
					if (_003Ci_003E5__3 < _003Ccount_003E5__1.Value)
					{
						_003Citem_003E5__4 = null;
						try
						{
							_003Citem_003E5__4 = InvokeDynamicMethod(collection, "item", _003Ci_003E5__3);
						}
						catch
						{
							_003Citem_003E5__4 = null;
						}
						if (_003Citem_003E5__4 != null)
						{
							_003C_003E2__current = _003Citem_003E5__4;
							_003C_003E1__state = 1;
							return true;
						}
						goto IL_00f6;
					}
					return false;
					IL_00f6:
					_003Citem_003E5__4 = null;
					_003Ci_003E5__3++;
					goto IL_0110;
				}
				if (_003C_003Es__5.MoveNext())
				{
					_003Citem_003E5__6 = _003C_003Es__5.Current;
					_003C_003E2__current = _003Citem_003E5__6;
					_003C_003E1__state = 2;
					return true;
				}
				_003C_003Em__Finally1();
				_003C_003Es__5 = null;
				return false;
			}
			catch
			{
				//try-fault
				((IDisposable)this).Dispose();
				throw;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		private void _003C_003Em__Finally1()
		{
			_003C_003E1__state = -1;
			if (_003C_003Es__5 is IDisposable disposable)
			{
				disposable.Dispose();
			}
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<object> IEnumerable<object>.GetEnumerator()
		{
			_003CEnumerateIndexedCollection_003Ed__79 _003CEnumerateIndexedCollection_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CEnumerateIndexedCollection_003Ed__ = this;
			}
			else
			{
				_003CEnumerateIndexedCollection_003Ed__ = new _003CEnumerateIndexedCollection_003Ed__79(0);
			}
			_003CEnumerateIndexedCollection_003Ed__.collection = _003C_003E3__collection;
			return _003CEnumerateIndexedCollection_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<object>)this).GetEnumerator();
		}
	}

	[CompilerGenerated]
	private sealed class _003CIterEmbeddedIeDocuments_003Ed__103 : IEnumerable<IntPtr>, IEnumerable, IEnumerator<IntPtr>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private IntPtr _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private IntPtr topHwnd;

		public IntPtr _003C_003E3__topHwnd;

		private IEnumerator<IntPtr> _003C_003Es__1;

		private IntPtr _003CcandidateHwnd_003E5__2;

		private object _003Cdocument_003E5__3;

		IntPtr IEnumerator<IntPtr>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CIterEmbeddedIeDocuments_003Ed__103(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			int num = _003C_003E1__state;
			if (num == -3 || num == 1)
			{
				try
				{
				}
				finally
				{
					_003C_003Em__Finally1();
				}
			}
			_003C_003Es__1 = null;
			_003Cdocument_003E5__3 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			try
			{
				int num = _003C_003E1__state;
				if (num != 0)
				{
					if (num != 1)
					{
						return false;
					}
					_003C_003E1__state = -3;
					goto IL_00ae;
				}
				_003C_003E1__state = -1;
				_003C_003Es__1 = IterWindowTree(topHwnd).GetEnumerator();
				_003C_003E1__state = -3;
				goto IL_00b6;
				IL_00ae:
				_003Cdocument_003E5__3 = null;
				goto IL_00b6;
				IL_00b6:
				do
				{
					if (_003C_003Es__1.MoveNext())
					{
						_003CcandidateHwnd_003E5__2 = _003C_003Es__1.Current;
						continue;
					}
					_003C_003Em__Finally1();
					_003C_003Es__1 = null;
					return false;
				}
				while (!string.Equals(SafeGetClassName(_003CcandidateHwnd_003E5__2), "Internet Explorer_Server", StringComparison.OrdinalIgnoreCase));
				if (TryGetHtmlDocumentFromHwnd(_003CcandidateHwnd_003E5__2, out _003Cdocument_003E5__3))
				{
					_003C_003E2__current = _003CcandidateHwnd_003E5__2;
					_003C_003E1__state = 1;
					return true;
				}
				goto IL_00ae;
			}
			catch
			{
				//try-fault
				((IDisposable)this).Dispose();
				throw;
			}
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		private void _003C_003Em__Finally1()
		{
			_003C_003E1__state = -1;
			if (_003C_003Es__1 != null)
			{
				_003C_003Es__1.Dispose();
			}
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<IntPtr> IEnumerable<IntPtr>.GetEnumerator()
		{
			_003CIterEmbeddedIeDocuments_003Ed__103 _003CIterEmbeddedIeDocuments_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CIterEmbeddedIeDocuments_003Ed__ = this;
			}
			else
			{
				_003CIterEmbeddedIeDocuments_003Ed__ = new _003CIterEmbeddedIeDocuments_003Ed__103(0);
			}
			_003CIterEmbeddedIeDocuments_003Ed__.topHwnd = _003C_003E3__topHwnd;
			return _003CIterEmbeddedIeDocuments_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<IntPtr>)this).GetEnumerator();
		}
	}

	[CompilerGenerated]
	private sealed class _003CIterWindowTree_003Ed__104 : IEnumerable<IntPtr>, IEnumerable, IEnumerator<IntPtr>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private IntPtr _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private IntPtr topHwnd;

		public IntPtr _003C_003E3__topHwnd;

		private Stack<IntPtr> _003Cstack_003E5__1;

		private _003C_003Ec__DisplayClass104_0 _003C_003E8__2;

		private IntPtr _003Chwnd_003E5__3;

		private int _003Ci_003E5__4;

		IntPtr IEnumerator<IntPtr>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CIterWindowTree_003Ed__104(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003Cstack_003E5__1 = null;
			_003C_003E8__2 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			switch (_003C_003E1__state)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				if (topHwnd == IntPtr.Zero || !NativeMethods.IsWindow(topHwnd))
				{
					return false;
				}
				_003Cstack_003E5__1 = new Stack<IntPtr>();
				_003Cstack_003E5__1.Push(topHwnd);
				break;
			case 1:
				_003C_003E1__state = -1;
				_003C_003E8__2.children = new List<IntPtr>();
				NativeMethods.EnumChildWindows(_003Chwnd_003E5__3, delegate(IntPtr childHwnd, IntPtr _)
				{
					_003C_003E8__2.children.Add(childHwnd);
					return true;
				}, IntPtr.Zero);
				_003Ci_003E5__4 = _003C_003E8__2.children.Count - 1;
				while (_003Ci_003E5__4 >= 0)
				{
					_003Cstack_003E5__1.Push(_003C_003E8__2.children[_003Ci_003E5__4]);
					_003Ci_003E5__4--;
				}
				_003C_003E8__2 = null;
				break;
			}
			if (_003Cstack_003E5__1.Count > 0)
			{
				_003C_003E8__2 = new _003C_003Ec__DisplayClass104_0();
				_003Chwnd_003E5__3 = _003Cstack_003E5__1.Pop();
				_003C_003E2__current = _003Chwnd_003E5__3;
				_003C_003E1__state = 1;
				return true;
			}
			return false;
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<IntPtr> IEnumerable<IntPtr>.GetEnumerator()
		{
			_003CIterWindowTree_003Ed__104 _003CIterWindowTree_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CIterWindowTree_003Ed__ = this;
			}
			else
			{
				_003CIterWindowTree_003Ed__ = new _003CIterWindowTree_003Ed__104(0);
			}
			_003CIterWindowTree_003Ed__.topHwnd = _003C_003E3__topHwnd;
			return _003CIterWindowTree_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<IntPtr>)this).GetEnumerator();
		}
	}

	private const string EmbeddedFullName = "embedded-mshtml";

	private const string IeServerClassName = "Internet Explorer_Server";

	private const bool EnableDebugLog = false;

	private static readonly Guid IHtmlDocument2Guid = new Guid("332C4425-26CB-11D0-B483-00C04FD90119");

	private static readonly Guid IDispatchGuid = new Guid("00020400-0000-0000-C000-000000000046");

	private static readonly uint WmHtmlGetObject = NativeMethods.RegisterWindowMessage("WM_HTML_GETOBJECT");

	private EmbeddedIEComWindow _window;

	private int _viewportHostOffsetX;

	private int _viewportHostOffsetY;

	private bool _viewportHostOffsetCalibrated;

	private IntPtr _activeDocHwnd = IntPtr.Zero;

	public EmbeddedIEComWindow raw => _window;

	public long? hwnd => (_window.HWND == IntPtr.Zero) ? ((long?)null) : new long?(_window.HWND.ToInt64());

	public long? doc_hwnd => (_window.DocHWND == IntPtr.Zero) ? ((long?)null) : new long?(_window.DocHWND.ToInt64());

	public string title
	{
		get
		{
			object obj = document_object();
			if (obj != null)
			{
				return SafeToString(ReadDynamicProperty(obj, "title"));
			}
			return SafeToString(_window.TopTitle);
		}
	}

	public string url => SafeToString(_window.LocationURL);

	public string full_name => "embedded-mshtml";

	private IEWindowController(EmbeddedIEComWindow window)
	{
		if (window == null)
		{
			throw new ArgumentNullException("window");
		}
		_window = window;
	}

	public static IEWindowController connect_embedded_ie_window(string title = null, string title_re = null, long? hwnd = null, int timeout = 60000, int interval = 500)
	{
		int num = ToSleepMilliseconds(timeout);
		int millisecondsTimeout = ToSleepMilliseconds(interval, 50);
		DateTime dateTime = DateTime.UtcNow.AddMilliseconds(num);
		while (DateTime.UtcNow <= dateTime)
		{
			foreach (IntPtr item in IterTopLevelWindows(title, title_re, hwnd))
			{
				foreach (IntPtr item2 in IterEmbeddedIeDocuments(item))
				{
					EmbeddedIEComWindow embeddedIEComWindow = CreateEmbeddedWindow(item, item2);
					if (embeddedIEComWindow == null)
					{
						continue;
					}
					IEWindowController iEWindowController = new IEWindowController(embeddedIEComWindow);
					return iEWindowController.wait_ready(Math.Max(0, interval));
				}
			}
			if (DateTime.UtcNow >= dateTime)
			{
				break;
			}
			Thread.Sleep(millisecondsTimeout);
		}
		throw new InvalidOperationException(string.Format("No matching embedded IE window found: title={0}, title_re={1}, hwnd={2}", FormatValue(title), FormatValue(title_re), hwnd.HasValue ? hwnd.Value.ToString() : "null"));
	}

	public static IEWindowController connect_embedded_ie_window_at_screen_point(int screenX, int screenY, int timeout = 5000)
	{
		int num = ToSleepMilliseconds(timeout);
		DateTime dateTime = DateTime.UtcNow.AddMilliseconds(num);
		Exception ex = null;
		while (DateTime.UtcNow <= dateTime)
		{
			foreach (IeDocCandidate item in CollectIeDocCandidatesAtScreenPoint(screenX, screenY))
			{
				try
				{
					EmbeddedIEComWindow embeddedIEComWindow = CreateEmbeddedWindow(item.TopHwnd, item.DocHwnd);
					if (embeddedIEComWindow != null)
					{
						IEWindowController iEWindowController = new IEWindowController(embeddedIEComWindow);
						iEWindowController.wait_ready(Math.Max(500, num / 2));
						if (iEWindowController.document_object() != null)
						{
							return iEWindowController;
						}
					}
				}
				catch (Exception ex2)
				{
					ex = ex2;
				}
			}
			if (DateTime.UtcNow >= dateTime)
			{
				break;
			}
			Thread.Sleep(100);
		}
		string message = $"No IE document/element found at screen point ({screenX}, {screenY})";
		if (ex != null)
		{
			throw new InvalidOperationException(message, ex);
		}
		throw new InvalidOperationException(message);
	}

	public static IEWindowController try_connect_embedded_ie_window_at_screen_point(int screenX, int screenY)
	{
		foreach (IeDocCandidate item in CollectIeDocCandidatesAtScreenPoint(screenX, screenY))
		{
			try
			{
				EmbeddedIEComWindow embeddedIEComWindow = CreateEmbeddedWindow(item.TopHwnd, item.DocHwnd);
				if (embeddedIEComWindow != null)
				{
					IEWindowController iEWindowController = new IEWindowController(embeddedIEComWindow);
					iEWindowController.wait_ready(500);
					if (iEWindowController.document_object() != null)
					{
						return iEWindowController;
					}
				}
			}
			catch
			{
			}
		}
		return null;
	}

	public static bool has_mshtml_at_screen_point(int screenX, int screenY)
	{
		using (IEnumerator<IeDocCandidate> enumerator = CollectIeDocCandidatesAtScreenPoint(screenX, screenY).GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				IeDocCandidate current = enumerator.Current;
				return true;
			}
		}
		return false;
	}

	public IeScreenHitResult try_hit_test_screen_point(int screenX, int screenY)
	{
		return ExecuteOnStaIfNeeded(delegate
		{
			if (_window == null || _window.DocHWND == IntPtr.Zero)
			{
				return (IeScreenHitResult)null;
			}
			if (TryHitTestScreenPointCore(screenX, screenY, out var result))
			{
				return result;
			}
			NativeMethods.POINT pOINT = NormalizeScreenPoint(screenX, screenY);
			if (pOINT.X == screenX && pOINT.Y == screenY)
			{
				return (IeScreenHitResult)null;
			}
			TryHitTestScreenPointCore(pOINT.X, pOINT.Y, out result);
			return result;
		});
	}

	private bool TryHitTestScreenPointCore(int screenX, int screenY, out IeScreenHitResult result)
	{
		result = null;
		IntPtr intPtr = ResolveDocHwndAtScreenPoint(_window.DocHWND, screenX, screenY);
		if (intPtr == IntPtr.Zero)
		{
			intPtr = _window.DocHWND;
		}
		if (!NativeMethods.GetWindowRect(intPtr, out var lpRect) || !RectContainsPoint(lpRect, screenX, screenY))
		{
			return false;
		}
		NativeMethods.POINT pOINT = ScreenToClient(intPtr, screenX, screenY);
		List<object> list = new List<object>();
		if (!TryHitTestWithScreenAlignment(document_object(), intPtr, pOINT.X, pOINT.Y, screenX, screenY, list, out var elementOut, out var elementDocumentOut, out var viewportOffsetX, out var viewportOffsetY))
		{
			return false;
		}
		_activeDocHwnd = intPtr;
		_viewportHostOffsetX = viewportOffsetX;
		_viewportHostOffsetY = viewportOffsetY;
		_viewportHostOffsetCalibrated = true;
		result = new IeScreenHitResult
		{
			Element = new IEDomElement(this, elementOut, elementDocumentOut),
			FramePath = list
		};
		return true;
	}

	public Rectangle? get_element_screen_bounds(IEDomElement element, IEnumerable<object> frame_path = null)
	{
		if (element == null)
		{
			return null;
		}
		return ExecuteOnStaIfNeeded(delegate
		{
			object rawElement = element.raw;
			if (rawElement == null || _window == null || _window.DocHWND == IntPtr.Zero)
			{
				return (Rectangle?)null;
			}
			object obj = SafeRead(() => InvokeDynamicMethod(rawElement, "getBoundingClientRect"));
			double num = 0.0;
			double num2 = 0.0;
			double num3 = 0.0;
			double num4 = 0.0;
			if (obj != null)
			{
				num = SafeDouble(ReadDynamicProperty(obj, "left")).GetValueOrDefault();
				num2 = SafeDouble(ReadDynamicProperty(obj, "top")).GetValueOrDefault();
				num4 = SafeDouble(ReadDynamicProperty(obj, "width")).GetValueOrDefault();
				num3 = SafeDouble(ReadDynamicProperty(obj, "height")).GetValueOrDefault();
			}
			else
			{
				num = SafeDouble(ReadDynamicProperty(rawElement, "offsetLeft")).GetValueOrDefault();
				num2 = SafeDouble(ReadDynamicProperty(rawElement, "offsetTop")).GetValueOrDefault();
				num4 = SafeDouble(ReadDynamicProperty(rawElement, "offsetWidth")).GetValueOrDefault();
				num3 = SafeDouble(ReadDynamicProperty(rawElement, "offsetHeight")).GetValueOrDefault();
			}
			if (num4 <= 0.0 || num3 <= 0.0)
			{
				return (Rectangle?)null;
			}
			List<object> framePath = ((frame_path == null) ? new List<object>() : new List<object>(frame_path));
			double offsetX = 0.0;
			double offsetY = 0.0;
			AccumulateFrameClientOffset(document_object(), framePath, 0, ref offsetX, ref offsetY);
			int num5 = (_viewportHostOffsetCalibrated ? _viewportHostOffsetX : 0);
			int num6 = (_viewportHostOffsetCalibrated ? _viewportHostOffsetY : 0);
			int num7 = (int)Math.Round(num + offsetX + (double)num5);
			int num8 = (int)Math.Round(num2 + offsetY + (double)num6);
			int clientX = num7 + (int)Math.Round(num4);
			int clientY = num8 + (int)Math.Round(num3);
			NativeMethods.POINT pOINT = ClientToScreen(GetActiveDocHwnd(), num7, num8);
			NativeMethods.POINT pOINT2 = ClientToScreen(GetActiveDocHwnd(), clientX, clientY);
			return Rectangle.FromLTRB(pOINT.X, pOINT.Y, pOINT2.X, pOINT2.Y);
		});
	}

	private IntPtr GetActiveDocHwnd()
	{
		return (_activeDocHwnd != IntPtr.Zero) ? _activeDocHwnd : _window.DocHWND;
	}

	public static void diagnose_embedded_ie_window(string title = null, string title_re = null, long? hwnd = null, string frame_name = null, string input_name = "userID", string input_tag = "input")
	{
		Console.WriteLine("========== IE Diagnose Start ==========");
		Console.WriteLine("Filter: title=" + FormatValue(title) + ", title_re=" + FormatValue(title_re) + ", hwnd=" + (hwnd.HasValue ? hwnd.Value.ToString() : "null") + ", frame_name=" + FormatValue(frame_name) + ", input_name=" + FormatValue(input_name) + ", input_tag=" + FormatValue(input_tag));
		List<IntPtr> list = new List<IntPtr>(IterTopLevelWindows(title, title_re, hwnd));
		Console.WriteLine("Matched top windows: " + list.Count);
		Dictionary<string, object> dictionary = new Dictionary<string, object>
		{
			{ "name", input_name },
			{
				"tag",
				string.IsNullOrWhiteSpace(input_tag) ? "input" : input_tag
			}
		};
		for (int i = 0; i < list.Count; i++)
		{
			IntPtr topHwnd = list[i];
			Console.WriteLine("-- Top[" + i + "] HWND=0x" + topHwnd.ToInt64().ToString("X") + " title=" + SafeGetWindowText(topHwnd) + " class=" + SafeGetClassName(topHwnd));
			List<IntPtr> list2 = new List<IntPtr>(IterEmbeddedIeDocuments(topHwnd));
			Console.WriteLine("   IE_Server docs: " + list2.Count);
			for (int j = 0; j < list2.Count; j++)
			{
				IntPtr docHwnd = list2[j];
				EmbeddedIEComWindow embeddedIEComWindow = CreateEmbeddedWindow(topHwnd, docHwnd);
				if (embeddedIEComWindow == null)
				{
					Console.WriteLine("   - Doc[" + j + "] HWND=0x" + docHwnd.ToInt64().ToString("X") + " create window failed");
					continue;
				}
				object obj = ReadDynamicProperty(embeddedIEComWindow, "Document") ?? embeddedIEComWindow.refresh_document();
				if (obj == null)
				{
					Console.WriteLine("   - Doc[" + j + "] HWND=0x" + docHwnd.ToInt64().ToString("X") + " document=null");
					continue;
				}
				object instance = PromoteToTopDocument(obj) ?? obj;
				string text = SafeToString(ReadDynamicProperty(instance, "url"));
				string text2 = SafeToString(ReadDynamicProperty(instance, "readyState"));
				string text3 = SafeToString(ReadDynamicProperty(instance, "title"));
				Console.WriteLine("   - Doc[" + j + "] HWND=0x" + docHwnd.ToInt64().ToString("X") + " ready=" + text2 + " title=" + text3 + " url=" + text);
				List<string> list3 = ListFrameNames(instance);
				Console.WriteLine("     frames(" + list3.Count + ")=" + string.Join(", ", list3));
				int count = new List<object>(LocateElements(instance, dictionary)).Count;
				Console.WriteLine("     topDoc locate(name=" + input_name + ",tag=" + SafeToString(dictionary["tag"]) + ") => " + count);
				if (!string.IsNullOrWhiteSpace(frame_name))
				{
					try
					{
						object obj2 = ResolveFrameDocument(instance, new object[1] { frame_name });
						Console.WriteLine("     frame '" + frame_name + "' resolve=OK, locate => " + new List<object>(LocateElements(obj2, dictionary)).Count);
					}
					catch (Exception ex)
					{
						Console.WriteLine("     frame '" + frame_name + "' resolve=FAIL: " + ex.Message);
					}
				}
			}
		}
		Console.WriteLine("========== IE Diagnose End ==========");
	}

	public IEWindowController wait_ready(int timeout = 60000, int interval = 200)
	{
		return ExecuteOnStaIfNeeded(delegate
		{
			int num = ToSleepMilliseconds(timeout);
			int millisecondsTimeout = ToSleepMilliseconds(interval, 50);
			DateTime dateTime = DateTime.UtcNow.AddMilliseconds(num);
			while (DateTime.UtcNow <= dateTime)
			{
				try
				{
					object obj = document_object();
					if (obj != null)
					{
						string readyState = SafeToString(ReadDynamicProperty(obj, "readyState"));
						if (IsDocumentReady(readyState))
						{
							return this;
						}
					}
				}
				catch
				{
				}
				if (DateTime.UtcNow >= dateTime)
				{
					break;
				}
				Thread.Sleep(millisecondsTimeout);
			}
			return this;
		});
	}

	public object document(IEnumerable<object> frame_path = null)
	{
		return ExecuteOnStaIfNeeded(delegate
		{
			object obj = ReadDynamicProperty(_window, "Document");
			if (obj == null)
			{
				obj = refresh_document();
			}
			if (obj == null)
			{
				throw new InvalidOperationException("Unable to obtain IE document");
			}
			try
			{
				return ResolveFrameDocument(obj, frame_path);
			}
			catch (Exception ex)
			{
				if (!IsRetryableFrameError(ex))
				{
					throw;
				}
				if (frame_path == null || !TrySwitchToDocumentForFramePath(frame_path, out var resolved))
				{
					object obj2 = refresh_document();
					if (obj2 == null)
					{
						throw;
					}
					try
					{
						return ResolveFrameDocument(obj2, frame_path);
					}
					catch (Exception ex2)
					{
						if (frame_path == null || !TrySwitchToDocumentForFramePath(frame_path, out var resolved2))
						{
							throw new InvalidOperationException(ex2.Message, ex2);
						}
						return resolved2;
					}
				}
				return resolved;
			}
		});
	}

	private bool TrySwitchToDocumentForFramePath(IEnumerable<object> framePath, out object resolved)
	{
		resolved = null;
		IntPtr hWND = _window.HWND;
		if (hWND == IntPtr.Zero)
		{
			return false;
		}
		foreach (IntPtr item in IterEmbeddedIeDocuments(hWND))
		{
			EmbeddedIEComWindow embeddedIEComWindow = CreateEmbeddedWindow(hWND, item);
			if (embeddedIEComWindow == null)
			{
				continue;
			}
			object obj = ReadDynamicProperty(embeddedIEComWindow, "Document") ?? embeddedIEComWindow.refresh_document();
			if (obj != null)
			{
				try
				{
					object obj2 = ResolveFrameDocument(obj, framePath);
					_window = embeddedIEComWindow;
					resolved = obj2;
					return true;
				}
				catch
				{
				}
			}
		}
		return false;
	}

	private bool TrySwitchToDocumentForLocator(IDictionary<string, object> locator, out object resolved)
	{
		resolved = null;
		IntPtr hWND = _window.HWND;
		if (hWND == IntPtr.Zero)
		{
			return false;
		}
		foreach (IntPtr item in IterEmbeddedIeDocuments(hWND))
		{
			if (item == _window.DocHWND)
			{
				continue;
			}
			EmbeddedIEComWindow embeddedIEComWindow = CreateEmbeddedWindow(hWND, item);
			if (embeddedIEComWindow == null)
			{
				continue;
			}
			object obj = ReadDynamicProperty(embeddedIEComWindow, "Document") ?? embeddedIEComWindow.refresh_document();
			if (obj != null)
			{
				object obj2 = PromoteToTopDocument(obj) ?? obj;
				List<object> list = new List<object>(LocateElements(obj, locator));
				if (list.Count == 0)
				{
					list = new List<object>(LocateElements(obj2, locator));
				}
				if (list.Count != 0)
				{
					_window = embeddedIEComWindow;
					resolved = obj2;
					return true;
				}
			}
		}
		return false;
	}

	private bool TryRebindCurrentDocument(out object resolved)
	{
		resolved = null;
		IntPtr hWND = _window.HWND;
		IntPtr docHWND = _window.DocHWND;
		if (hWND == IntPtr.Zero || docHWND == IntPtr.Zero)
		{
			return false;
		}
		EmbeddedIEComWindow embeddedIEComWindow = CreateEmbeddedWindow(hWND, docHWND);
		if (embeddedIEComWindow == null)
		{
			return false;
		}
		object obj = ReadDynamicProperty(embeddedIEComWindow, "Document") ?? embeddedIEComWindow.refresh_document();
		if (obj == null)
		{
			return false;
		}
		_window = embeddedIEComWindow;
		resolved = PromoteToTopDocument(obj) ?? obj;
		return true;
	}

	private static bool IsFramePathEmpty(IEnumerable<object> framePath)
	{
		if (framePath == null)
		{
			return true;
		}
		using (IEnumerator<object> enumerator = framePath.GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				object current = enumerator.Current;
				return false;
			}
		}
		return true;
	}

	public object refresh_document()
	{
		return ExecuteOnStaIfNeeded(delegate
		{
			Delegate refreshMethod = ReadDynamicProperty(_window, "refresh_document") as Delegate;
			return ((object)refreshMethod != null) ? SafeRead(() => refreshMethod.DynamicInvoke()) : _window.refresh_document();
		});
	}

	public IEWindowController refresh(int timeout = 60000, int interval = 200)
	{
		refresh_document();
		return wait_ready(timeout, interval);
	}

	public bool has_frame(IEnumerable<object> frame_path)
	{
		return ExecuteOnStaIfNeeded(delegate
		{
			try
			{
				document(frame_path);
				return true;
			}
			catch
			{
				return false;
			}
		});
	}

	public IEWindowController wait_for_frame(IEnumerable<object> frame_path, int timeout = 60000, int interval = 200)
	{
		return ExecuteOnStaIfNeeded(delegate
		{
			DateTime dateTime = DateTime.UtcNow.AddMilliseconds(ToSleepMilliseconds(timeout));
			List<object> list = ((frame_path == null) ? new List<object>() : new List<object>(frame_path));
			int millisecondsTimeout = ToSleepMilliseconds(interval, 50);
			while (DateTime.UtcNow <= dateTime)
			{
				refresh_document();
				try
				{
					document(list);
					return this;
				}
				catch (Exception ex)
				{
					if (!IsRetryableFrameError(ex))
					{
						throw;
					}
				}
				if (DateTime.UtcNow >= dateTime)
				{
					break;
				}
				Thread.Sleep(millisecondsTimeout);
			}
			throw new InvalidOperationException($"Timed out waiting for frame: frame_path={DescribeFramePath(list)}");
		});
	}

	public IEDomElement[] find_elements(IDictionary<string, object> locator, IEnumerable<object> frame_path = null)
	{
		return ExecuteOnStaIfNeeded(delegate
		{
			if (locator == null)
			{
				throw new ArgumentNullException("locator");
			}
			object obj = document(frame_path);
			List<object> list = new List<object>(LocateElements(obj, locator));
			if (list.Count == 0 && IsFramePathEmpty(frame_path) && TryRebindCurrentDocument(out var resolved))
			{
				obj = resolved;
				list = new List<object>(LocateElements(obj, locator));
				DebugLog("find_elements", "rebuilt current COM binding locator={0}, matches={1}", DescribeLocator(locator), list.Count);
			}
			if (list.Count == 0 && IsFramePathEmpty(frame_path))
			{
				object obj2 = PromoteToTopDocument(obj) ?? obj;
				if (obj2 != obj)
				{
					List<object> list2 = new List<object>(LocateElements(obj2, locator));
					if (list2.Count > 0)
					{
						obj = obj2;
						list = list2;
						DebugLog("find_elements", "matched on top document fallback locator={0}, matches={1}", DescribeLocator(locator), list.Count);
					}
				}
			}
			if (list.Count == 0 && IsFramePathEmpty(frame_path) && TrySwitchToDocumentForLocator(locator, out var resolved2))
			{
				obj = resolved2;
				list = new List<object>(LocateElements(obj, locator));
				DebugLog("find_elements", "switched embedded document by locator={0}, new_doc_hwnd=0x{1}", DescribeLocator(locator), _window.DocHWND.ToInt64().ToString("X"));
			}
			List<IEDomElement> list3 = new List<IEDomElement>();
			foreach (object item in list)
			{
				list3.Add(new IEDomElement(this, item, obj));
			}
			return list3.ToArray();
		});
	}

	public IEDomElement find_element(IDictionary<string, object> locator, IEnumerable<object> frame_path = null, int timeout = 15000, int interval = 200)
	{
		return ExecuteOnStaIfNeeded(delegate
		{
			if (locator == null)
			{
				throw new ArgumentNullException("locator");
			}
			DateTime dateTime = DateTime.UtcNow.AddMilliseconds(ToSleepMilliseconds(timeout));
			Exception ex = null;
			while (true)
			{
				try
				{
					IEDomElement[] array = find_elements(locator, frame_path);
					DebugLog("find_element", "locator={0}, frame_path={1}, matches={2}", DescribeLocator(locator), DescribeFramePath(frame_path), (array != null) ? array.Length : 0);
					if (array.Length != 0)
					{
						IEDomElement iEDomElement = PickPreferredElement(array);
						if (iEDomElement != null)
						{
							DebugLog("find_element", "selected(preferred): {0}", DescribeDomElement(iEDomElement));
							return iEDomElement;
						}
						DebugLog("find_element", "selected(first): {0}", DescribeDomElement(array[0]));
						return array[0];
					}
				}
				catch (Exception ex2)
				{
					if (!IsRetryableFrameError(ex2))
					{
						throw;
					}
					ex = ex2;
					if (DateTime.UtcNow >= dateTime)
					{
						break;
					}
					refresh_document();
					Thread.Sleep(ToSleepMilliseconds(interval, 50));
					continue;
				}
				if (DateTime.UtcNow >= dateTime)
				{
					break;
				}
				Thread.Sleep(ToSleepMilliseconds(interval, 50));
			}
			if (ex != null)
			{
				string text = BuildNotFoundDiagnostics(locator, frame_path);
				throw new InvalidOperationException($"Timed out waiting for frame before locating element: locator={DescribeLocator(locator)}, frame_path={DescribeFramePath(frame_path)}, error={ex.Message}, diagnose={text}", ex);
			}
			string arg = BuildNotFoundDiagnostics(locator, frame_path);
			throw new InvalidOperationException($"Element not found: locator={DescribeLocator(locator)}, frame_path={DescribeFramePath(frame_path)}, diagnose={arg}");
		});
	}

	private static IEDomElement PickPreferredElement(IEnumerable<IEDomElement> elements)
	{
		if (elements == null)
		{
			return null;
		}
		IEDomElement iEDomElement = null;
		foreach (IEDomElement element in elements)
		{
			if (element != null)
			{
				if (iEDomElement == null)
				{
					iEDomElement = element;
				}
				if (IsElementInteractable(element.raw))
				{
					return element;
				}
			}
		}
		return iEDomElement;
	}

	private static bool IsElementInteractable(object element)
	{
		if (element == null)
		{
			return false;
		}
		if (SafeRead(() => ReadDynamicProperty(element, "disabled")) is int num && num != 0)
		{
			return false;
		}
		string a = SafeToString(SafeRead(() => ReadDynamicProperty(element, "type")));
		if (string.Equals(a, "hidden", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		string a2 = SafeToString(SafeRead(() => ReadDynamicProperty(element, "tagName")));
		if (string.Equals(a2, "input", StringComparison.OrdinalIgnoreCase) && string.Equals(a, "hidden", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		int? num2 = SafeInt(SafeRead(() => ReadDynamicProperty(element, "offsetWidth")));
		int? num3 = SafeInt(SafeRead(() => ReadDynamicProperty(element, "offsetHeight")));
		if (num2.HasValue && num3.HasValue && num2.Value <= 0 && num3.Value <= 0)
		{
			return false;
		}
		object style = SafeRead(() => ReadDynamicProperty(element, "style"));
		string a3 = SafeToString(SafeRead(() => ReadDynamicProperty(style, "display")));
		if (string.Equals(a3, "none", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		string a4 = SafeToString(SafeRead(() => ReadDynamicProperty(style, "visibility")));
		if (string.Equals(a4, "hidden", StringComparison.OrdinalIgnoreCase) || string.Equals(a4, "collapse", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		return true;
	}

	public void input_text(IDictionary<string, object> locator, object value, IEnumerable<object> frame_path = null, int timeout = 15000, bool trigger_events = true, int delay_before = 0)
	{
		ExecuteOnStaIfNeeded(delegate
		{
			DebugLog("input_text", "begin locator={0}, frame_path={1}, value={2}", DescribeLocator(locator), DescribeFramePath(frame_path), FormatAny(value));
			IEDomElement iEDomElement = find_element(locator, frame_path, timeout);
			iEDomElement.set_value(value, trigger_events, delay_before);
			DebugLog("input_text", "completed element={0}", DescribeDomElement(iEDomElement));
			return true;
		});
	}

	public void click_element_native(IDictionary<string, object> locator, IEnumerable<object> frame_path = null, int timeout = 15000, int delay_before = 0, MouseButton button = MouseButton.Left)
	{
		ExecuteOnStaIfNeeded(delegate
		{
			IEDomElement iEDomElement = find_element(locator, frame_path, timeout);
			iEDomElement.native_click(delay_before, button);
			return true;
		});
	}

	public void select_option(IDictionary<string, object> locator, IEnumerable<object> frame_path = null, int timeout = 15000, string text = null, string value = null, int? index = null, string text_contains = null, string text_re = null, bool trigger_events = true, bool trigger_dblclick = false, int delay_before = 0)
	{
		ExecuteOnStaIfNeeded(delegate
		{
			IEDomElement iEDomElement = find_element(locator, frame_path, timeout);
			iEDomElement.select_option(text, value, index, text_contains, text_re, trigger_events, trigger_dblclick, delay_before);
			return true;
		});
	}

	public void check_element(IDictionary<string, object> locator, IEnumerable<object> frame_path = null, int timeout = 15000, bool trigger_events = true, int delay_before = 0)
	{
		ExecuteOnStaIfNeeded(delegate
		{
			IEDomElement iEDomElement = find_element(locator, frame_path, timeout);
			iEDomElement.check(trigger_events, delay_before);
			return true;
		});
	}

	public void uncheck_element(IDictionary<string, object> locator, IEnumerable<object> frame_path = null, int timeout = 15000, bool trigger_events = true, int delay_before = 0)
	{
		ExecuteOnStaIfNeeded(delegate
		{
			IEDomElement iEDomElement = find_element(locator, frame_path, timeout);
			iEDomElement.uncheck(trigger_events, delay_before);
			return true;
		});
	}

	public string get_element_text(IDictionary<string, object> locator, IEnumerable<object> frame_path = null, int timeout = 15000)
	{
		return ExecuteOnStaIfNeeded(delegate
		{
			IEDomElement iEDomElement = find_element(locator, frame_path, timeout);
			return iEDomElement.get_text();
		});
	}

	public object get_element_attribute(IDictionary<string, object> locator, string name, IEnumerable<object> frame_path = null, int timeout = 15000, object default_value = null)
	{
		return ExecuteOnStaIfNeeded(delegate
		{
			IEDomElement iEDomElement = find_element(locator, frame_path, timeout);
			return iEDomElement.get_attribute(name, default_value);
		});
	}

	public void set_attribute(IDictionary<string, object> locator, string name, object value, IEnumerable<object> frame_path = null, int timeout = 15000, bool trigger_events = true, int delay_before = 0)
	{
		ExecuteOnStaIfNeeded(delegate
		{
			IEDomElement iEDomElement = find_element(locator, frame_path, timeout);
			iEDomElement.set_attribute(name, value, trigger_events, delay_before);
			return true;
		});
	}

	public object run_js(string script, IEnumerable<object> frame_path = null)
	{
		return run_js(script, frame_path, null);
	}

	public object run_js(string script, IEnumerable<object> frame_path = null, IList<object> args = null)
	{
		return ExecuteOnStaIfNeeded(delegate
		{
			if (string.IsNullOrWhiteSpace(script))
			{
				return (object)null;
			}
			if (frame_path == null)
			{
				frame_path = new List<object>();
			}
			object instance = document(frame_path);
			object window = ReadDynamicProperty(instance, "parentWindow");
			if (window == null)
			{
				throw new InvalidOperationException("Unable to obtain document.parentWindow; cannot execute JavaScript");
			}
			if (args != null)
			{
				string text = ToJavaScriptLiteral(args);
				string text2 = "(function(){var __fn=(" + script + ");if(typeof __fn!=='function'){throw new Error('run_js: script must evaluate to a function when args is provided');}return __fn.apply(window," + text + ");})()";
				try
				{
					return InvokeDynamicMethod(window, "eval", text2);
				}
				catch (Exception ex)
				{
					throw new InvalidOperationException("run_js(function+args) execution failed: " + ex.Message, ex);
				}
			}
			object obj = SafeRead(() => InvokeDynamicMethod(window, "eval", script));
			return (obj != null) ? obj : SafeRead(() => InvokeDynamicMethod(window, "execScript", script, "javascript"));
		});
	}

	public IEDomElement[] get_elements(IDictionary<string, object> locator, IEnumerable<object> frame_path = null)
	{
		return find_elements(locator, frame_path);
	}

	public void press_keys(IDictionary<string, object> locator, object keys, IEnumerable<object> frame_path = null, int timeout = 15000, int delay_before = 0)
	{
		ExecuteOnStaIfNeeded(delegate
		{
			IEDomElement iEDomElement = find_element(locator, frame_path, timeout);
			iEDomElement.press_keys(keys, delay_before);
			return true;
		});
	}

	public void double_click_element(IDictionary<string, object> locator, IEnumerable<object> frame_path = null, int timeout = 15000, int delay_before = 0, MouseButton button = MouseButton.Left)
	{
		ExecuteOnStaIfNeeded(delegate
		{
			IEDomElement iEDomElement = find_element(locator, frame_path, timeout);
			iEDomElement.double_click(delay_before, button);
			return true;
		});
	}

	public IEDomElement get_parent(IDictionary<string, object> locator, IEnumerable<object> frame_path = null, int timeout = 15000)
	{
		IEDomElement iEDomElement = find_element(locator, frame_path, timeout);
		return iEDomElement.get_parent();
	}

	public IEDomElement[] get_children(IDictionary<string, object> locator, IEnumerable<object> frame_path = null, int timeout = 15000)
	{
		IEDomElement iEDomElement = find_element(locator, frame_path, timeout);
		return iEDomElement.get_children();
	}

	public bool is_checked(IDictionary<string, object> locator, IEnumerable<object> frame_path = null, int timeout = 0)
	{
		return ExecuteOnStaIfNeeded(delegate
		{
			IEDomElement iEDomElement = find_element(locator, frame_path, timeout);
			return iEDomElement.is_checked();
		});
	}

	public string[] get_selected_options(IDictionary<string, object> locator, IEnumerable<object> frame_path = null, int timeout = 15000)
	{
		return ExecuteOnStaIfNeeded(delegate
		{
			IEDomElement iEDomElement = find_element(locator, frame_path, timeout);
			return iEDomElement.get_selected_options();
		});
	}

	public object document_object()
	{
		return ExecuteOnStaIfNeeded(() => _window.refresh_document());
	}

	private static T ExecuteOnStaIfNeeded<T>(Func<T> operation)
	{
		if (operation == null)
		{
			throw new ArgumentNullException("operation");
		}
		if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
		{
			return operation();
		}
		T result = default(T);
		Exception error = null;
		ManualResetEvent done = new ManualResetEvent(initialState: false);
		try
		{
			Thread thread = new Thread((ThreadStart)delegate
			{
				try
				{
					result = operation();
				}
				catch (Exception ex)
				{
					error = ex;
				}
				finally
				{
					done.Set();
				}
			});
			thread.IsBackground = true;
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
			done.WaitOne();
		}
		finally
		{
			if (done != null)
			{
				((IDisposable)done).Dispose();
			}
		}
		if (error != null)
		{
			ExceptionDispatchInfo.Capture(error).Throw();
		}
		return result;
	}

	private static object ResolveFrameDocument(object document, IEnumerable<object> framePath)
	{
		List<object> list = ((framePath == null) ? new List<object>() : new List<object>(framePath));
		object obj = PromoteToTopDocument(document);
		if (list.Count == 0)
		{
			return obj ?? document;
		}
		foreach (object frameRef in list)
		{
			object frames = GetFrameCollection(obj);
			if (frames == null)
			{
				throw new InvalidOperationException("Current document does not contain frames: frame_ref=" + FormatAny(frameRef));
			}
			object obj2 = null;
			obj2 = TryGetFrameWindowByTypedInterop(frames, frameRef);
			if (obj2 == null)
			{
				obj2 = SafeRead(() => InvokeDynamicMethod(frames, "item", frameRef));
			}
			if (obj2 == null)
			{
				int frameCount = GetFrameCount(frames);
				int index;
				for (index = 0; index < frameCount; index++)
				{
					object obj3 = TryGetFrameWindowByTypedInterop(frames, index) ?? SafeRead(() => InvokeDynamicMethod(frames, "item", index));
					if (obj3 == null)
					{
						continue;
					}
					string text = GetFrameWindowName(obj3);
					if (string.IsNullOrWhiteSpace(text))
					{
						object instance = ReadDynamicProperty(obj3, "document");
						text = SafeToString(ReadDynamicProperty(instance, "name"));
						if (string.IsNullOrWhiteSpace(text))
						{
							object instance2 = ReadDynamicProperty(instance, "frameElement");
							text = SafeToString(ReadDynamicProperty(instance2, "name"));
							if (string.IsNullOrWhiteSpace(text))
							{
								text = SafeToString(ReadDynamicProperty(instance2, "id"));
							}
						}
					}
					if ((frameRef is int && (int)frameRef == index) || string.Equals(SafeToString(frameRef), text, StringComparison.Ordinal) || string.Equals(SafeToString(frameRef), text, StringComparison.OrdinalIgnoreCase))
					{
						obj2 = obj3;
						break;
					}
				}
			}
			if (obj2 == null)
			{
				throw new InvalidOperationException("Frame not found: " + FormatAny(frameRef));
			}
			obj = GetFrameWindowDocument(obj2) ?? ReadDynamicProperty(obj2, "document");
			if (obj == null)
			{
				throw new InvalidOperationException("Frame has no accessible document: " + FormatAny(frameRef));
			}
		}
		return obj;
	}

	private static object PromoteToTopDocument(object document)
	{
		if (document == null)
		{
			return null;
		}
		try
		{
			object instance = ReadDynamicProperty(document, "parentWindow");
			object instance2 = ReadDynamicProperty(instance, "top");
			object obj = ReadDynamicProperty(instance2, "document");
			if (obj != null)
			{
				return obj;
			}
		}
		catch
		{
		}
		return document;
	}

	private static object GetFrameCollection(object document)
	{
		if (document == null)
		{
			return null;
		}
		object obj = ReadDynamicProperty(document, "frames");
		if (obj != null)
		{
			return obj;
		}
		object instance = ReadDynamicProperty(document, "parentWindow");
		obj = ReadDynamicProperty(instance, "frames");
		if (obj != null)
		{
			return obj;
		}
		object instance2 = ReadDynamicProperty(document, "Script");
		return ReadDynamicProperty(instance2, "frames");
	}

	private static object TryGetFrameWindowByTypedInterop(object frames, object frameRef)
	{
		return SafeRead(() => InvokeDynamicMethod(frames, "item", frameRef));
	}

	private static int GetFrameCount(object frames)
	{
		return SafeInt(ReadDynamicProperty(frames, "length")).GetValueOrDefault();
	}

	private static string GetFrameWindowName(object frameWindow)
	{
		return SafeToString(ReadDynamicProperty(frameWindow, "name"));
	}

	private static object GetFrameWindowDocument(object frameWindow)
	{
		return ReadDynamicProperty(frameWindow, "document");
	}

	private static List<string> ListFrameNames(object document)
	{
		List<string> list = new List<string>();
		object frames = GetFrameCollection(document);
		if (frames == null)
		{
			return list;
		}
		int frameCount = GetFrameCount(frames);
		int i;
		for (i = 0; i < frameCount; i++)
		{
			object obj = TryGetFrameWindowByTypedInterop(frames, i) ?? SafeRead(() => InvokeDynamicMethod(frames, "item", i));
			if (obj != null)
			{
				string text = GetFrameWindowName(obj);
				if (string.IsNullOrWhiteSpace(text))
				{
					text = "(index=" + i + ")";
				}
				list.Add(text);
			}
		}
		return list;
	}

	private static IEnumerable<object> LocateElements(object document, IDictionary<string, object> locator)
	{
		string selector = GetLocatorString(locator, "selector");
		IEnumerable<object> enumerable;
		if (!string.IsNullOrWhiteSpace(selector))
		{
			object collection = SafeRead(() => InvokeDynamicMethod(document, "querySelectorAll", selector));
			enumerable = EnumerateIndexedCollection(collection);
		}
		else
		{
			object collection2 = ReadDynamicProperty(document, "all") ?? SafeRead(() => InvokeDynamicMethod(document, "getElementsByTagName", "*"));
			enumerable = EnumerateIndexedCollection(collection2);
		}
		List<object> list = new List<object>();
		foreach (object item in enumerable)
		{
			if (item != null && ElementMatches(item, locator))
			{
				list.Add(item);
			}
		}
		int? num = SafeInt(GetLocatorValue(locator, "index"));
		if (num.HasValue)
		{
			if (num.Value >= 0 && num.Value < list.Count)
			{
				return new object[1] { list[num.Value] };
			}
			return new object[0];
		}
		return list;
	}

	private static bool ElementMatches(object element, IDictionary<string, object> locator)
	{
		if (locator == null)
		{
			return false;
		}
		if (!MatchesString(SafeToString(ReadDynamicProperty(element, "id")), GetLocatorString(locator, "id"), ignoreCase: false))
		{
			return false;
		}
		if (!MatchesString(SafeToString(ReadDynamicProperty(element, "name")), GetLocatorString(locator, "name"), ignoreCase: false))
		{
			return false;
		}
		if (!MatchesString(SafeToString(ReadDynamicProperty(element, "tagName")), GetLocatorString(locator, "tag"), ignoreCase: true))
		{
			return false;
		}
		if (!MatchesString(SafeToString(ReadDynamicProperty(element, "type")), GetLocatorString(locator, "type"), ignoreCase: true))
		{
			return false;
		}
		string value = GetLocatorString(locator, "class_name") ?? GetLocatorString(locator, "class");
		if (!string.IsNullOrWhiteSpace(value))
		{
			string text = SafeToString(ReadDynamicProperty(element, "className"));
			if (text.IndexOf(value, StringComparison.OrdinalIgnoreCase) < 0)
			{
				return false;
			}
		}
		string text2 = NormalizeText(SafeToString(ReadDynamicProperty(element, "innerText")));
		if (!MatchesString(text2, GetLocatorString(locator, "text"), ignoreCase: false))
		{
			return false;
		}
		string locatorString = GetLocatorString(locator, "text_contains");
		if (!string.IsNullOrWhiteSpace(locatorString) && text2.IndexOf(locatorString, StringComparison.OrdinalIgnoreCase) < 0)
		{
			return false;
		}
		string locatorString2 = GetLocatorString(locator, "text_re");
		if (!string.IsNullOrWhiteSpace(locatorString2) && !Regex.IsMatch(text2, locatorString2, RegexOptions.IgnoreCase))
		{
			return false;
		}
		if (!MatchesValue(element, GetLocatorString(locator, "value")))
		{
			return false;
		}
		IDictionary<string, object> locatorDictionary = GetLocatorDictionary(locator, "attrs");
		if (locatorDictionary != null)
		{
			foreach (KeyValuePair<string, object> pair in locatorDictionary)
			{
				string a = SafeToString(SafeRead(() => InvokeDynamicMethod(element, "getAttribute", pair.Key)));
				if (!string.Equals(a, SafeToString(pair.Value), StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
			}
		}
		if (!MatchesGenericLocatorAttributes(element, locator))
		{
			return false;
		}
		return true;
	}

	private static bool MatchesGenericLocatorAttributes(object element, IDictionary<string, object> locator)
	{
		if (element == null || locator == null)
		{
			return false;
		}
		foreach (KeyValuePair<string, object> pair in locator)
		{
			if (pair.Key == null || IsReservedLocatorKey(pair.Key))
			{
				continue;
			}
			string text = SafeToString(pair.Value);
			if (string.IsNullOrWhiteSpace(text))
			{
				continue;
			}
			string text2 = SafeToString(SafeRead(() => InvokeDynamicMethod(element, "getAttribute", pair.Key)));
			if (string.IsNullOrWhiteSpace(text2))
			{
				text2 = SafeToString(SafeRead(() => ReadDynamicProperty(element, pair.Key)));
			}
			if (string.Equals(text2, text, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			return false;
		}
		return true;
	}

	private static bool IsReservedLocatorKey(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return true;
		}
		return string.Equals(key, "selector", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "index", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "id", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "name", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "tag", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "type", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "class", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "class_name", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "text", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "text_contains", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "text_re", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "value", StringComparison.OrdinalIgnoreCase) || string.Equals(key, "attrs", StringComparison.OrdinalIgnoreCase);
	}

	private static bool MatchesValue(object element, string expected)
	{
		if (string.IsNullOrWhiteSpace(expected))
		{
			return true;
		}
		string text = NormalizeText(expected);
		if (string.IsNullOrWhiteSpace(text))
		{
			return true;
		}
		string a = NormalizeText(SafeToString(ReadDynamicProperty(element, "value")));
		if (string.Equals(a, text, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		string a2 = NormalizeText(SafeToString(SafeRead(() => InvokeDynamicMethod(element, "getAttribute", "value"))));
		if (string.Equals(a2, text, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		string a3 = NormalizeText(SafeToString(ReadDynamicProperty(element, "innerText")));
		if (string.Equals(a3, text, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		string a4 = NormalizeText(SafeToString(ReadDynamicProperty(element, "textContent")));
		return string.Equals(a4, text, StringComparison.OrdinalIgnoreCase);
	}

	[IteratorStateMachine(typeof(_003CEnumerateIndexedCollection_003Ed__79))]
	private static IEnumerable<object> EnumerateIndexedCollection(object collection)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CEnumerateIndexedCollection_003Ed__79(-2)
		{
			_003C_003E3__collection = collection
		};
	}

	private static object InvokeDynamicMethod(object instance, string methodName, params object[] args)
	{
		if (instance == null || string.IsNullOrWhiteSpace(methodName))
		{
			return null;
		}
		return instance.GetType().InvokeMember(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod, null, instance, args);
	}

	private static void SetDynamicProperty(object instance, string propertyName, object value)
	{
		if (instance == null || string.IsNullOrWhiteSpace(propertyName))
		{
			return;
		}
		try
		{
			instance.GetType().InvokeMember(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetProperty, null, instance, new object[1] { value });
		}
		catch
		{
			try
			{
				instance.GetType().InvokeMember(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.SetField, null, instance, new object[1] { value });
			}
			catch
			{
			}
		}
	}

	private static IEnumerable<IeDocCandidate> CollectIeDocCandidatesAtScreenPoint(int screenX, int screenY)
	{
		List<IeDocCandidate> candidates = new List<IeDocCandidate>();
		NativeMethods.EnumWindows(delegate(IntPtr topHwnd, IntPtr _)
		{
			if (!NativeMethods.IsWindowVisible(topHwnd))
			{
				return true;
			}
			if (!NativeMethods.GetWindowRect(topHwnd, out var lpRect) || !RectContainsPoint(lpRect, screenX, screenY))
			{
				return true;
			}
			foreach (IntPtr item in IterEmbeddedIeDocuments(topHwnd))
			{
				if (NativeMethods.GetWindowRect(item, out var lpRect2) && RectContainsPoint(lpRect2, screenX, screenY))
				{
					candidates.Add(new IeDocCandidate
					{
						TopHwnd = topHwnd,
						DocHwnd = item,
						Area = Math.Max(1L, (long)(lpRect2.Right - lpRect2.Left) * (long)(lpRect2.Bottom - lpRect2.Top))
					});
				}
			}
			return true;
		}, IntPtr.Zero);
		candidates.Sort((IeDocCandidate left, IeDocCandidate right) => left.Area.CompareTo(right.Area));
		return candidates;
	}

	private static bool TryHitTestDocument(object document, int clientX, int clientY, IList<object> framePathOut, out object elementOut, out object elementDocumentOut)
	{
		elementOut = null;
		elementDocumentOut = null;
		if (document == null)
		{
			return false;
		}
		object element = SafeRead(() => InvokeDynamicMethod(document, "elementFromPoint", clientX, clientY));
		if (element == null)
		{
			return false;
		}
		string tagName = SafeToString(ReadDynamicProperty(element, "tagName"));
		if (IsFrameTag(tagName))
		{
			object instance = SafeRead(() => InvokeDynamicMethod(element, "getBoundingClientRect"));
			double valueOrDefault = SafeDouble(ReadDynamicProperty(instance, "left")).GetValueOrDefault();
			double valueOrDefault2 = SafeDouble(ReadDynamicProperty(instance, "top")).GetValueOrDefault();
			object obj = ReadDynamicProperty(element, "contentDocument") ?? ReadDynamicProperty(element, "document");
			if (obj == null)
			{
				return false;
			}
			framePathOut.Add(ResolveFrameReference(element, obj));
			return TryHitTestDocument(obj, clientX - (int)Math.Round(valueOrDefault), clientY - (int)Math.Round(valueOrDefault2), framePathOut, out elementOut, out elementDocumentOut);
		}
		elementOut = element;
		elementDocumentOut = document;
		return true;
	}

	private bool TryHitTestWithScreenAlignment(object topDocument, IntPtr docHwnd, int hostClientX, int hostClientY, int screenX, int screenY, IList<object> framePathOut, out object elementOut, out object elementDocumentOut, out int viewportOffsetX, out int viewportOffsetY)
	{
		elementOut = null;
		elementDocumentOut = null;
		viewportOffsetX = (_viewportHostOffsetCalibrated ? _viewportHostOffsetX : 0);
		viewportOffsetY = (_viewportHostOffsetCalibrated ? _viewportHostOffsetY : 0);
		List<ViewportOffsetCandidate> list = new List<ViewportOffsetCandidate>();
		if (_viewportHostOffsetCalibrated)
		{
			list.Add(new ViewportOffsetCandidate
			{
				X = viewportOffsetX,
				Y = viewportOffsetY
			});
		}
		if (TryMeasureViewportOffsetFromDocument(topDocument, out var offsetX, out var offsetY) && !ContainsOffset(list, offsetX, offsetY))
		{
			list.Add(new ViewportOffsetCandidate
			{
				X = offsetX,
				Y = offsetY
			});
		}
		if (!ContainsOffset(list, 0, 0))
		{
			list.Add(new ViewportOffsetCandidate
			{
				X = 0,
				Y = 0
			});
		}
		foreach (ViewportOffsetCandidate item in list)
		{
			if (TryHitTestAtViewportOffset(topDocument, docHwnd, hostClientX, hostClientY, screenX, screenY, item.X, item.Y, framePathOut, out elementOut, out elementDocumentOut))
			{
				viewportOffsetX = item.X;
				viewportOffsetY = item.Y;
				return true;
			}
		}
		return false;
	}

	private static bool ContainsOffset(IList<ViewportOffsetCandidate> offsets, int x, int y)
	{
		for (int i = 0; i < offsets.Count; i++)
		{
			if (offsets[i].X == x && offsets[i].Y == y)
			{
				return true;
			}
		}
		return false;
	}

	private bool TryMeasureViewportOffsetFromDocument(object topDocument, out int offsetX, out int offsetY)
	{
		offsetX = 0;
		offsetY = 0;
		try
		{
			object body = ReadDynamicProperty(topDocument, "body");
			if (body == null)
			{
				return false;
			}
			object obj = SafeRead(() => InvokeDynamicMethod(body, "createTextRange"));
			if (obj == null)
			{
				return false;
			}
			offsetX = (int)Math.Round(ReadDouble(ReadDynamicProperty(obj, "boundingLeft")).GetValueOrDefault());
			offsetY = (int)Math.Round(ReadDouble(ReadDynamicProperty(obj, "boundingTop")).GetValueOrDefault());
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static double? ReadDouble(object value)
	{
		if (value == null)
		{
			return null;
		}
		if (value is double value2)
		{
			return value2;
		}
		if (value is int num)
		{
			return num;
		}
		if (value is float num2)
		{
			return num2;
		}
		if (double.TryParse(SafeToString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return result;
		}
		if (double.TryParse(SafeToString(value), out result))
		{
			return result;
		}
		return null;
	}

	private bool TryHitTestAtViewportOffset(object topDocument, IntPtr docHwnd, int hostClientX, int hostClientY, int screenX, int screenY, int viewportOffsetX, int viewportOffsetY, IList<object> framePathOut, out object elementOut, out object elementDocumentOut)
	{
		elementOut = null;
		elementDocumentOut = null;
		List<object> list = new List<object>();
		int clientX = hostClientX - viewportOffsetX;
		int clientY = hostClientY - viewportOffsetY;
		if (!TryHitTestDocument(topDocument, clientX, clientY, list, out elementOut, out elementDocumentOut))
		{
			return false;
		}
		Rectangle? rectangle = TryGetRawElementScreenBounds(docHwnd, elementOut, elementDocumentOut, list, viewportOffsetX, viewportOffsetY);
		if (!rectangle.HasValue)
		{
			return false;
		}
		if (screenX < rectangle.Value.Left - 2 || screenX >= rectangle.Value.Right + 2 || screenY < rectangle.Value.Top - 2 || screenY >= rectangle.Value.Bottom + 2)
		{
			elementOut = null;
			elementDocumentOut = null;
			return false;
		}
		framePathOut.Clear();
		foreach (object item in list)
		{
			framePathOut.Add(item);
		}
		return true;
	}

	private Rectangle? TryGetRawElementScreenBounds(IntPtr docHwnd, object rawElement, object elementDocument, IList<object> framePath, int viewportOffsetX, int viewportOffsetY)
	{
		if (rawElement == null || docHwnd == IntPtr.Zero)
		{
			return null;
		}
		object obj = SafeRead(() => InvokeDynamicMethod(rawElement, "getBoundingClientRect"));
		double num = 0.0;
		double num2 = 0.0;
		double num3 = 0.0;
		double num4 = 0.0;
		if (obj != null)
		{
			num = SafeDouble(ReadDynamicProperty(obj, "left")).GetValueOrDefault();
			num2 = SafeDouble(ReadDynamicProperty(obj, "top")).GetValueOrDefault();
			num4 = SafeDouble(ReadDynamicProperty(obj, "width")).GetValueOrDefault();
			num3 = SafeDouble(ReadDynamicProperty(obj, "height")).GetValueOrDefault();
		}
		else
		{
			num = SafeDouble(ReadDynamicProperty(rawElement, "offsetLeft")).GetValueOrDefault();
			num2 = SafeDouble(ReadDynamicProperty(rawElement, "offsetTop")).GetValueOrDefault();
			num4 = SafeDouble(ReadDynamicProperty(rawElement, "offsetWidth")).GetValueOrDefault();
			num3 = SafeDouble(ReadDynamicProperty(rawElement, "offsetHeight")).GetValueOrDefault();
		}
		if (num4 <= 0.0 || num3 <= 0.0)
		{
			return null;
		}
		List<object> framePath2 = ((framePath == null) ? new List<object>() : new List<object>(framePath));
		double offsetX = 0.0;
		double offsetY = 0.0;
		AccumulateFrameClientOffset(document_object(), framePath2, 0, ref offsetX, ref offsetY);
		int num5 = (int)Math.Round(num + offsetX + (double)viewportOffsetX);
		int num6 = (int)Math.Round(num2 + offsetY + (double)viewportOffsetY);
		int clientX = num5 + (int)Math.Round(num4);
		int clientY = num6 + (int)Math.Round(num3);
		NativeMethods.POINT pOINT = ClientToScreen(docHwnd, num5, num6);
		NativeMethods.POINT pOINT2 = ClientToScreen(docHwnd, clientX, clientY);
		return Rectangle.FromLTRB(pOINT.X, pOINT.Y, pOINT2.X, pOINT2.Y);
	}

	private static NativeMethods.POINT NormalizeScreenPoint(int screenX, int screenY)
	{
		NativeMethods.POINT pOINT = default(NativeMethods.POINT);
		pOINT.X = screenX;
		pOINT.Y = screenY;
		NativeMethods.POINT lpPoint = pOINT;
		try
		{
			NativeMethods.PhysicalToLogicalPointForPerMonitor(IntPtr.Zero, ref lpPoint);
		}
		catch
		{
		}
		return lpPoint;
	}

	private static IntPtr ResolveDocHwndAtScreenPoint(IntPtr fallbackDocHwnd, int screenX, int screenY)
	{
		NativeMethods.POINT pOINT = default(NativeMethods.POINT);
		pOINT.X = screenX;
		pOINT.Y = screenY;
		NativeMethods.POINT point = pOINT;
		IntPtr intPtr = NativeMethods.WindowFromPoint(point);
		while (intPtr != IntPtr.Zero)
		{
			if (string.Equals(SafeGetClassName(intPtr), "Internet Explorer_Server", StringComparison.OrdinalIgnoreCase))
			{
				return intPtr;
			}
			intPtr = NativeMethods.GetParent(intPtr);
		}
		return fallbackDocHwnd;
	}

	private static object ResolveFrameReference(object frameElement, object innerDocument)
	{
		string text = SafeToString(ReadDynamicProperty(frameElement, "name"));
		if (!string.IsNullOrWhiteSpace(text))
		{
			return text;
		}
		string text2 = SafeToString(ReadDynamicProperty(frameElement, "id"));
		if (!string.IsNullOrWhiteSpace(text2))
		{
			return text2;
		}
		object obj = ReadDynamicProperty(innerDocument, "parentWindow");
		obj = ReadDynamicProperty(obj, "document") ?? obj;
		object frames = GetFrameCollection(obj);
		int frameCount = GetFrameCount(frames);
		int index;
		for (index = 0; index < frameCount; index++)
		{
			object obj2 = TryGetFrameWindowByTypedInterop(frames, index) ?? SafeRead(() => InvokeDynamicMethod(frames, "item", index));
			if (obj2 != null)
			{
				object frameWindowDocument = GetFrameWindowDocument(obj2);
				if (frameWindowDocument == innerDocument)
				{
					return index;
				}
			}
		}
		return 0;
	}

	private static bool IsFrameTag(string tagName)
	{
		return string.Equals(tagName, "iframe", StringComparison.OrdinalIgnoreCase) || string.Equals(tagName, "frame", StringComparison.OrdinalIgnoreCase);
	}

	private static void AccumulateFrameClientOffset(object topDocument, IList<object> framePath, int depth, ref double offsetX, ref double offsetY)
	{
		if (framePath == null || depth >= framePath.Count || topDocument == null)
		{
			return;
		}
		object obj = topDocument;
		for (int i = 0; i <= depth && i < framePath.Count; i++)
		{
			object frameRef = framePath[i];
			object frames = GetFrameCollection(obj);
			if (frames == null)
			{
				break;
			}
			object obj2 = TryGetFrameWindowByTypedInterop(frames, frameRef) ?? SafeRead(() => InvokeDynamicMethod(frames, "item", frameRef));
			if (obj2 == null)
			{
				break;
			}
			object frameElement = ReadDynamicProperty(obj2, "frameElement");
			if (frameElement != null)
			{
				object instance = SafeRead(() => InvokeDynamicMethod(frameElement, "getBoundingClientRect"));
				offsetX += SafeDouble(ReadDynamicProperty(instance, "left")).GetValueOrDefault();
				offsetY += SafeDouble(ReadDynamicProperty(instance, "top")).GetValueOrDefault();
			}
			obj = GetFrameWindowDocument(obj2);
			if (obj == null)
			{
				break;
			}
		}
	}

	private static bool RectContainsPoint(NativeMethods.RECT rect, int x, int y)
	{
		return x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom;
	}

	private static NativeMethods.POINT ScreenToClient(IntPtr hwnd, int screenX, int screenY)
	{
		NativeMethods.POINT pOINT = default(NativeMethods.POINT);
		pOINT.X = screenX;
		pOINT.Y = screenY;
		NativeMethods.POINT lpPoint = pOINT;
		NativeMethods.ScreenToClient(hwnd, ref lpPoint);
		return lpPoint;
	}

	private static NativeMethods.POINT ClientToScreen(IntPtr hwnd, int clientX, int clientY)
	{
		NativeMethods.POINT pOINT = default(NativeMethods.POINT);
		pOINT.X = clientX;
		pOINT.Y = clientY;
		NativeMethods.POINT lpPoint = pOINT;
		NativeMethods.ClientToScreen(hwnd, ref lpPoint);
		return lpPoint;
	}

	private static double? SafeDouble(object value)
	{
		if (value == null)
		{
			return null;
		}
		try
		{
			return Convert.ToDouble(value, CultureInfo.InvariantCulture);
		}
		catch
		{
			return null;
		}
	}

	private static IEnumerable<IntPtr> IterTopLevelWindows(string title, string titleRegex, long? hwnd)
	{
		Regex titlePattern = null;
		if (!string.IsNullOrWhiteSpace(titleRegex))
		{
			titlePattern = new Regex(titleRegex, RegexOptions.IgnoreCase | RegexOptions.Compiled);
		}
		List<IntPtr> matched = new List<IntPtr>();
		NativeMethods.EnumWindows(delegate(IntPtr candidateHwnd, IntPtr _)
		{
			if (!NativeMethods.IsWindowVisible(candidateHwnd))
			{
				return true;
			}
			if (hwnd.HasValue && candidateHwnd.ToInt64() != hwnd.Value)
			{
				return true;
			}
			string text = SafeGetWindowText(candidateHwnd);
			if (title != null && !string.Equals(text, title, StringComparison.Ordinal))
			{
				return true;
			}
			if (titlePattern != null && !titlePattern.IsMatch(text))
			{
				return true;
			}
			matched.Add(candidateHwnd);
			return true;
		}, IntPtr.Zero);
		return matched;
	}

	[IteratorStateMachine(typeof(_003CIterEmbeddedIeDocuments_003Ed__103))]
	private static IEnumerable<IntPtr> IterEmbeddedIeDocuments(IntPtr topHwnd)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CIterEmbeddedIeDocuments_003Ed__103(-2)
		{
			_003C_003E3__topHwnd = topHwnd
		};
	}

	[IteratorStateMachine(typeof(_003CIterWindowTree_003Ed__104))]
	private static IEnumerable<IntPtr> IterWindowTree(IntPtr topHwnd)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CIterWindowTree_003Ed__104(-2)
		{
			_003C_003E3__topHwnd = topHwnd
		};
	}

	private static EmbeddedIEComWindow CreateEmbeddedWindow(IntPtr topHwnd, IntPtr docHwnd)
	{
		if (!TryGetHtmlDocumentFromHwnd(docHwnd, out var obj))
		{
			return null;
		}
		return new EmbeddedIEComWindow(topHwnd, SafeGetWindowText(topHwnd), SafeGetClassName(topHwnd), docHwnd, obj);
	}

	private static bool TryGetHtmlDocumentFromHwnd(IntPtr hwnd, out object document)
	{
		document = null;
		if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
		{
			return false;
		}
		IntPtr lpdwResult;
		IntPtr intPtr = NativeMethods.SendMessageTimeout(hwnd, WmHtmlGetObject, IntPtr.Zero, IntPtr.Zero, 2u, 1000u, out lpdwResult);
		if (intPtr == IntPtr.Zero || lpdwResult == IntPtr.Zero)
		{
			return false;
		}
		Guid riid = IHtmlDocument2Guid;
		if (NativeMethods.ObjectFromLresult(lpdwResult, ref riid, 0u, out var ppvObject) != 0 || ppvObject == null)
		{
			riid = IDispatchGuid;
			if (NativeMethods.ObjectFromLresult(lpdwResult, ref riid, 0u, out ppvObject) != 0 || ppvObject == null)
			{
				return false;
			}
		}
		document = ppvObject;
		return true;
	}

	private static object ReadDynamicProperty(object instance, string propertyName)
	{
		if (instance == null || string.IsNullOrWhiteSpace(propertyName))
		{
			return null;
		}
		try
		{
			return instance.GetType().InvokeMember(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetProperty, null, instance, null);
		}
		catch
		{
			try
			{
				return instance.GetType().InvokeMember(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.GetField, null, instance, null);
			}
			catch
			{
				return null;
			}
		}
	}

	private static object ReadDynamicProperty(object instance, string propertyName, object defaultValue)
	{
		object obj = ReadDynamicProperty(instance, propertyName);
		return obj ?? defaultValue;
	}

	private static object ReadDynamicNestedProperty(object instance, params string[] propertyNames)
	{
		object obj = instance;
		for (int i = 0; i < propertyNames.Length; i++)
		{
			obj = ReadDynamicProperty(obj, propertyNames[i]);
			if (obj == null)
			{
				return null;
			}
		}
		return obj;
	}

	private static bool IsDocumentReady(string readyState)
	{
		if (string.IsNullOrWhiteSpace(readyState))
		{
			return false;
		}
		return readyState.Equals("complete", StringComparison.OrdinalIgnoreCase) || readyState.Equals("interactive", StringComparison.OrdinalIgnoreCase);
	}

	private static string SafeGetWindowText(IntPtr hwnd)
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder(512);
			NativeMethods.GetWindowText(hwnd, stringBuilder, stringBuilder.Capacity);
			return stringBuilder.ToString();
		}
		catch
		{
			return string.Empty;
		}
	}

	private static string SafeGetClassName(IntPtr hwnd)
	{
		try
		{
			StringBuilder stringBuilder = new StringBuilder(256);
			NativeMethods.GetClassName(hwnd, stringBuilder, stringBuilder.Capacity);
			return stringBuilder.ToString();
		}
		catch
		{
			return string.Empty;
		}
	}

	private static object SafeRead(Func<object> action)
	{
		try
		{
			return action();
		}
		catch
		{
			return null;
		}
	}

	private static string SafeToString(object value)
	{
		return (value == null) ? string.Empty : (Convert.ToString(value) ?? string.Empty);
	}

	private static int? SafeInt(object value)
	{
		try
		{
			if (value == null)
			{
				return null;
			}
			return Convert.ToInt32(value);
		}
		catch
		{
			return null;
		}
	}

	private static string ReadDocumentUrl(object document)
	{
		if (document == null)
		{
			return string.Empty;
		}
		object value = ReadDynamicProperty(document, "url") ?? ReadDynamicProperty(document, "URL") ?? ReadDynamicNestedProperty(document, "parentWindow", "location", "href") ?? ReadDynamicNestedProperty(document, "parentWindow", "location");
		return SafeToString(value);
	}

	private static int ToSleepMilliseconds(int milliseconds, int minimumMilliseconds = 0)
	{
		if (milliseconds < 0)
		{
			milliseconds = 0;
		}
		int num = milliseconds;
		if (num < minimumMilliseconds)
		{
			num = minimumMilliseconds;
		}
		return num;
	}

	private static string FormatValue(string value)
	{
		return (value == null) ? "null" : ("'" + value + "'");
	}

	private static string FormatAny(object value)
	{
		return (value == null) ? "null" : ("'" + SafeToString(value) + "'");
	}

	private static bool IsRetryableFrameError(Exception ex)
	{
		string text = ((ex == null) ? string.Empty : (ex.Message ?? string.Empty));
		return text.IndexOf("Frame not found", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Current document does not contain frames", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Frame has no accessible document", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Unable to obtain IE document", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private static string DescribeFramePath(IEnumerable<object> framePath)
	{
		if (framePath == null)
		{
			return "[]";
		}
		List<string> list = new List<string>();
		foreach (object item in framePath)
		{
			list.Add(FormatAny(item));
		}
		return "[" + string.Join(", ", list) + "]";
	}

	private static string DescribeLocator(IDictionary<string, object> locator)
	{
		if (locator == null || locator.Count == 0)
		{
			return "{}";
		}
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, object> item in locator)
		{
			list.Add(item.Key + "=" + FormatAny(item.Value));
		}
		return "{" + string.Join(", ", list) + "}";
	}

	private string BuildNotFoundDiagnostics(IDictionary<string, object> locator, IEnumerable<object> framePath)
	{
		try
		{
			object obj = document(framePath);
			Dictionary<string, object> dictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
			string locatorString = GetLocatorString(locator, "tag");
			if (!string.IsNullOrWhiteSpace(locatorString))
			{
				dictionary["tag"] = locatorString;
			}
			string locatorString2 = GetLocatorString(locator, "type");
			if (!string.IsNullOrWhiteSpace(locatorString2))
			{
				dictionary["type"] = locatorString2;
			}
			List<object> list = new List<object>(LocateElements(obj, dictionary));
			if (list.Count == 0)
			{
				string text = DiagnoseAcrossEmbeddedDocuments(locator, dictionary);
				if (!string.IsNullOrWhiteSpace(text))
				{
					return "no candidates for tag/type; docs=" + text;
				}
				return "no candidates for tag/type";
			}
			List<string> list2 = new List<string>();
			for (int i = 0; i < list.Count && i < 8; i++)
			{
				list2.Add(DescribeCandidate(list[i], i));
			}
			return string.Format("candidates={0}, sample=[{1}]", list.Count, string.Join("; ", list2));
		}
		catch (Exception ex)
		{
			return "diagnose failed: " + ex.Message;
		}
	}

	private string DiagnoseAcrossEmbeddedDocuments(IDictionary<string, object> locator, IDictionary<string, object> probe)
	{
		IntPtr hWND = _window.HWND;
		if (hWND == IntPtr.Zero)
		{
			return string.Empty;
		}
		List<string> list = new List<string>();
		foreach (IntPtr item in IterEmbeddedIeDocuments(hWND))
		{
			try
			{
				EmbeddedIEComWindow embeddedIEComWindow = CreateEmbeddedWindow(hWND, item);
				if (embeddedIEComWindow != null)
				{
					object obj = ReadDynamicProperty(embeddedIEComWindow, "Document") ?? embeddedIEComWindow.refresh_document();
					if (obj != null)
					{
						object instance = PromoteToTopDocument(obj) ?? obj;
						int count = new List<object>(LocateElements(instance, locator)).Count;
						int count2 = new List<object>(LocateElements(instance, probe)).Count;
						string text = SafeToString(ReadDynamicProperty(instance, "title"));
						string text2 = SafeToString(ReadDynamicProperty(instance, "url"));
						list.Add(string.Format("doc=0x{0}, locator={1}, probe={2}, title={3}, url={4}{5}", item.ToInt64().ToString("X"), count, count2, FormatAny(TruncateDebug(text, 36)), FormatAny(TruncateDebug(text2, 48)), (item == _window.DocHWND) ? ", current=1" : string.Empty));
					}
				}
			}
			catch (Exception ex)
			{
				list.Add(string.Format("doc=0x{0}, error={1}", item.ToInt64().ToString("X"), FormatAny(TruncateDebug(ex.Message, 48))));
			}
		}
		return string.Join(" | ", list);
	}

	private static string DescribeDomElement(IEDomElement element)
	{
		if (element == null)
		{
			return "null";
		}
		object obj = element.raw;
		string value = SafeToString(ReadDynamicProperty(obj, "tagName"));
		string value2 = SafeToString(ReadDynamicProperty(obj, "type"));
		string value3 = SafeToString(ReadDynamicProperty(obj, "id"));
		string value4 = SafeToString(ReadDynamicProperty(obj, "name"));
		string text = SafeToString(ReadDynamicProperty(obj, "value"));
		return $"tag={FormatAny(value)}, type={FormatAny(value2)}, id={FormatAny(value3)}, name={FormatAny(value4)}, value={FormatAny(TruncateDebug(text, 60))}, interactable={IsElementInteractable(obj)}";
	}

	private static void DebugLog(string stage, string message, params object[] args)
	{
		bool flag = true;
	}

	private static string DescribeCandidate(object element, int index)
	{
		string value = SafeToString(ReadDynamicProperty(element, "tagName"));
		string value2 = SafeToString(ReadDynamicProperty(element, "type"));
		string value3 = SafeToString(ReadDynamicProperty(element, "id"));
		string value4 = SafeToString(ReadDynamicProperty(element, "name"));
		string text = NormalizeText(SafeToString(ReadDynamicProperty(element, "value")));
		string text2 = NormalizeText(SafeToString(ReadDynamicProperty(element, "innerText")));
		if (string.IsNullOrWhiteSpace(text2))
		{
			text2 = NormalizeText(SafeToString(ReadDynamicProperty(element, "textContent")));
		}
		return $"#{index}(tag={FormatAny(value)},type={FormatAny(value2)},id={FormatAny(value3)},name={FormatAny(value4)},value={FormatAny(TruncateDebug(text, 40))},text={FormatAny(TruncateDebug(text2, 40))},interactable={IsElementInteractable(element)})";
	}

	private static string TruncateDebug(string text, int maxLength)
	{
		if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
		{
			return text;
		}
		return text.Substring(0, maxLength) + "...";
	}

	private static string GetLocatorString(IDictionary<string, object> locator, string key)
	{
		return SafeToString(GetLocatorValue(locator, key));
	}

	private static object GetLocatorValue(IDictionary<string, object> locator, string key)
	{
		if (locator == null || string.IsNullOrWhiteSpace(key))
		{
			return null;
		}
		foreach (KeyValuePair<string, object> item in locator)
		{
			if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
			{
				return item.Value;
			}
		}
		return null;
	}

	private static IDictionary<string, object> GetLocatorDictionary(IDictionary<string, object> locator, string key)
	{
		return GetLocatorValue(locator, key) as IDictionary<string, object>;
	}

	private static bool MatchesString(string actual, string expected, bool ignoreCase)
	{
		if (string.IsNullOrWhiteSpace(expected))
		{
			return true;
		}
		return string.Equals(actual ?? string.Empty, expected, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
	}

	private static string NormalizeText(string value)
	{
		return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
	}

	private static string ToJavaScriptLiteral(object value)
	{
		if (value == null)
		{
			return "null";
		}
		if (value is string text)
		{
			return "'" + text.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "\\r")
				.Replace("\n", "\\n")
				.Replace("\t", "\\t") + "'";
		}
		if (value is bool)
		{
			return ((bool)value) ? "true" : "false";
		}
		if (value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal)
		{
			return Convert.ToString(value, CultureInfo.InvariantCulture);
		}
		if (value is DateTime dateTime)
		{
			return "new Date('" + dateTime.ToString("o", CultureInfo.InvariantCulture) + "')";
		}
		if (value is IDictionary dictionary)
		{
			List<string> list = new List<string>();
			foreach (DictionaryEntry item in dictionary)
			{
				string key = SafeToString(item.Key);
				list.Add(ToJavaScriptObjectKey(key) + ":" + ToJavaScriptLiteral(item.Value));
			}
			return "{" + string.Join(",", list) + "}";
		}
		if (!(value is string) && value is IEnumerable enumerable)
		{
			List<string> list2 = new List<string>();
			foreach (object item2 in enumerable)
			{
				list2.Add(ToJavaScriptLiteral(item2));
			}
			return "[" + string.Join(",", list2) + "]";
		}
		return ToJavaScriptLiteral(SafeToString(value));
	}

	private static string ToJavaScriptObjectKey(string key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return "''";
		}
		if (Regex.IsMatch(key, "^[A-Za-z_$][A-Za-z0-9_$]*$"))
		{
			return key;
		}
		return ToJavaScriptLiteral(key);
	}
}
