using System;
using System.Collections.Generic;
using F2B.Browser.Chromium.Cdp.Browser;
using F2B.Browser.Chromium.Cdp.Selectors;
using Newtonsoft.Json.Linq;

namespace F2B.Browser.Chromium.Cdp.Inspector.Services
{
    /// <summary>
    /// Page-side hover / pick / selector build via Runtime.callFunctionOn (no Chrome extension).
    /// Scripts must be <c>function (...)</c> declarations so CDP arg binding works.
    /// </summary>
    internal static class CdpPagePickAssist
    {
        private const string HoverScript = @"
function(sx, sy) {
  function toClient(sx, sy) {
    var chromeY = Math.max(0, (window.outerHeight || 0) - (window.innerHeight || 0));
    var chromeX = Math.max(0, (window.outerWidth || 0) - (window.innerWidth || 0));
    return {
      x: sx - (window.screenX || 0) - Math.floor(chromeX / 2),
      y: sy - (window.screenY || 0) - chromeY
    };
  }
  function deepElementFromPoint(doc, x, y) {
    var el = doc.elementFromPoint(x, y);
    if (!el) return null;
    if (el.tagName === 'IFRAME' || el.tagName === 'FRAME') {
      try {
        var rect = el.getBoundingClientRect();
        var child = deepElementFromPoint(el.contentDocument, x - rect.left, y - rect.top);
        if (child) return child;
      } catch (e) { }
    }
    return el;
  }
  function ensureOverlay() {
    var id = '__f2b_cdp_inspector_hover__';
    var box = document.getElementById(id);
    if (!box) {
      box = document.createElement('div');
      box.id = id;
      box.style.cssText = 'position:fixed;pointer-events:none;z-index:2147483647;border:2px solid #ff9800;background:rgba(33,150,243,0.18);box-sizing:border-box;';
      document.documentElement.appendChild(box);
    }
    return box;
  }
  var pt = toClient(sx, sy);
  if (pt.x < 0 || pt.y < 0 || pt.x > window.innerWidth || pt.y > window.innerHeight) {
    var stale = document.getElementById('__f2b_cdp_inspector_hover__');
    if (stale) stale.style.display = 'none';
    return JSON.stringify({ hit: false });
  }
  var el = deepElementFromPoint(document, pt.x, pt.y);
  if (!el) {
    var stale2 = document.getElementById('__f2b_cdp_inspector_hover__');
    if (stale2) stale2.style.display = 'none';
    return JSON.stringify({ hit: false });
  }
  var r = el.getBoundingClientRect();
  var box = ensureOverlay();
  box.style.display = 'block';
  box.style.left = r.left + 'px';
  box.style.top = r.top + 'px';
  box.style.width = Math.max(0, r.width) + 'px';
  box.style.height = Math.max(0, r.height) + 'px';
  return JSON.stringify({
    hit: true,
    tag: (el.tagName || '').toLowerCase(),
    id: el.id || '',
    text: ((el.innerText || el.textContent || '') + '').trim().slice(0, 80)
  });
}";

        private const string ClearHoverScript = @"
function() {
  var box = document.getElementById('__f2b_cdp_inspector_hover__');
  if (box) box.remove();
  var hi = document.getElementById('__f2b_cdp_inspector_highlight__');
  if (hi) hi.remove();
  return true;
}";

