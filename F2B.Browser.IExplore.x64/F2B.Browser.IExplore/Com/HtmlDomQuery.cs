using System;
using System.Collections.Generic;
using System.Text;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>Fast MSHTML queries (typed COM + IE fallbacks).</summary>
    internal static class HtmlDomQuery
    {
        /// <summary>Document Mode 5 / Edge IE Mode: typed <see cref="IHTMLDocument2.getElementById"/> may return null; dynamic matches IEChooser.</summary>
        public static object TryGetElementById(IHTMLDocument2 document, string id)
        {
            if (document == null || string.IsNullOrEmpty(id))
                return null;

            try
            {
                dynamic d = document;
                return d.getElementById(id);
            }
            catch
            {
                return null;
            }
        }

        public static List<object> CollectByName(IHTMLDocument2 document, string name)
        {
            var hits = new List<object>();
            if (document == null || string.IsNullOrWhiteSpace(name))
                return hits;

            name = name.Trim();
            AppendUnique(hits, MaterializeCollection(GetElementsByName(document, name)));
            if (hits.Count > 0)
                return hits;

            AppendUnique(hits, CollectDocumentAllNamed(document, name));
            return hits;
        }

        public static List<object> MaterializeCollection(object collection)
        {
            var items = new List<object>();
            if (collection == null)
                return items;

            int length;
            try { length = (int)((dynamic)collection).length; }
            catch { return items; }

            for (var i = 0; i < length; i++)
            {
                object el;
                try { el = ((dynamic)collection).item(i); }
                catch { continue; }

                if (ComElementHelper.IsValidElement(el))
                    items.Add(el);
            }

            return items;
        }

        public static string NameAttributeSelector(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            return "[name=\"" + EscapeCssString(name) + "\"]";
        }

        public static object GetElementsByName(IHTMLDocument2 document, string name)
        {
            if (document == null || string.IsNullOrEmpty(name))
                return null;

            try
            {
                return document.getElementsByName(name);
            }
            catch
            {
                try
                {
                    dynamic doc = document;
                    return doc.getElementsByName(name);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static List<object> CollectDocumentAllNamed(IHTMLDocument2 document, string name)
        {
            var items = new List<object>();
            try
            {
                dynamic doc = document;
                dynamic all = doc.all;
                if (all == null)
                    return items;

                object hit = null;
                try { hit = all.item(name, 0); }
                catch
                {
                    try { hit = all.item(name); }
                    catch { /* ignore */ }
                }

                AppendAllNamedHits(items, hit);
            }
            catch
            {
                // ignore
            }

            return items;
        }

        private static void AppendAllNamedHits(List<object> items, object hit)
        {
            if (!ComElementHelper.IsValidElement(hit))
            {
                if (hit == null)
                    return;

                int length;
                try { length = (int)((dynamic)hit).length; }
                catch { return; }

                for (var i = 0; i < length; i++)
                {
                    object el;
                    try { el = ((dynamic)hit).item(i); }
                    catch { continue; }

                    if (ComElementHelper.IsValidElement(el))
                        items.Add(el);
                }

                return;
            }

            items.Add(hit);
        }

        private static void AppendUnique(List<object> target, List<object> source)
        {
            if (source == null || source.Count == 0)
                return;

            foreach (var el in source)
            {
                if (!ComElementHelper.IsValidElement(el))
                    continue;

                if (!target.Contains(el))
                    target.Add(el);
            }
        }

        private static string EscapeCssString(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (var ch in value)
            {
                if (ch == '\\' || ch == '"')
                    sb.Append('\\');
                sb.Append(ch);
            }

            return sb.ToString();
        }
    }
}
