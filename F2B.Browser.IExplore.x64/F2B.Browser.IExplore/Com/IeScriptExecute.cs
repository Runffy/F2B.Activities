using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>Execute JavaScript in MSHTML document / element context (IE).</summary>
    internal static class IeScriptExecute
    {
        private const string ResultSlot = "__ieExecResult";
        private const string ElementSlot = "__ieExecElement";
        private const string ArgsSlot = "__ieExecArgs";

        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
        private static readonly string[] ScriptLanguages = { "JavaScript", "JScript" };

        public static object Execute(
            IHTMLDocument2 document,
            string script,
            object element = null,
            IList<object> args = null)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (string.IsNullOrWhiteSpace(script))
                throw new ArgumentException("Script is required.", nameof(script));

            var fullScript = BuildFullScript(script, element, args);
            RunScript(document, fullScript);
            return ReadResult(document);
        }

        /// <summary>Run a script statement in the document window (no result wrapper).</summary>
        public static bool TryRunStatement(IHTMLDocument2 document, string statement)
        {
            if (document == null || string.IsNullOrWhiteSpace(statement))
                return false;

            try
            {
                RunScript(document, statement);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static IList<object> ParseArgsJson(string argsJson)
        {
            if (string.IsNullOrWhiteSpace(argsJson))
                return null;

            var normalized = IEJsonParse.NormalizeToJson(argsJson.Trim());
            var raw = Json.DeserializeObject(normalized);

            var list = raw as IList;
            if (list != null)
            {
                var args = new List<object>(list.Count);
                foreach (var item in list)
                    args.Add(item);
                return args;
            }

            if (raw is IDictionary)
                return new List<object> { raw };

            return new List<object> { raw };
        }

        private static string BuildFullScript(string userScript, object element, IList<object> args)
        {
            var sb = new StringBuilder();
            sb.Append("window.").Append(ElementSlot).Append('=');

            if (ComElementHelper.IsValidElement(element))
            {
                var id = ReadElementId(element);
                if (!string.IsNullOrEmpty(id))
                    sb.Append("document.getElementById('").Append(EscapeJsString(id)).Append("')");
                else
                    sb.Append("null");
            }
            else
            {
                sb.Append("null");
            }

            sb.Append(";window.").Append(ArgsSlot).Append('=');
            sb.Append(args == null || args.Count == 0 ? "null" : Json.Serialize(args));
            sb.Append(";window.").Append(ResultSlot).Append("=(function(element,args){");
            sb.Append(userScript);
            if (!userScript.TrimEnd().EndsWith(";"))
                sb.Append(';');
            sb.Append("})(window.").Append(ElementSlot).Append(",window.").Append(ArgsSlot).Append(");");
            sb.Append("try{var __iev_r=window.").Append(ResultSlot).Append(";");
            sb.Append("if(__iev_r!==undefined&&__iev_r!==null)");
            sb.Append("document.body.setAttribute('data-ie-exec-result',String(__iev_r));");
            sb.Append("else document.body.removeAttribute('data-ie-exec-result');}catch(e){}");
            return sb.ToString();
        }

        private static object ReadResult(IHTMLDocument2 document)
        {
            try
            {
                dynamic win = GetScriptWindow(document);
                object value = null;

                try { value = win.__ieExecResult; }
                catch { /* ignore */ }

                if (value == null || value is DBNull)
                {
                    try { value = win[ResultSlot]; }
                    catch { /* ignore */ }
                }

                if (value == null || value is DBNull)
                    value = ReadResultFromBodyAttribute(document);

                if (value == null || value is DBNull)
                    return null;

                return value;
            }
            catch
            {
                return ReadResultFromBodyAttribute(document);
            }
        }

        private static object ReadResultFromBodyAttribute(IHTMLDocument2 document)
        {
            try
            {
                dynamic doc = document;
                dynamic body = doc.body;
                if (body == null)
                    return null;

                object attr = body.getAttribute("data-ie-exec-result");
                if (attr == null || attr is DBNull)
                    return null;

                return attr.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static dynamic GetScriptWindow(IHTMLDocument2 document)
        {
            dynamic doc = document;
            return doc.parentWindow;
        }

        private static void RunScript(IHTMLDocument2 document, string script)
        {
            Exception last = null;
            dynamic window = GetScriptWindow(document);

            foreach (var language in ScriptLanguages)
            {
                try
                {
                    window.execScript(script, language);
                    return;
                }
                catch (Exception ex)
                {
                    last = ex;
                }
            }

            try
            {
                InjectScript(document, script);
                return;
            }
            catch (Exception ex)
            {
                last = ex;
            }

            throw new InvalidOperationException(
                "JavaScript execution failed. " + last?.Message,
                last);
        }

        private static void InjectScript(IHTMLDocument2 document, string script)
        {
            dynamic doc = document;
            dynamic body = doc.body;
            if (body == null)
                throw new InvalidOperationException("document.body is null.");

            dynamic scriptEl = doc.createElement("script");
            scriptEl.type = "text/javascript";
            scriptEl.text = script;
            body.appendChild(scriptEl);
        }

        private static string ReadElementId(object element)
        {
            try
            {
                dynamic el = element;
                return (string)el.id ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string EscapeJsString(string value) =>
            (value ?? string.Empty).Replace("\\", "\\\\").Replace("'", "\\'");
    }
}