        private const string PickAndBuildScript = @"
function(sx, sy, browserName, port) {
  function toClient(sx, sy) {
    var chromeY = Math.max(0, (window.outerHeight || 0) - (window.innerHeight || 0));
    var chromeX = Math.max(0, (window.outerWidth || 0) - (window.innerWidth || 0));
    return {
      x: sx - (window.screenX || 0) - Math.floor(chromeX / 2),
      y: sy - (window.screenY || 0) - chromeY
    };
  }
  function deepElementFromPoint(doc, x, y, path) {
    var el = doc.elementFromPoint(x, y);
    if (!el) return { el: null, path: path };
    if (el.tagName === 'IFRAME' || el.tagName === 'FRAME') {
      path.push(el);
      try {
        var rect = el.getBoundingClientRect();
        return deepElementFromPoint(el.contentDocument, x - rect.left, y - rect.top, path);
      } catch (e) {
        return { el: el, path: path };
      }
    }
    return { el: el, path: path };
  }
  function cssEscape(v) {
    if (window.CSS && CSS.escape) return CSS.escape(v);
    return String(v).replace(/[^a-zA-Z0-9_-]/g, '\\$&');
  }
  function attr(el, name) {
    if (!el || !el.getAttribute) return '';
    return el.getAttribute(name) || '';
  }
  function buildCss(el) {
    if (!el || el.nodeType !== 1) return '';
    if (el.id) return '#' + cssEscape(el.id);
    var parts = [];
    var cur = el;
    while (cur && cur.nodeType === 1 && parts.length < 5) {
      var part = cur.tagName.toLowerCase();
      if (cur.id) {
        parts.unshift('#' + cssEscape(cur.id));
        break;
      }
      var parent = cur.parentElement;
      if (parent) {
        var siblings = Array.prototype.filter.call(parent.children, function(c) { return c.tagName === cur.tagName; });
        if (siblings.length > 1) {
          part += ':nth-of-type(' + (siblings.indexOf(cur) + 1) + ')';
        }
      }
      parts.unshift(part);
      cur = parent;
      if (cur && (cur.tagName === 'BODY' || cur.tagName === 'HTML')) break;
    }
    return parts.join(' > ');
  }
  function indexInParent(el) {
    if (!el || !el.parentElement) return 0;
    var siblings = Array.prototype.filter.call(el.parentElement.children, function(c) { return c.tagName === el.tagName; });
    return Math.max(0, siblings.indexOf(el));
  }
  function prop(name, value, selected) {
    return { name: name, value: value == null ? '' : String(value), isSelected: !!selected, isRegex: false };
  }
  function countMatches(selector) {
    try { return document.querySelectorAll(selector).length; } catch (e) { return 0; }
  }
  function ctrlLevelFull(el) {
    var props = [];
    var tag = (el.tagName || '').toLowerCase();
    props.push(prop('tag', tag, true));
    var id = el.id || '';
    if (id) props.push(prop('id', id, true));
    var classes = (el.className && typeof el.className === 'string')
      ? el.className.trim().split(/\s+/).filter(Boolean) : [];
    // Finder matches a single class token — never emit space-joined class strings.
    if (classes.length === 1) props.push(prop('class', classes[0], false));
    else if (classes.length > 1) {
      for (var ci = 0; ci < classes.length; ci++) props.push(prop('class', classes[ci], false));
    }
    var name = attr(el, 'name');
    if (name) props.push(prop('name', name, false));
    var type = attr(el, 'type');
    if (type) props.push(prop('type', type, false));
    var role = attr(el, 'role');
    if (role) props.push(prop('role', role, false));
    var title = attr(el, 'title');
    if (title) props.push(prop('title', title, false));
    var href = attr(el, 'href');
    if (href) props.push(prop('href', href, false));
    // Boolean HTML attrs: <button disabled> => getAttribute returns "".
    if ((el.hasAttribute && el.hasAttribute('disabled')) || el.disabled) {
      props.push(prop('disabled', 'true', false));
    }
    if ((el.hasAttribute && el.hasAttribute('readonly')) || el.readOnly) {
      props.push(prop('readonly', 'true', false));
    }
    var text = ((el.innerText || '').trim()).slice(0, 120);
    if (text && text.length <= 60) props.push(prop('text', text, false));
    props.push(prop('idx', String(indexInParent(el)), false));
    var css = buildCss(el);
    if (css) props.push(prop('css-selector', css, false));
    return { tagName: 'ctrl', isEnabled: true, canDisable: true, properties: props };
  }
  function minimalCtrlLevel(el) {
    var tag = (el.tagName || '').toLowerCase();
    var id = el.id || '';
    if (id && countMatches('#' + cssEscape(id)) === 1) {
      return { tagName: 'ctrl', isEnabled: true, canDisable: true, properties: [
        prop('tag', tag, true), prop('id', id, true)
      ]};
    }
    var name = attr(el, 'name');
    if (name) {
      var named = 0;
      var nodes = document.getElementsByTagName(tag);
      for (var ni = 0; ni < nodes.length; ni++) {
        if ((nodes[ni].getAttribute('name') || '') === name) named++;
      }
      if (named === 1) {
        return { tagName: 'ctrl', isEnabled: true, canDisable: true, properties: [
          prop('tag', tag, true), prop('name', name, true)
        ]};
      }
    }
    var classes = (el.className && typeof el.className === 'string')
      ? el.className.trim().split(/\s+/).filter(Boolean) : [];
    for (var i = 0; i < classes.length; i++) {
      var c = classes[i];
      if (countMatches(tag + '.' + cssEscape(c)) === 1) {
        return { tagName: 'ctrl', isEnabled: true, canDisable: true, properties: [
          prop('tag', tag, true), prop('class', c, true)
        ]};
      }
    }
    var css = buildCss(el);
    if (css && countMatches(css) === 1) {
      return { tagName: 'ctrl', isEnabled: true, canDisable: true, properties: [
        prop('tag', tag, true), prop('css-selector', css, true)
      ]};
    }
    return { tagName: 'ctrl', isEnabled: true, canDisable: true, properties: [
      prop('tag', tag, true), prop('idx', String(indexInParent(el)), true)
    ]};
  }
  function frmLevelFull(frameEl) {
    var props = [];
    props.push(prop('tag', 'iframe', true));
    var id = frameEl.id || '';
    if (id) props.push(prop('id', id, true));
    var name = attr(frameEl, 'name');
    if (name) props.push(prop('name', name, false));
    var src = attr(frameEl, 'src');
    if (src) props.push(prop('src', src, false));
    props.push(prop('idx', String(indexInParent(frameEl)), false));
    return { tagName: 'frm', isEnabled: true, canDisable: true, properties: props };
  }
  function minimalFrmLevel(frameEl) {
    var id = frameEl.id || '';
    if (id) {
      return { tagName: 'frm', isEnabled: true, canDisable: true, properties: [
        prop('tag', 'iframe', true), prop('id', id, true)
      ]};
    }
    var name = attr(frameEl, 'name');
    if (name) {
      return { tagName: 'frm', isEnabled: true, canDisable: true, properties: [
        prop('tag', 'iframe', true), prop('name', name, true)
      ]};
    }
    return { tagName: 'frm', isEnabled: true, canDisable: true, properties: [
      prop('tag', 'iframe', true), prop('idx', String(indexInParent(frameEl)), true)
    ]};
  }
  function wndLevelFull(browserName, port) {
    var title = document.title || '';
    var url = location.href || '';
    return {
      tagName: 'wnd',
      isEnabled: true,
      canDisable: false,
      properties: [
        prop('title', title, true),
        prop('url', url, false),
        prop('browser', browserName || 'chrome', true),
        prop('port', String(port || ''), false),
        prop('idx', '0', false)
      ]
    };
  }
  function minimalWndLevel(browserName) {
    var title = document.title || '';
    return {
      tagName: 'wnd',
      isEnabled: true,
      canDisable: false,
      properties: [
        prop('title', title, true),
        prop('browser', browserName || 'chrome', true)
      ]
    };
  }

  var pt = toClient(sx, sy);
  if (pt.x < 0 || pt.y < 0 || pt.x > window.innerWidth || pt.y > window.innerHeight) {
    return JSON.stringify({ cancelled: true });
  }
  var hit = deepElementFromPoint(document, pt.x, pt.y, []);
  var el = hit.el;
  if (!el) return JSON.stringify({ cancelled: true });

  var frames = hit.path || [];
  var full = [wndLevelFull(browserName, port)];
  for (var i = 0; i < frames.length; i++) full.push(frmLevelFull(frames[i]));
  full.push(ctrlLevelFull(el));

  var minimal = [minimalWndLevel(browserName)];
  for (var j = 0; j < frames.length; j++) minimal.push(minimalFrmLevel(frames[j]));
  minimal.push(minimalCtrlLevel(el));

  var display = (el.tagName || '').toLowerCase();
  if (el.id) display += '#' + el.id;
  var t = ((el.innerText || '').trim()).slice(0, 40);
  if (t) display += ' ' + t;

  return JSON.stringify({
    cancelled: false,
    displayName: display,
    levels: full,
    minimalLevels: minimal
  });
}";

