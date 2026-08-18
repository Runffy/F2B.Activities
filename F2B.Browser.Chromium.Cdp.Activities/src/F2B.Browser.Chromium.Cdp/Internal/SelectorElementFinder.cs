using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Exceptions;
using F2B.Browser.Chromium.Cdp.Selectors;

namespace F2B.Browser.Chromium.Cdp.Internal
{
    internal static class SelectorElementFinder
    {
        private const string FinderScript =
            @"(function(levels, findAll, markPrefix, directFirstLevel) {
    directFirstLevel = !!directFirstLevel;
    function getProp(level, name) {
        if (!level || !level.props) return null;
        var target = (name || '').toLowerCase();
        for (var i = 0; i < level.props.length; i++) {
            if ((level.props[i].name || '').toLowerCase() === target) return level.props[i];
        }
        return null;
    }

    function getDirectElementText(el) {
        if (!el || !el.childNodes) return '';
        var text = '';
        for (var i = 0; i < el.childNodes.length; i++) {
            var node = el.childNodes[i];
            if (node && node.nodeType === 3) text += node.textContent || '';
        }
        return normalizeText(text);
    }

    function getInnerText(el) {
        if (!el) return '';
        return normalizeText((el.innerText || el.textContent || '') + '');
    }

    function getAaName(el) {
        if (!el || el.nodeType !== 1) return '';
        var aria = el.getAttribute ? (el.getAttribute('aria-label') || '') : '';
        if (normalizeText(aria)) return normalizeText(aria);
        if (el.title && normalizeText(el.title)) return normalizeText(el.title);
        var placeholder = el.getAttribute ? (el.getAttribute('placeholder') || '') : '';
        if (normalizeText(placeholder)) return normalizeText(placeholder);
        // Fall back to visible aggregated text (same idea as Bridge getElementName).
        return getInnerText(el);
    }

    function normalizeText(value) {
        return String(value == null ? '' : value).replace(/\s+/g, ' ').trim();
    }

    function readValue(el, name) {
        if (!el || el.nodeType !== 1) return '';
        var key = (name || '').toLowerCase();
        if (key === 'tag') return (el.tagName || '').toLowerCase();
        if (key === 'text') return getDirectElementText(el);
        if (key === 'innertext') return getInnerText(el);
        if (key === 'aaname') return getAaName(el);
        if (key === 'class') {
            var cn = el.className;
            // SVGElement.className is SVGAnimatedString, not a string.
            if (cn && typeof cn === 'object' && cn.baseVal != null) return String(cn.baseVal);
            return cn || '';
        }
        // Boolean HTML attributes: <button disabled> => getAttribute returns "".
        if (key === 'disabled' || key === 'checked' || key === 'selected' ||
            key === 'readonly' || key === 'required' || key === 'multiple') {
            var present = false;
            if (el.hasAttribute && el.hasAttribute(name)) present = true;
            else if (key === 'disabled' && el.disabled) present = true;
            else if (key === 'checked' && el.checked) present = true;
            else if (key === 'selected' && el.selected) present = true;
            else if (key === 'readonly' && el.readOnly) present = true;
            else if (key === 'required' && el.required) present = true;
            else if (key === 'multiple' && el.multiple) present = true;
            if (!present) return 'false';
            var raw = el.getAttribute ? el.getAttribute(name) : null;
            if (raw == null || raw === '' || String(raw).toLowerCase() === key) return 'true';
            return String(raw);
        }
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
        var negated = prop && (prop.negate === true || prop.negate === 1 || prop.negate === 'true');
        // -ne / -nre: keep when the positive match fails (empty actual also passes for -ne).
        if (negated) {
            return !matchValuePositive(actual, prop);
        }
        return matchValuePositive(actual, prop);
    }

