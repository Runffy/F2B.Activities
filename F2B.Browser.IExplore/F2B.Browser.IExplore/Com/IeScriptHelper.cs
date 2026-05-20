using System;
using System.Globalization;
using System.Reflection;

namespace F2B.Browser.IExplore.Com
{
    internal static class IeScriptHelper
    {
        /// <summary>Run page init such as <c>wireDemoHandlers</c> (no execScript).</summary>
        public static void EnsurePageHandlers(IHTMLDocument2 document, params string[] initFunctionNames)
        {
            if (document == null || initFunctionNames == null)
                return;

            foreach (var name in initFunctionNames)
            {
                if (!string.IsNullOrWhiteSpace(name))
                    TryInvokeScriptFunction(document, name);
            }
        }

        /// <summary>
        /// Invoke click handlers without execScript (blocked on file:// with "Access denied").
        /// </summary>
        public static void InvokeElementClick(IHTMLDocument2 document, object element, int? clientX, int? clientY)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (!ComElementHelper.IsValidElement(element))
                throw new InvalidOperationException("Element was not found (MSHTML returned null/DBNull).");

            EnsurePageHandlers(document, "wireDemoHandlers");

            var elementId = ReadProperty(element, "id");
            if (TryScriptClickViaTop(document, elementId))
                return;

            var inlineHandler = ReadAttribute(element, "onclick");
            if (!string.IsNullOrWhiteSpace(inlineHandler))
            {
                var fnName = ParseFunctionName(inlineHandler);
                if (!string.IsNullOrEmpty(fnName) && TryInvokeScriptFunction(document, fnName))
                    return;
            }

            // IE: BUTTON often ignores COM onclick/fireEvent; call page script by id (e.g. onGoClick).
            var handlerFromId = GetScriptHandlerNameFromElementId(elementId);
            if (!string.IsNullOrEmpty(handlerFromId) && TryInvokeScriptFunction(document, handlerFromId))
                return;

            if (TryInvokeElementOnclick(element))
                return;

            FireIeEvent(element, "onclick");
            FireIeEvent(element, "click");

            try
            {
                dynamic el = element;
                el.click();
            }
            catch
            {
                // ignore
            }

            if (clientX.HasValue && clientY.HasValue)
                TryInvokeElementFromPoint(document, clientX.Value, clientY.Value);
        }

        /// <summary>Double-click via wired handler or a single synthetic dblclick event.</summary>
        public static void InvokeElementDoubleClick(IHTMLDocument2 document, object element, int? clientX, int? clientY)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (!ComElementHelper.IsValidElement(element))
                throw new InvalidOperationException("Element was not found (MSHTML returned null/DBNull).");

            EnsurePageHandlers(document, "wireDemoHandlers");

            var dblHandler = GetScriptHandlerNameFromElementId(ReadProperty(element, "id"));
            if (!string.IsNullOrEmpty(dblHandler) && TryInvokeScriptFunction(document, dblHandler))
                return;

            if (TryInvokeElementOndblclick(element))
                return;

            FireDoubleClickEvent(element);