        private const string HighlightScript = @"
function(durationMs) {
  var existing = document.getElementById('__f2b_cdp_inspector_highlight__');
  if (existing) existing.remove();
  var el = window.__f2b_cdp_inspector_last_match__;
  if (!el || !el.getBoundingClientRect) return false;
  var r = el.getBoundingClientRect();
  var box = document.createElement('div');
  box.id = '__f2b_cdp_inspector_highlight__';
  box.style.cssText = 'position:fixed;pointer-events:none;z-index:2147483647;border:3px solid #f44336;background:rgba(244,67,54,0.15);box-sizing:border-box;';
  box.style.left = r.left + 'px';
  box.style.top = r.top + 'px';
  box.style.width = Math.max(0, r.width) + 'px';
  box.style.height = Math.max(0, r.height) + 'px';
  document.documentElement.appendChild(box);
  setTimeout(function() { if (box.parentNode) box.parentNode.removeChild(box); }, durationMs || 3000);
  return true;
}";

        public static bool TryHover(CdpTab tab, int screenX, int screenY, out string displayHint)
        {
            displayHint = null;
            if (tab == null)
            {
                return false;
            }

            try
            {
                var raw = tab.RunJs(HoverScript, new object[] { screenX, screenY }, false, false, 3000);
                var json = Convert.ToString(raw);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return false;
                }

                var obj = JObject.Parse(json);
                if (obj.Value<bool?>("hit") != true)
                {
                    return false;
                }

                displayHint = (obj.Value<string>("tag") ?? string.Empty)
                    + (string.IsNullOrEmpty(obj.Value<string>("id")) ? string.Empty : "#" + obj.Value<string>("id"));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void ClearHover(CdpTab tab)
        {
            if (tab == null)
            {
                return;
            }

            try
            {
                tab.RunJs(ClearHoverScript, null, false, false, 2000);
            }
            catch
            {
            }
        }

        public static CdpIndicatePickResult PickAndBuild(
            CdpTab tab,
            int screenX,
            int screenY,
            string browserName,
            int port)
        {
            if (tab == null)
            {
                return new CdpIndicatePickResult { Cancelled = true };
            }

            var raw = tab.RunJs(
                PickAndBuildScript,
                new object[] { screenX, screenY, browserName ?? "chrome", port },
                false,
                false,
                15000);
            var json = Convert.ToString(raw);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new CdpIndicatePickResult { Cancelled = true };
            }

            var obj = JObject.Parse(json);
            if (obj.Value<bool?>("cancelled") == true)
            {
                return new CdpIndicatePickResult { Cancelled = true };
            }

            return new CdpIndicatePickResult
            {
                Cancelled = false,
                DisplayName = obj.Value<string>("displayName") ?? string.Empty,
                Levels = ParseLevels(obj["levels"] as JArray),
                MinimalLevels = ParseLevels(obj["minimalLevels"] as JArray)
            };
        }