    function matchValuePositive(actual, prop) {
        actual = actual == null ? '' : String(actual);
        var expected = prop.value == null ? '' : String(prop.value);
        var propName = (prop.name || '').toLowerCase();
        if (prop.regex) {
            try {
                return new RegExp(expected).test(actual);
            } catch (e) {
                // Invalid pattern cannot match ? positive false ? -nre would keep the element.
                return false;
            }
        }
        if (propName === 'class') {
            if (expected === '') return actual === '';
            var classes = actual.split(/\s+/);
            for (var i = 0; i < classes.length; i++) {
                if (classes[i] && classes[i].toLowerCase() === expected.toLowerCase()) return true;
            }
            return false;
        }
        if (propName === 'disabled' || propName === 'checked' || propName === 'selected' ||
            propName === 'readonly' || propName === 'required' || propName === 'multiple') {
            var exp = expected.toLowerCase();
            var wantTrue = (exp === '' || exp === 'true' || exp === '1' || exp === propName);
            var wantFalse = (exp === 'false' || exp === '0');
            var isTrue = actual.toLowerCase() === 'true';
            if (wantTrue) return isTrue;
            if (wantFalse) return !isTrue;
        }
        if (propName === 'text' || propName === 'innertext' || propName === 'aaname') {
            return normalizeText(actual).toLowerCase() === normalizeText(expected).toLowerCase();
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

    // Among matches that share aggregated text, keep the deepest (non-ancestor) nodes so
    // text-only selectors hit the leaf, and <parent level='1'/> climbs from that leaf.
    function preferDeepest(matched) {
        if (!matched || matched.length <= 1) return matched || [];
        var deepest = [];
        for (var i = 0; i < matched.length; i++) {
            var candidate = matched[i];
            if (!candidate) continue;
            var isAncestorOfOther = false;
            for (var j = 0; j < matched.length; j++) {
                if (i === j || !matched[j]) continue;
                try {
                    if (candidate.contains && candidate.contains(matched[j])) {
                        isAncestorOfOther = true;
                        break;
                    }
                } catch (e) {}
            }
            if (!isAncestorOfOther) deepest.push(candidate);
        }
        return deepest.length > 0 ? deepest : matched;
    }

    function getSearchRoot(root) {
        if (!root) return null;
        if (root.nodeType === 9) return root.body || root.documentElement;
        if (root.nodeType === 1) return root;
        return null;
    }

    function narrowMatches(matched, level, levelIndex) {
        matched = preferDeepest(matched);
        var idxProp = getProp(level, 'idx');
        if (idxProp && idxProp.value !== '' && idxProp.value != null) {
            var idx = parseInt(idxProp.value, 10);
            if (isNaN(idx) || idx < 0 || idx >= matched.length) return [];
            return [matched[idx]];
        }
        var isLast = levelIndex >= levels.length - 1;
        // Intermediate levels must keep the full candidate set so chains like
        // <ctrl tag='td'/><parent level='1'/> can produce every row <tr>.
        if (!isLast) {
            return matched;
        }
        if (!findAll) {
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

    function applyCtrlOnDirectChildren(root, level, levelIndex) {
        var searchRoot = getSearchRoot(root);
        if (!searchRoot || !searchRoot.children) return [];
        var matched = [];
        var children = searchRoot.children;
        for (var i = 0; i < children.length; i++) {
            if (matchElement(children[i], level)) matched.push(children[i]);
        }
        return narrowMatches(matched, level, levelIndex);
    }

    function applyParent(nodes, level) {
        var prop = getProp(level, 'level');
        var count = prop && prop.value ? parseInt(prop.value, 10) : 1;
        if (isNaN(count) || count < 1) count = 1;
        var result = [];
        var seen = typeof Set !== 'undefined' ? new Set() : null;
        for (var i = 0; i < nodes.length; i++) {
            var node = nodes[i];
            for (var j = 0; j < count && node; j++) node = node.parentElement;
            if (!node) continue;
            if (seen) {
                if (seen.has(node)) continue;
                seen.add(node);
            } else {
                var dup = false;
                for (var s = 0; s < result.length; s++) {
                    if (result[s] === node) { dup = true; break; }
                }
                if (dup) continue;
            }
            result.push(node);
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
            // Terminal <frm> ??host iframe/frame elements (needed by FindFrame / AsFrame).
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
            // Terminal <frm> under an Element root ??host iframe (not contentDocument body).
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
            var ctrlMatches = (directFirstLevel && levelIndex === 0)
                ? applyCtrlOnDirectChildren(node, level, levelIndex)
                : applyCtrlOnRoot(node, level, levelIndex);
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
    // Return DOM node(s) directly so CDP can resolve objectId(s).
    // Prefer this over DOM attribute marks (marks fail for some parent/SVG hosts).
    if (!findAll) {
        return roots.length > 0 ? roots[0] : null;
    }
    return roots;
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

        /// <summary>
        /// Find matching elements under <paramref name="root"/> using Bridge GetChildren semantics:
        /// the first ctrl level matches direct children only; subsequent levels search descendants.
        /// </summary>
        public static CdpElement[] FindChildElements(CdpElement root, string selectorXml)
        {
            if (root == null)
            {
                throw new ArgumentNullException("root");
            }

            var scope = SelectorXmlSerializer.SplitScopeForOperation(selectorXml);
            var levels = CombineLevels(scope);
            if (levels.Count == 0)
            {
                return new CdpElement[0];
            }

            root.Context.RefreshIds();
            var levelsJson = SerializeLevels(levels);
            var functionDeclaration = BuildElementFinderFunction(
                levelsJson,
                "true",
                markPrefix: string.Empty,
                directFirstLevel: true);

            try
            {
                var arrayObjectId = RunElementFinderObjectId(
                    root.Tab.GetSession(),
                    root.ObjectId,
                    functionDeclaration);
                return ResolveElementsFromArrayObjectId(root.Tab.GetSession(), root.Tab, arrayObjectId)
                    .ToArray();
            }
            catch
            {
                return new CdpElement[0];
            }
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
                catch (BrowserException)
                {
                    // Transient CDP/JS/resolve failures are treated as "not found yet"
                    // and retried until timeout (aligned with Bridge FindElement semantics).
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

            try
            {
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
            catch (BrowserException)
            {
                return new List<CdpElement>();
            }
        }

        private static IList<CdpElement> QueryElements(CdpTab tab, CdpElement root, string selectorXml, bool findAll)
        {
            var scope = SelectorXmlSerializer.SplitScopeForOperation(selectorXml);

            try
            {
                if (root != null)
                {
                    return QueryElementsFromRoot(tab.GetSession(), root, scope, findAll);
                }

                if (scope.ElementLevels == null || scope.ElementLevels.Count == 0)
                {
                    return new List<CdpElement>();
                }

                using (var context = tab.GetSession().CreateDomContext(scope.FrameLevels))
                {
                    return QueryElementsInContext(context, scope.ElementLevels, findAll);
                }
            }
            catch (BrowserException)
            {
                return new List<CdpElement>();
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

            try
            {
                root.Context.RefreshIds();
                var levelsJson = SerializeLevels(levels);
                var findAllLiteral = findAll ? "true" : "false";
                var functionDeclaration = BuildElementFinderFunction(levelsJson, findAllLiteral, markPrefix: string.Empty);
                var objectId = RunElementFinderObjectId(session, root.ObjectId, functionDeclaration);
                if (!findAll)
                {
                    var element = ResolveElementByObjectId(session, root.Tab, objectId);
                    if (element == null)
                    {
                        return new List<CdpElement>();
                    }

                    return new List<CdpElement> { element };
                }

                return ResolveElementsFromArrayObjectId(session, root.Tab, objectId);
            }
            catch (BrowserException)
            {
                return new List<CdpElement>();
            }
        }

        private static IList<CdpElement> QueryElementsInContext(
            CdpDomContext context,
            IList<SelectorLevel> elementLevels,
            bool findAll)
        {
            var levelsJson = SerializeLevels(elementLevels);
            var findAllLiteral = findAll ? "true" : "false";
            // markPrefix kept for FinderScript arity compatibility; unused since we return nodes directly.
            var expression = FinderScript + "(" + levelsJson + ", " + findAllLiteral + ", '')";

            try
            {
                var objectId = context.EvaluateObjectId(expression);
                if (!findAll)
                {
                    var element = context.ResolveElement(objectId);
                    if (element == null)
                    {
                        return new List<CdpElement>();
                    }

                    return new List<CdpElement> { element };
                }

                return ResolveElementsFromArrayObjectId(context, objectId);
            }
            catch (BrowserException)
            {
                return new List<CdpElement>();
            }
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

        private static string BuildElementFinderFunction(
            string levelsJson,
            string findAllLiteral,
            string markPrefix,
            bool directFirstLevel = false)
        {
            const string wrapperPrefix = "(function(levels, findAll, markPrefix, directFirstLevel) {";
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
                   "; var markPrefix = '" + markPrefix + "'; var directFirstLevel = " +
                   (directFirstLevel ? "true" : "false") + ";" + body + "}";
        }

        private static string RunElementFinderObjectId(
            CdpTabSession session,
            string objectId,
            string functionDeclaration)
        {
            var response = session.Send("Runtime.callFunctionOn", new Dictionary<string, object>
            {
                { "functionDeclaration", functionDeclaration },
                { "objectId", objectId },
                { "returnByValue", false },
                { "awaitPromise", true },
                { "userGesture", true }
            });

            object exceptionDetails;
            if (response.TryGetValue("exceptionDetails", out exceptionDetails) && exceptionDetails != null)
            {
                throw new BrowserException(
                    string.Format(
                        "Element finder failed: {0}",
                        CdpErrorFormatter.FormatExceptionDetails(exceptionDetails)));
            }

            var inner = CdpValueConverter.GetDictionary(response, "result");
            return inner != null ? CdpValueConverter.GetString(inner, "objectId") : null;
        }

        private static CdpElement ResolveElementByObjectId(CdpTabSession session, CdpTab tab, string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return null;
            }

            try
            {
                session.Send("DOM.getDocument", new Dictionary<string, object> { { "depth", 0 } });
            }
            catch
            {
            }

            Dictionary<string, object> describe;
            try
            {
                describe = session.Send("DOM.describeNode", new Dictionary<string, object>
                {
                    { "objectId", objectId }
                });
            }
            catch (BrowserException)
            {
                var request = session.Send("DOM.requestNode", new Dictionary<string, object>
                {
                    { "objectId", objectId }
                });

                var nodeId = CdpValueConverter.GetInt(request, "nodeId");
                describe = session.Send("DOM.describeNode", new Dictionary<string, object>
                {
                    { "nodeId", nodeId }
                });
            }

            var node = CdpValueConverter.GetDictionary(describe, "node");
            if (node == null)
            {
                return null;
            }

            return new CdpElement(
                tab,
                CdpValueConverter.GetString(node, "localName") ?? string.Empty,
                CdpValueConverter.GetInt(node, "backendNodeId"),
                CdpValueConverter.GetInt(node, "nodeId"),
                objectId);
        }

        private static IList<CdpElement> ResolveElementsFromArrayObjectId(
            CdpTabSession session,
            CdpTab tab,
            string arrayObjectId)
        {
            if (string.IsNullOrEmpty(arrayObjectId))
            {
                return new List<CdpElement>();
            }

            try
            {
                var propsResponse = session.Send("Runtime.getProperties", new Dictionary<string, object>
                {
                    { "objectId", arrayObjectId },
                    { "ownProperties", true }
                });

                return ResolveIndexedElements(
                    CdpValueConverter.GetList(propsResponse, "result"),
                    objectId => ResolveElementByObjectId(session, tab, objectId));
            }
            finally
            {
                ReleaseRemoteObject(session, arrayObjectId);
            }
        }

        private static IList<CdpElement> ResolveElementsFromArrayObjectId(
            CdpDomContext context,
            string arrayObjectId)
        {
            if (string.IsNullOrEmpty(arrayObjectId))
            {
                return new List<CdpElement>();
            }

            try
            {
                var propsResponse = context.Send("Runtime.getProperties", new Dictionary<string, object>
                {
                    { "objectId", arrayObjectId },
                    { "ownProperties", true }
                });

                return ResolveIndexedElements(
                    CdpValueConverter.GetList(propsResponse, "result"),
                    objectId => context.ResolveElement(objectId));
            }
            finally
            {
                ReleaseRemoteObject(context, arrayObjectId);
            }
        }

        private static IList<CdpElement> ResolveIndexedElements(
            IList props,
            Func<string, CdpElement> resolve)
        {
            var indexed = new List<KeyValuePair<int, CdpElement>>();
            if (props == null)
            {
                return new List<CdpElement>();
            }

            foreach (var propEntry in props)
            {
                var prop = propEntry as Dictionary<string, object>;
                if (prop == null)
                {
                    continue;
                }

                var name = CdpValueConverter.GetString(prop, "name");
                if (string.IsNullOrEmpty(name) || name == "length")
                {
                    continue;
                }

                int index;
                if (!int.TryParse(name, out index))
                {
                    continue;
                }

                var value = CdpValueConverter.GetDictionary(prop, "value");
                var objectId = value != null ? CdpValueConverter.GetString(value, "objectId") : null;
                if (string.IsNullOrEmpty(objectId))
                {
                    continue;
                }

                var element = resolve(objectId);
                if (element != null)
                {
                    indexed.Add(new KeyValuePair<int, CdpElement>(index, element));
                }
            }

            indexed.Sort((left, right) => left.Key.CompareTo(right.Key));
            return indexed.Select(pair => pair.Value).ToList();
        }

        private static void ReleaseRemoteObject(CdpTabSession session, string objectId)
        {
            try
            {
                session.Send("Runtime.releaseObject", new Dictionary<string, object>
                {
                    { "objectId", objectId }
                });
            }
            catch
            {
            }
        }

        private static void ReleaseRemoteObject(CdpDomContext context, string objectId)
        {
            try
            {
                context.Send("Runtime.releaseObject", new Dictionary<string, object>
                {
                    { "objectId", objectId }
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

                    props.Add(new Dictionary<string, object>
                    {
                        { "name", property.Name != null ? property.Name.ToLowerInvariant() : string.Empty },
                        { "value", property.Value ?? string.Empty },
                        { "regex", property.IsRegex },
                        { "negate", property.IsNegated }
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
    }
}