            if (clientX.HasValue && clientY.HasValue)
                TryInvokeElementFromPoint(document, clientX.Value, clientY.Value);
        }

        private static bool TryInvokeElementOndblclick(object element)
        {
            try
            {
                dynamic el = element;
                object handler = el.ondblclick;
                if (handler == null || handler is DBNull)
                    return false;

                if (handler is string)
                    return false;

                el.ondblclick();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void FireDoubleClickEvent(object element)
        {
            try
            {
                dynamic el = element;
                el.fireEvent("ondblclick");
            }
            catch { /* ignore */ }
        }

        private static bool TryInvokeScriptFunction(IHTMLDocument2 document, string functionName)
        {
            if (string.IsNullOrWhiteSpace(functionName))
                return false;

            var call = functionName.Trim() + "();";
            if (IeScriptExecute.TryRunStatement(document, call))
                return true;

            var top = GetTopDocument(document);
            if (top != null && !IsSameDocument(document, top) && IeScriptExecute.TryRunStatement(top, call))
                return true;

            if (functionName == "onGoClick" && top != null
                && IeScriptExecute.TryRunStatement(
                    top,
                    "try{var w=top.frames['f-go'];if(w&&typeof w.onGoClick==='function')w.onGoClick();}catch(e){}"))
                return true;

            try
            {
                dynamic doc = document;
                object script = doc.Script;
                if (script == null)
                    return false;

                script.GetType().InvokeMember(
                    functionName.Trim(),
                    BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    script,
                    null,
                    CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryScriptClickViaTop(IHTMLDocument2 ownerDocument, string elementId)
        {
            if (string.IsNullOrEmpty(elementId))
                return false;

            var top = GetTopDocument(ownerDocument);
            if (top == null)
                return false;

            string statement;
            switch (elementId)
            {
                case "go":
                    statement =
                        "try{var w=top.frames['f-go'];"
                        + "if(!w&&top.frames['f-actions'])w=top.frames['f-actions'].frames['f-go'];"
                        + "if(w&&typeof w.onGoClick==='function')w.onGoClick();}catch(e){}";
                    break;
                case "pick-0":
                    statement = "try{if(typeof onPick0==='function')onPick0();}catch(e){}";
                    break;
                case "pick-1":
                    statement = "try{if(typeof onPick1==='function')onPick1();}catch(e){}";
                    break;
                default:
                    if (!IsSameDocument(ownerDocument, top))
                        return false;
                    statement =
                        "try{var e=document.getElementById('"
                        + EscapeJsString(elementId)
                        + "');if(e&&e.onclick)e.onclick();else if(e)e.click();}catch(x){}";
                    break;
            }

            return IeScriptExecute.TryRunStatement(top, statement);
        }

        private static IHTMLDocument2 GetTopDocument(IHTMLDocument2 document)
        {
            if (document == null)
                return null;

            try
            {
                dynamic doc = document;
                dynamic win = doc.parentWindow;
                while (true)
                {
                    dynamic parent;
                    try { parent = win.parent; }
                    catch { break; }

                    if (parent == null)
                        break;

                    try
                    {
                        if (parent == win)
                            break;
                    }
                    catch
                    {
                        break;
                    }

                    win = parent;
                }

                return win.document as IHTMLDocument2;
            }
            catch
            {
                return document;
            }
        }

        private static bool IsSameDocument(IHTMLDocument2 a, IHTMLDocument2 b)
        {
            if (a == null || b == null)
                return false;
            if (ReferenceEquals(a, b))
                return true;

            try
            {
                var urlA = HtmlDocumentHelper.ReadDocumentUrl(a);
                var urlB = HtmlDocumentHelper.ReadDocumentUrl(b);
                return !string.IsNullOrEmpty(urlA)
                    && urlA.Equals(urlB, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static string EscapeJsString(string value) =>
            (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");

        private static bool TryInvokeElementOnclick(object element)
        {
            try
            {
                dynamic el = element;
                object handler = el.onclick;
                if (handler == null || handler is DBNull)
                    return false;

                if (handler is string)
                    return false;

                el.onclick();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryInvokeElementFromPoint(IHTMLDocument2 document, int x, int y)
        {
            try
            {
                dynamic doc = document;
                dynamic el = doc.elementFromPoint(x, y);
                if (!ComElementHelper.IsValidElement(el))
                    return;

                if (TryInvokeElementOnclick(el))
                    return;

                FireIeEvent(el, "onclick");
            }
            catch
            {
                // ignore
            }
        }

        private static void FireIeEvent(object element, string eventName)
        {
            try
            {
                dynamic el = element;
                dynamic doc = el.document;
                dynamic evt = doc.createEventObject();
                el.fireEvent(eventName, evt);
            }
            catch
            {
                // ignore
            }
        }

        private static string ParseFunctionName(string inlineHandler)
        {
            var s = (inlineHandler ?? string.Empty).Trim();
            if (s.EndsWith(";", StringComparison.Ordinal))
                s = s.Substring(0, s.Length - 1).Trim();

            var paren = s.IndexOf('(');
            if (paren > 0)
                return s.Substring(0, paren).Trim();

            return s;
        }

        /// <summary>Maps known demo ids to page script functions.</summary>
        private static string GetScriptHandlerNameFromElementId(string elementId)
        {
            if (string.IsNullOrEmpty(elementId))
                return null;

            switch (elementId)
            {
                case "go": return "onGoClick";
                case "status": return "onStatusClick";
                case "pick-0": return "onPick0";
                case "pick-1": return "onPick1";
                case "dbl-target": return "onDblTarget";
                default:
                    return "on"
                        + char.ToUpper(elementId[0], CultureInfo.InvariantCulture)
                        + (elementId.Length > 1 ? elementId.Substring(1) : string.Empty)
                        + "Click";
            }
        }

        private static string ReadProperty(object element, string name)
        {
            try
            {
                dynamic el = element;
                if (name == "id")
                    return (string)el.id;
                if (name == "tagName")
                    return (string)el.tagName;
            }
            catch { /* ignore */ }

            return null;
        }

        private static string ReadAttribute(object element, string name)
        {
            try
            {
                dynamic el = element;
                object v = el.getAttribute(name);
                if (v == null || v is DBNull)
                    return null;
                return v.ToString();
            }
            catch
            {
                return null;
            }
        }
    }
}