        public static void HighlightMatchedElement(CdpTab tab, CdpElement element, int durationMs)
        {
            if (tab == null || element == null)
            {
                return;
            }

            try
            {
                element.RunJs("window.__f2b_cdp_inspector_last_match__ = this; return true;", null, false, false, 3000);
                tab.RunJs(HighlightScript, new object[] { durationMs }, false, false, 3000);
            }
            catch
            {
            }
        }

        private static IList<SelectorLevel> ParseLevels(JArray array)
        {
            var levels = new List<SelectorLevel>();
            if (array == null)
            {
                return levels;
            }

            foreach (var token in array)
            {
                var obj = token as JObject;
                if (obj == null)
                {
                    continue;
                }

                var level = new SelectorLevel(obj.Value<string>("tagName") ?? "ctrl")
                {
                    IsEnabled = obj.Value<bool?>("isEnabled") ?? true,
                    CanDisable = obj.Value<bool?>("canDisable") ?? true
                };

                var props = obj["properties"] as JArray;
                if (props != null)
                {
                    foreach (var propToken in props)
                    {
                        var propObj = propToken as JObject;
                        if (propObj == null)
                        {
                            continue;
                        }

                        level.Properties.Add(new SelectorProperty
                        {
                            Name = propObj.Value<string>("name") ?? string.Empty,
                            Value = propObj.Value<string>("value") ?? string.Empty,
                            IsSelected = propObj.Value<bool?>("isSelected") ?? true,
                            IsRegex = propObj.Value<bool?>("isRegex") ?? false
                        });
                    }
                }

                levels.Add(level);
            }

            return levels;
        }
    }
}
