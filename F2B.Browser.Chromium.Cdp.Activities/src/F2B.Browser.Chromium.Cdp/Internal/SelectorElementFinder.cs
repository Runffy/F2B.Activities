using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;
using F2B.Browser.Chromium.Cdp.Selectors;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class SelectorElementFinder
    {
        private const string FinderScript =
            @"(function(levels, findAll, markPrefix) {
    function getProp(level, name) {
        if (!level || !level.props) return null;
        var target = (name || '').toLowerCase();
        for (var i = 0; i < level.props.length; i++) {
            if ((level.props[i].name || '').toLowerCase() === target) return level.props[i];
        }
        return null;
    }

    function readValue(el, name) {
        if (!el || el.nodeType !== 1) return '';
        var key = (name || '').toLowerCase();
        if (key === 'tag') return (el.tagName || '').toLowerCase();
        if (key === 'text') return (el.innerText || el.textContent || '').trim();
        if (key === 'class') return el.className || '';
        if (el.hasAttribute && el.hasAttribute(name)) {
            var attr = el.getAttribute(name);
            return attr == null ? '' : String(attr);
        }
        if (key === 'id' && el.id) return el.id;
        if (key === 'value' && el.value != null && el.value !== '') return String(el.value);
        if (key === 'type' && el.type) return String(el.type);
        if (key === 'href' && el.href) return el.getAttribute('href') || String(el.href);
        if (key === 'title' && el.title) return el.title;
        return el.getAttribute(name) || '';
    }

    function matchValue(actual, prop) {
        actual = actual == null ? '' : String(actual);
        var expected = prop.value == null ? '' : String(prop.value);
        var propName = (prop.name || '').toLowerCase();
        if (prop.regex) {
            try { return new RegExp(expected).test(actual); } catch (e) { return false; }
        }
        if (propName === 'class') {
            if (expected === '') return actual === '';
            var classes = actual.split(/\s+/);
            for (var i = 0; i < classes.length; i++) {
                if (classes[i] && classes[i].toLowerCase() === expected.toLowerCase()) return true;
            }
            return false;
        }
        return actual.toLowerCase() === expected.toLowerCase();
    }

    function matchElement(el, level) {
        if (!el || !level || !level.props) return true;
        for (var i = 0; i < level.props.length; i++) {
            var prop = level.props[i];
            if (!prop) continue;
            var propName = (prop.name || '').toLowerCase();
            if (propName === 'idx' || propName === 'level') continue;
            if (!matchValue(readValue(el, prop.name), prop)) return false;
        }
        return true;
    }

    function getSearchRoot(root) {
        if (!root) return null;
        if (root.nodeType === 9) return root.body || root.documentElement;
        if (root.nodeType === 1) return root;
        return null;
    }

    function narrowMatches(matched, level, levelIndex) {
        var idxProp = getProp(level, 'idx');
        if (idxProp && idxProp.value !== '' && idxProp.value != null) {
            var idx = parseInt(idxProp.value, 10);
            if (isNaN(idx) || idx < 0 || idx >= matched.length) return [];
            return [matched[idx]];
        }
        var isLast = levelIndex >= levels.length - 1;
        if (!isLast || !findAll) {
            return matched.length > 0 ? [matched[0]] : [];
        }
        return matched;
    }

    function collectCandidates(searchRoot, excludeSelf, tag, level) {
        var matched = [];
        function consider(el) {
            if (excludeSelf && el === searchRoot) return;
            if (matchElement(el, level)) matched.push(el);
        }
        if (tag === '*') {
            var allNodes = searchRoot.querySelectorAll('*');
            for (var i = 0; i < allNodes.length; i++) consider(allNodes[i]);
            if (!excludeSelf) consider(searchRoot);
        } else {
            var tagNodes = searchRoot.querySelectorAll(tag);
            for (var j = 0; j < tagNodes.length; j++) consider(tagNodes[j]);
            // Document context uses body as searchRoot; querySelectorAll('body') never
            // returns the root itself, so include it when allowed (e.g. ctrl tag=body).
            if (!excludeSelf) consider(searchRoot);
        }
        return matched;
    }

    function applyCtrlOnRoot(root, level, levelIndex) {
        var searchRoot = getSearchRoot(root);
        if (!searchRoot || !searchRoot.querySelectorAll) return [];
        var excludeSelf = (root && root.nodeType === 1 && searchRoot === root);
        var tagProp = getProp(level, 'tag');
        var tag = tagProp && tagProp.value ? tagProp.value.toLowerCase() : '*';
        var matched = collectCandidates(searchRoot, excludeSelf, tag, level);
        return narrowMatches(matched, level, levelIndex);
    }

    function applyParent(nodes, level) {
        var prop = getProp(level, 'level');
        var count = prop && prop.value ? parseInt(prop.value, 10) : 1;
        if (isNaN(count) || count < 1) count = 1;
        var result = [];
        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i];
            for (var j = 0; j < count && node; j++) node = node.parentElement;
            if (node) result.push(node);
        }
        return result;
    }

    function applyFrm(doc, level, levelIndex) {
        var docRoot = getSearchRoot(doc);
        if (!docRoot) return [];
        var frames = docRoot.querySelectorAll('iframe');
        var frameNodes = docRoot.querySelectorAll('frame');
        var matched = [];
        for (var i = 0; i < frames.length; i++) {
            if (matchElement(frames[i], level)) matched.push(frames[i]);
        }
        for (var f = 0; f < frameNodes.length; f++) {
            if (matchElement(frameNodes[f], level)) matched.push(frameNodes[f]);
        }
        return narrowMatches(matched, level, levelIndex);
    }

    function walk(doc, levelIndex) {
        if (levelIndex >= levels.length) return [doc.body || doc.documentElement];
        var level = levels[levelIndex];
        var tag = (level.tag || '').toLowerCase();
        if (tag === 'frm') {
            var frameEls = applyFrm(doc, level, levelIndex);
            // Terminal <frm> â†?host iframe/frame elements (needed by FindFrame / AsFrame).
            if (levelIndex + 1 >= levels.length) return frameEls;
            var docs = [];
            for (var i = 0; i < frameEls.length; i++) {
                try {
                    var inner = frameEls[i].contentDocument;
                    if (inner) docs.push(inner);
                } catch (e) {}
            }
            var next = [];
            for (var j = 0; j < docs.length; j++) {
                var part = walk(docs[j], levelIndex + 1);
                for (var k = 0; k < part.length; k++) next.push(part[k]);
            }
            return next;
        }
        if (tag === 'parent') {
            var parentRoots = applyParent([doc.body || doc.documentElement], level);
            return walkRoots(parentRoots, levelIndex + 1);
        }
        if (tag === 'ctrl') {
            return walkRoots(applyCtrlOnRoot(doc, level, levelIndex), levelIndex + 1);
        }
        return walkRoots([doc.body || doc.documentElement], levelIndex + 1);
    }

    function walkRoots(roots, levelIndex) {
        if (levelIndex >= levels.length) return roots;
        var level = levels[levelIndex];
        var tag = (level.tag || '').toLowerCase();
        if (tag === 'parent') {
            return walkRoots(applyParent(roots, level), levelIndex + 1);
        }
        if (tag === 'ctrl') {
            var next = [];
            for (var i = 0; i < roots.length; i++) {
                var part = applyCtrlOnRoot(roots[i], level, levelIndex);
                for (var j = 0; j < part.length; j++) next.push(part[j]);
            }
            return walkRoots(next, levelIndex + 1);
        }
        if (tag === 'frm') {
            var merged = [];
            for (var r = 0; r < roots.length; r++) {
                var doc = roots[r].ownerDocument || roots[r];
                var frameEls = applyFrm(doc, level, levelIndex);
                if (levelIndex + 1 >= levels.length) {
                    for (var fh = 0; fh < frameEls.length; fh++) merged.push(frameEls[fh]);
                    continue;
                }
                for (var f = 0; f < frameEls.length; f++) {
                    try {
                        var inner = frameEls[f].contentDocument;
                        if (inner) {
                            var part2 = walk(inner, levelIndex + 1);
                            for (var m = 0; m < part2.length; m++) merged.push(part2[m]);
                        }
                    } catch (e) {}
                }
            }
            return merged;
        }
        return walkRoots(roots, levelIndex + 1);
    }

    function walkElement(node, levelIndex) {
        if (levelIndex >= levels.length) return [node];
        var level = levels[levelIndex];
        var tag = (level.tag || '').toLowerCase();
        if (tag === 'frm') {
            var searchRoot = getSearchRoot(node);
            if (!searchRoot || !searchRoot.querySelectorAll) return [];
            var matched = [];
            var frames = searchRoot.querySelectorAll('iframe');
            var frameNodes = searchRoot.querySelectorAll('frame');
            for (var fi = 0; fi < frames.length; fi++) {
                if (matchElement(frames[fi], level)) matched.push(frames[fi]);
            }
            for (var ff = 0; ff < frameNodes.length; ff++) {
                if (matchElement(frameNodes[ff], level)) matched.push(frameNodes[ff]);
            }
            var frameEls = narrowMatches(matched, level, levelIndex);
            // Terminal <frm> under an Element root â†?host iframe (not contentDocument body).
            if (levelIndex + 1 >= levels.length) return frameEls;
            var docs = [];
            for (var i2 = 0; i2 < frameEls.length; i2++) {
                try {
                    var inner = frameEls[i2].contentDocument;
                    if (inner) docs.push(inner.body || inner.documentElement);
                } catch (e) {}
            }
            var next = [];
            for (var j = 0; j < docs.length; j++) {
                var part = walkElement(docs[j], levelIndex + 1);
                for (var k = 0; k < part.length; k++) next.push(part[k]);
            }
            return next;
        }
        if (tag === 'parent') {
            var parents = applyParent([node], level);
            var next2 = [];
            for (var p = 0; p < parents.length; p++) {
                var part2 = walkElement(parents[p], levelIndex + 1);
                for (var m = 0; m < part2.length; m++) next2.push(part2[m]);
            }
            return next2;
        }
        if (tag === 'ctrl') {
            var ctrlMatches = applyCtrlOnRoot(node, level, levelIndex);
            var next3 = [];
            for (var c = 0; c < ctrlMatches.length; c++) {
                var part3 = walkElement(ctrlMatches[c], levelIndex + 1);
                for (var n = 0; n < part3.length; n++) next3.push(part3[n]);
            }
            return next3;
        }
        return walkElement(node, levelIndex + 1);
    }

    var roots = walk(document, 0);
    var objectIds = [];
    for (var i = 0; i < roots.length; i++) {
        var el = roots[i];
        if (!el || !el.setAttribute) continue;
        el.setAttribute('data-cdp-f2b-mark', markPrefix + i);
        objectIds.push(markPrefix + i);
    }
    return objectIds;
})";

        public static IList<CdpElement> FindElements(CdpTab tab, string selectorXml)
        {
            return QueryElements(tab, null, selectorXml, findAll: true);
        }

        public static CdpElement FindElement(CdpTab tab, string selectorXml, int timeoutMs, bool throwException)
        {
            return FindElementInternal(tab, null, selectorXml, timeoutMs, throwException);
        }

        public static CdpElement FindElement(CdpElement root, string selectorXml, int timeoutMs, bool throwException)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            return FindElementInternal(root.Tab, root, selectorXml, timeoutMs, throwException);
        }

        public static CdpElement[] FindElements(CdpElement root, string selectorXml)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            var list = QueryElements(root.Tab, root, selectorXml, findAll: true);
            var array = new CdpElement[list.Count];
            list.CopyTo(array, 0);
            return array;
        }

        public static bool TryFindElement(CdpTab tab, string selectorXml)
        {
            try
            {
                return QueryElements(tab, null, selectorXml, findAll: false).Count > 0;
            }
            catch (BrowserException)
            {
                return false;
            }
        }

        public static bool TryFindElement(CdpFrame frame, string selectorXml)
        {
            try
            {
                return QueryElements(frame, selectorXml, findAll: false).Count > 0;
            }
            catch (BrowserException)
            {
                return false;
            }
        }

        public static CdpElement FindElement(CdpFrame frame, string selectorXml, int timeoutMs, bool throwException)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }

            return FindElementInternal(frame.Tab, null, selectorXml, timeoutMs, throwException, frame);
        }

        public static IList<CdpElement> FindElements(CdpFrame frame, string selectorXml)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }

            return QueryElements(frame, selectorXml, findAll: true);
        }

        public static CdpParallelFindElementResult ParallelFindElement(
            CdpBase root,
            IList<string> selectorXmlList,
            int timeoutMs)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            if (selectorXmlList == null || selectorXmlList.Count == 0)
            {
                return CdpParallelFindElementResult.NotFound();
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            do
            {
                for (var i = 0; i < selectorXmlList.Count; i++)
                {
                    CdpElement element = null;
                    try
                    {
                        element = root.FindElement(selectorXmlList[i], 0, false);
                    }
                    catch (BrowserException)
                    {
                    }

                    if (element != null)
                    {
                        return new CdpParallelFindElementResult(i, element);
                    }
                }

                if (timeoutMs <= 0)
                {
                    break;
                }

                System.Threading.Thread.Sleep(10);
            }
            while (DateTime.UtcNow < deadline);

            return CdpParallelFindElementResult.NotFound();
        }

        public static CdpParallelFindElementResult ParallelFindElement(CdpTab tab, IList<string> selectorXmlList, int timeoutMs)
        {
            if (tab == null)
            {
                throw new ArgumentNullException("tab");
            }

            if (selectorXmlList == null || selectorXmlList.Count == 0)
            {
                return CdpParallelFindElementResult.NotFound();
            }

            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            do
            {
                for (var i = 0; i < selectorXmlList.Count; i++)
                {
                    var element = TryQueryFirstElement(tab, selectorXmlList[i]);
                    if (element != null)
                    {
                        return new CdpParallelFindElementResult(i, element);
                    }
                }

                if (timeoutMs <= 0)
                {
                    break;
                }

                System.Threading.Thread.Sleep(10);
            }
            while (DateTime.UtcNow < deadline);

            return CdpParallelFindElementResult.NotFound();
        }

        private static CdpElement TryQueryFirstElement(CdpTab tab, string selectorXml)
        {
            try
            {
                var elements = QueryElements(tab, null, selectorXml, findAll: false);
                return elements.Count > 0 ? elements[0] : null;
            }
            catch (BrowserException)
            {
                return null;
            }
        }

        private static CdpElement FindElementInternal(
            CdpTab tab,
            CdpElement root,
            string selectorXml,
            int timeoutMs,
            bool throwException,
            CdpFrame frameRoot = null)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, timeoutMs));
            BrowserException lastError = null;

            do
            {
                try
                {
                    IList<CdpElement> elements;
                    if (frameRoot != null)
                    {
                        elements = QueryElements(frameRoot, selectorXml, findAll: false);
                    }
                    else
                    {
                        elements = QueryElements(tab, root, selectorXml, findAll: false);
                    }

                    if (elements.Count > 0)
                    {
                        return elements[0];
                    }
                }
                catch (BrowserException ex)
                {
                    lastError = ex;
                }

                if (timeoutMs <= 0)
                {
                    break;
                }

                System.Threading.Thread.Sleep(100);
            }
            while (DateTime.UtcNow < deadline);

            if (!throwException)
            {
                return null;
            }

            if (lastError != null)
            {
                throw lastError;
            }

            throw new BrowserException(
                timeoutMs <= 0
                    ? "FindElement failed: no matching element."
                    : string.Format("FindElement failed within {0} ms.", timeoutMs));
        }

        private static IList<CdpElement> QueryElements(CdpFrame frame, string selectorXml, bool findAll)
        {
            var scope = SelectorXmlSerializer.SplitScopeForOperation(selectorXml);
            var frameLevels = new List<SelectorLevel>();
            if (frame.FrameLevelsFromTab != null)
            {
                frameLevels.AddRange(frame.FrameLevelsFromTab);
            }

            if (scope.FrameLevels != null && scope.FrameLevels.Count > 0)
            {
                frameLevels.AddRange(scope.FrameLevels);
            }

            if (scope.ElementLevels == null || scope.ElementLevels.Count == 0)
            {
                return new List<CdpElement>();
            }

            if (frameLevels.Count == 0)
            {
                using (var context = frame.CreateDomContext())
                {
                    return QueryElementsInContext(context, scope.ElementLevels, findAll);
                }
            }

            using (var context = frame.Tab.GetSession().CreateDomContext(frameLevels))
            {
                return QueryElementsInContext(context, scope.ElementLevels, findAll);
            }
        }

        private static IList<CdpElement> QueryElements(CdpTab tab, CdpElement root, string selectorXml, bool findAll)
        {
            var scope = SelectorXmlSerializer.SplitScopeForOperation(selectorXml);

            if (root != null)
            {
                return QueryElementsFromRoot(tab.GetSession(), root, scope, findAll);
            }

            if (scope.ElementLevels.Count == 0)
            {
                return new List<CdpElement>();
            }

            using (var context = tab.GetSession().CreateDomContext(scope.FrameLevels))
            {
                return QueryElementsInContext(context, scope.ElementLevels, findAll);
            }
        }

        private static IList<CdpElement> QueryElementsFromRoot(
            CdpTabSession session,
            CdpElement root,
            SelectorScope scope,
            bool findAll)
        {
            var levels = CombineLevels(scope);
            if (levels.Count == 0)
            {
                return new List<CdpElement>();
            }

            root.Context.RefreshIds();
            var levelsJson = SerializeLevels(levels);
            var markPrefix = "f2b-" + Guid.NewGuid().ToString("N") + "-";
            var findAllLiteral = findAll ? "true" : "false";
            var functionDeclaration = BuildElementFinderFunction(levelsJson, findAllLiteral, markPrefix);

            IList<string> marks;
            try
            {
                marks = ReadMarks(RunElementFinder(session, root.ObjectId, functionDeclaration));
            }
            catch
            {
                return new List<CdpElement>();
            }

            var elements = new List<CdpElement>();
            try
            {
                foreach (var mark in marks)
                {
                    var element = ResolveMarkedElement(session, root, mark);
                    if (element != null)
                    {
                        elements.Add(element);
                    }
                }
            }
            finally
            {
                CleanupElementMarks(session, root, markPrefix);
            }

            return elements;
        }

        private static IList<CdpElement> QueryElementsInContext(
            CdpDomContext context,
            IList<SelectorLevel> elementLevels,
            bool findAll)
        {
            var levelsJson = SerializeLevels(elementLevels);
            var markPrefix = "f2b-" + Guid.NewGuid().ToString("N") + "-";
            var findAllLiteral = findAll ? "true" : "false";
            var expression = FinderScript + "(" + levelsJson + ", " + findAllLiteral + ", '" + markPrefix + "')";

            object rawResult;
            try
            {
                rawResult = context.Evaluate(expression);
            }
            catch
            {
                return new List<CdpElement>();
            }

            var marks = ReadMarks(rawResult);
            var elements = new List<CdpElement>();
            try
            {
                foreach (var mark in marks)
                {
                    var objectId = context.EvaluateObjectId(
                        "document.querySelector('[data-cdp-f2b-mark=\"" + mark + "\"]')");

                    var element = context.ResolveElement(objectId);
                    if (element != null)
                    {
                        elements.Add(element);
                    }
                }
            }
            finally
            {
                CleanupMarks(context, markPrefix);
            }

            return elements;
        }

        private static IList<SelectorLevel> CombineLevels(SelectorScope scope)
        {
            var levels = new List<SelectorLevel>();
            if (scope.FrameLevels != null)
            {
                levels.AddRange(scope.FrameLevels);
            }

            if (scope.ElementLevels != null)
            {
                levels.AddRange(scope.ElementLevels);
            }

            return levels;
        }

        private static string BuildElementFinderFunction(string levelsJson, string findAllLiteral, string markPrefix)
        {
            const string wrapperPrefix = "(function(levels, findAll, markPrefix) {";
            const string wrapperSuffix = "})";
            if (!FinderScript.StartsWith(wrapperPrefix, StringComparison.Ordinal) ||
                !FinderScript.EndsWith(wrapperSuffix, StringComparison.Ordinal))
            {
                throw new BrowserException("Finder script format is invalid.");
            }

            var body = FinderScript.Substring(
                wrapperPrefix.Length,
                FinderScript.Length - wrapperPrefix.Length - wrapperSuffix.Length);

            const string documentRoots = "var roots = walk(document, 0);";
            const string elementRoots = "var roots = walkElement(this, 0);";
            var rootsIndex = body.LastIndexOf(documentRoots, StringComparison.Ordinal);
            if (rootsIndex >= 0)
            {
                body = body.Substring(0, rootsIndex) + elementRoots + body.Substring(rootsIndex + documentRoots.Length);
            }

            return "function() { var levels = " + levelsJson + "; var findAll = " + findAllLiteral +
                   "; var markPrefix = '" + markPrefix + "';" + body + "}";
        }

        private static object RunElementFinder(CdpTabSession session, string objectId, string functionDeclaration)
        {
            var response = session.Send("Runtime.callFunctionOn", new Dictionary<string, object>
            {
                { "functionDeclaration", functionDeclaration },
                { "objectId", objectId },
                { "returnByValue", true },
                { "awaitPromise", true },
                { "userGesture", true }
            });

            object exceptionDetails;
            if (response.TryGetValue("exceptionDetails", out exceptionDetails) && exceptionDetails != null)
            {
                throw new BrowserException(string.Format("Element finder failed: {0}", exceptionDetails));
            }

            var inner = CdpValueConverter.GetDictionary(response, "result");
            object value;
            return inner != null && inner.TryGetValue("value", out value) ? value : null;
        }

        private static CdpElement ResolveMarkedElement(CdpTabSession session, CdpElement root, string mark)
        {
            var response = session.Send("Runtime.callFunctionOn", new Dictionary<string, object>
            {
                {
                    "functionDeclaration",
                    "function() { return this.ownerDocument.querySelector('[data-cdp-f2b-mark=\"" + mark + "\"]'); }"
                },
                { "objectId", root.ObjectId },
                { "returnByValue", false }
            });

            var inner = CdpValueConverter.GetDictionary(response, "result");
            var objectId = inner != null ? CdpValueConverter.GetString(inner, "objectId") : null;
            if (string.IsNullOrEmpty(objectId))
            {
                return null;
            }

            var request = session.Send("DOM.requestNode", new Dictionary<string, object>
            {
                { "objectId", objectId }
            });

            var nodeId = CdpValueConverter.GetInt(request, "nodeId");
            var describe = session.Send("DOM.describeNode", new Dictionary<string, object>
            {
                { "nodeId", nodeId }
            });

            var node = CdpValueConverter.GetDictionary(describe, "node");
            return new CdpElement(
                root.Tab,
                CdpValueConverter.GetString(node, "localName") ?? string.Empty,
                CdpValueConverter.GetInt(node, "backendNodeId"),
                nodeId,
                objectId);
        }

        private static void CleanupElementMarks(CdpTabSession session, CdpElement root, string markPrefix)
        {
            try
            {
                session.Send("Runtime.callFunctionOn", new Dictionary<string, object>
                {
                    {
                        "functionDeclaration",
                        "function() { var nodes = this.ownerDocument.querySelectorAll('[data-cdp-f2b-mark^=\"" + markPrefix + "\"]'); for (var i = 0; i < nodes.length; i++) nodes[i].removeAttribute('data-cdp-f2b-mark'); }"
                    },
                    { "objectId", root.ObjectId }
                });
            }
            catch
            {
            }
        }

        private static string SerializeLevels(IList<SelectorLevel> levels)
        {
            var payload = new List<object>();
            foreach (var level in levels)
            {
                if (level == null || !level.IsEnabled)
                {
                    continue;
                }

                var props = new List<object>();
                foreach (var property in level.Properties)
                {
                    if (property == null || !property.IsSelected)
                    {
                        continue;
                    }

                    props.Add(new
                    {
                        name = property.Name != null ? property.Name.ToLowerInvariant() : string.Empty,
                        value = property.Value ?? string.Empty,
                        regex = property.IsRegex
                    });
                }

                payload.Add(new
                {
                    tag = level.TagName != null ? level.TagName.ToLowerInvariant() : "ctrl",
                    props = props
                });
            }

            return new CdpJsonSerializer().Serialize(payload);
        }

        private static IList<string> ReadMarks(object rawResult)
        {
            var marks = new List<string>();
            var array = rawResult as IList;
            if (array == null)
            {
                return marks;
            }

            foreach (var item in array)
            {
                if (item != null)
                {
                    marks.Add(Convert.ToString(item));
                }
            }

            return marks;
        }

        private static void CleanupMarks(CdpDomContext context, string markPrefix)
        {
            try
            {
                context.Evaluate(
                    @"(function(prefix){
                        var nodes = document.querySelectorAll('[data-cdp-f2b-mark^=""' + prefix + '""]');
                        for (var i = 0; i < nodes.length; i++) nodes[i].removeAttribute('data-cdp-f2b-mark');
                    })('" + markPrefix + "')");
            }
            catch
            {
                // Ignore cleanup errors.
            }
        }
    }
}
