/* global globalThis */
(function (root) {
  const REGEX_SUFFIX = '-re';

  const PROPERTY_ATTR_MAP = {
    ControlType: 'role',
    Title: 'title',
    Name: 'name',
    AutomationId: 'automationid',
    ClassName: 'cls',
    FrameworkId: 'framework',
    ProcessName: 'app',
    FileName: 'filename',
    IndexInParent: 'idx',
    Url: 'url',
    TagName: 'tag',
    Type: 'type',
    Href: 'href',
    Id: 'id',
    Placeholder: 'placeholder',
    HtmlTitle: 'html-title',
    Rows: 'rows',
    Cols: 'cols',
    Autocomplete: 'autocomplete',
    Style: 'style',
    Value: 'value',
    Alt: 'alt',
    Src: 'src',
    AriaLabel: 'aria-label',
    TabIndex: 'tabindex',
    Disabled: 'disabled',
    ReadOnly: 'readonly'
  };

  const PROPERTY_TO_ATTR = {
    Id: 'id',
    Placeholder: 'placeholder',
    HtmlTitle: 'html-title',
    Rows: 'rows',
    Cols: 'cols',
    Autocomplete: 'autocomplete',
    Style: 'style',
    Value: 'value',
    Alt: 'alt',
    Src: 'src',
    AriaLabel: 'aria-label',
    TabIndex: 'tabindex',
    Disabled: 'disabled',
    ReadOnly: 'readonly',
    ClassName: 'class'
  };

  function resolveAttributeName(propertyName) {
    if (PROPERTY_TO_ATTR[propertyName]) {
      return PROPERTY_TO_ATTR[propertyName];
    }

    return propertyName;
  }

  function matchHtmlAttributeProperty(element, propertyName, expected, compare) {
    const attrName = resolveAttributeName(propertyName);
    if (!element.hasAttribute(attrName)) {
      return false;
    }

    return compare(element.getAttribute(attrName) || '');
  }

  function pageTrace(step) {
    if (typeof globalThis.__f2bPageTrace === 'function') {
      globalThis.__f2bPageTrace(step);
    }
  }

  function escapeRegex(value) {
    return String(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  function parseRole(role) {
    if (!role) {
      return role;
    }

    return String(role)
      .split(/\s+/)
      .filter(Boolean)
      .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
      .join('');
  }

  function unescapeValue(value) {
    return String(value || '').replace(/&apos;/g, "'");
  }

  function parseSelectorLevels(levels) {
    if (Array.isArray(levels)) {
      return levels.map(normalizeLevel).filter(Boolean);
    }

    return [];
  }

  function normalizeLevel(level) {
    if (!level) {
      return null;
    }

    return {
      tagName: String(level.tagName || 'ctrl').toLowerCase(),
      properties: Array.isArray(level.properties)
        ? level.properties.map((property) => ({
            name: property.name,
            value: property.value || '',
            isRegex: !!property.isRegex,
            isSelected: property.isSelected !== false
          }))
        : []
    };
  }

  function getElementName(element) {
    const ariaLabel = element.getAttribute('aria-label');
    if (ariaLabel) {
      return ariaLabel.trim();
    }

    const title = element.getAttribute('title');
    if (title) {
      return title.trim();
    }

    const placeholder = element.getAttribute('placeholder');
    if (placeholder) {
      return placeholder.trim();
    }

    const text = (element.innerText || element.textContent || '').trim();
    return text;
  }

  function getAutomationId(element) {
    if (element.id) {
      return element.id;
    }

    return (
      element.getAttribute('data-automation-id') ||
      element.getAttribute('data-automationid') ||
      element.getAttribute('automationid') ||
      element.getAttribute('name') ||
      ''
    );
  }

  function normalizeRoleToken(role) {
    return String(role || '').replace(/\s+/g, '').toLowerCase();
  }

  function matchRole(element, role) {
    if (!role) {
      return true;
    }

    const expected = normalizeRoleToken(parseRole(role));
    const tag = element.tagName.toLowerCase();
    const type = (element.getAttribute('type') || '').toLowerCase();
    const ariaRole = (element.getAttribute('role') || '').toLowerCase();

    const roleMap = {
      button: () =>
        tag === 'button' ||
        (tag === 'input' && ['button', 'submit', 'reset', 'image'].includes(type)) ||
        ariaRole === 'button',
      edit: () =>
        tag === 'textarea' ||
        (tag === 'input' && !['checkbox', 'radio', 'button', 'submit', 'reset', 'hidden', 'file', 'image'].includes(type)) ||
        ariaRole === 'textbox',
      checkbox: () => (tag === 'input' && type === 'checkbox') || ariaRole === 'checkbox',
      checkboxalt: () => (tag === 'input' && type === 'checkbox') || ariaRole === 'checkbox',
      radiobutton: () => (tag === 'input' && type === 'radio') || ariaRole === 'radio',
      combobox: () => tag === 'select' || ariaRole === 'combobox',
      list: () => tag === 'ul' || tag === 'ol' || ariaRole === 'list',
      listitem: () => tag === 'li' || ariaRole === 'listitem',
      link: () => tag === 'a' || ariaRole === 'link',
      image: () => tag === 'img' || ariaRole === 'img',
      pane: () => tag === 'div' || tag === 'section' || tag === 'main' || ariaRole === 'region',
      window: () => tag === 'body' || ariaRole === 'document',
      tab: () => tag === 'body' || ariaRole === 'document',
      document: () => tag === 'body' || ariaRole === 'document',
      text: () => tag === 'span' || tag === 'p' || tag === 'label' || ariaRole === 'text',
      frm: () => tag === 'iframe' || tag === 'frame',
      frame: () => tag === 'iframe' || tag === 'frame'
    };

    const aliases = {
      checkbox: 'checkboxalt',
      'checkboxalt': 'checkboxalt'
    };

    const key = normalizeRoleToken(expected);
    const mapped = aliases[key] || key;
    if (roleMap[mapped]) {
      return roleMap[mapped]();
    }

    return normalizeRoleToken(tag) === key || ariaRole === key;
  }

  function matchProperty(element, property) {
    if (!property || property.isSelected === false) {
      return true;
    }

    const name = property.name;
    const expected = property.value || '';
    const compare = (actual) => {
      if (property.isRegex) {
        try {
          return new RegExp(expected, 'i').test(String(actual || ''));
        } catch (error) {
          return false;
        }
      }

      return String(actual || '') === expected;
    };

    switch (name) {
      case 'ControlType':
        return matchRole(element, expected);
      case 'Name':
      case 'Title':
        if (element.tagName && (element.tagName.toLowerCase() === 'iframe' || element.tagName.toLowerCase() === 'frame')) {
          if (compare(element.getAttribute('name') || '')) {
            return true;
          }
          if (compare(element.getAttribute('title') || '')) {
            return true;
          }
        }
        if (compare(element.getAttribute('name') || '')) {
          return true;
        }
        return compare(getElementName(element));
      case 'AutomationId':
        return compare(getAutomationId(element));
      case 'id':
        return compare(element.id || '');
      case 'ClassName':
      case 'class':
        return property.isRegex
          ? new RegExp(expected, 'i').test(element.className || '')
          : (` ${element.className || ''} `).includes(` ${expected} `);
      case 'TagName':
      case 'tag':
        return compare(element.tagName.toLowerCase());
      case 'Type':
      case 'type':
        return compare((element.getAttribute('type') || '').toLowerCase());
      case 'Href':
      case 'href':
        return compare(element.getAttribute('href') || '');
      case 'name':
        return compare(element.getAttribute('name') || '');
      case 'title':
        return compare(element.getAttribute('title') || '');
      case 'url':
      case 'Url':
        if (element.tagName && (element.tagName.toLowerCase() === 'iframe' || element.tagName.toLowerCase() === 'frame')) {
          return compare(element.getAttribute('src') || '');
        }
        return true;
      case 'src':
        return compare(element.getAttribute('src') || '');
      case 'FrameworkId':
        return compare(element.getAttribute('data-framework') || '');
      case 'IndexInParent':
      case 'idx':
      case 'ProcessName':
      case 'FileName':
        return true;
      default:
        return matchHtmlAttributeProperty(element, name, expected, compare);
    }
  }

  function matchLevel(element, level) {
    if (level && String(level.tagName || '').toLowerCase() === 'frm') {
      const tag = element.tagName.toLowerCase();
      if (tag !== 'iframe' && tag !== 'frame') {
        return false;
      }
    }

    const selected = (level.properties || []).filter((property) => property.isSelected !== false);
    for (const property of selected) {
      if (!matchProperty(element, property)) {
        return false;
      }
    }

    const indexProperty = selected.find(function (property) {
      return property.name === 'IndexInParent' || property.name === 'idx';
    });
    if (indexProperty && indexProperty.value !== '' && indexProperty.value != null) {
      const parent = element.parentElement;
      if (!parent) {
        return false;
      }

      const idx = parseInt(indexProperty.value, 10);
      const siblings = Array.from(parent.children);
      return siblings.indexOf(element) === idx;
    }

    return true;
  }

  function collectDeepElements(root) {
    const result = [];
    const stack = [root];

    while (stack.length > 0) {
      const node = stack.pop();
      if (!node) {
        continue;
      }

      if (node.nodeType === 1) {
        result.push(node);

        if (node.shadowRoot) {
          stack.push(node.shadowRoot);
        }
      }

      const children = node.children;
      if (children && children.length > 0) {
        for (let i = children.length - 1; i >= 0; i -= 1) {
          stack.push(children[i]);
        }
      }
    }

    return result;
  }

  function getCandidates(parent, searchDescendants) {
    if (!parent) {
      return [];
    }

    if (searchDescendants) {
      if (parent.nodeType === 9) {
        return collectDeepElements(parent.documentElement || parent.body || parent);
      }

      return collectDeepElements(parent);
    }

    return Array.from(parent.children || []);
  }

  function shouldSearchDescendants(parent, searchDescendants, isRootLevel) {
    if (searchDescendants) {
      return true;
    }

    if (!isRootLevel || !parent) {
      return false;
    }

    // iframe.contentDocument is a different Document than the top-level `document`.
    if (parent.nodeType === 9) {
      return true;
    }

    return parent === document || parent === document.documentElement || parent === document.body;
  }

  function findMatchingElements(parent, level, searchDescendants, isRootLevel) {
    const candidates = getCandidates(
      parent,
      shouldSearchDescendants(parent, searchDescendants, isRootLevel)
    );
    return candidates.filter((candidate) => matchLevel(candidate, level));
  }

  function findElements(levels, root, maxResults) {
    const enabledLevels = parseSelectorLevels(levels).filter((level) => level.isEnabled !== false);
    if (enabledLevels.length === 0) {
      return [];
    }

    let current = [root || document];
    for (let i = 0; i < enabledLevels.length; i += 1) {
      const level = enabledLevels[i];
      const next = [];
      const searchDescendants = i > 0;
      const isRootLevel = i === 0;

      for (const parent of current) {
        const matches = findMatchingElements(parent, level, searchDescendants, isRootLevel);
        next.push(...matches);
        if (next.length >= (maxResults || Number.MAX_SAFE_INTEGER)) {
          break;
        }
      }

      current = next.slice(0, maxResults || Number.MAX_SAFE_INTEGER);
      if (current.length === 0) {
        break;
      }
    }

    return current;
  }

  function waitForElements(levels, root, options) {
    const timeout = options?.timeout || 5000;
    const delayBefore = options?.delayBefore || 0;
    const waitState = options?.waitState || 'Attached';
    const index = options?.index || 0;
    const started = Date.now();
    const searchRoot = root || document;

    pageTrace('waitForElements start timeout=' + timeout + ' levels=' + (levels ? levels.length : 0));

    return new Promise((resolve, reject) => {
      let settled = false;
      let observer = null;
      let pollTimer = null;
      let timeoutTimer = null;

      const finish = (handler, value) => {
        if (settled) {
          return;
        }

        settled = true;
        if (observer) {
          observer.disconnect();
          observer = null;
        }

        if (pollTimer != null) {
          clearTimeout(pollTimer);
          pollTimer = null;
        }

        if (timeoutTimer != null) {
          clearTimeout(timeoutTimer);
          timeoutTimer = null;
        }

        handler(value);
      };

      const tryFind = () => {
        if (delayBefore > 0 && Date.now() - started < delayBefore) {
          return null;
        }

        const matches = findElements(levels, searchRoot);
        const element = matches[index];
        if (element && matchWaitState(element, waitState)) {
          scrollIntoViewIfNeeded(element);
          return element;
        }

        return null;
      };

      const found = tryFind();
      if (found) {
        pageTrace('waitForElements immediate match tag=' + (found.tagName || '') + ' id=' + (found.id || ''));
        resolve(found);
        return;
      }

      pageTrace('waitForElements watching DOM');
      const observeTarget = searchRoot === document ? document.documentElement : searchRoot;
      if (observeTarget && typeof MutationObserver !== 'undefined') {
        observer = new MutationObserver(() => {
          const matched = tryFind();
          if (matched) {
            pageTrace('waitForElements mutation match tag=' + (matched.tagName || '') + ' id=' + (matched.id || ''));
            finish(resolve, matched);
          }
        });

        observer.observe(observeTarget, {
          childList: true,
          subtree: true,
          attributes: true,
          characterData: true
        });
      }

      const poll = () => {
        if (settled) {
          return;
        }

        const matched = tryFind();
        if (matched) {
          pageTrace('waitForElements poll match tag=' + (matched.tagName || '') + ' id=' + (matched.id || ''));
          finish(resolve, matched);
          return;
        }

        if (Date.now() - started >= timeout) {
          pageTrace('waitForElements timeout after ' + (Date.now() - started) + 'ms');
          finish(reject, new Error('Element not found for selector within timeout.'));
          return;
        }

        pollTimer = setTimeout(poll, 50);
      };

      pollTimer = setTimeout(poll, 50);
      timeoutTimer = setTimeout(() => {
        pageTrace('waitForElements hard timeout after ' + timeout + 'ms');
        finish(reject, new Error('Element not found for selector within timeout.'));
      }, timeout);
    });
  }

  function matchWaitState(element, waitState) {
    switch (waitState) {
      case 'Visible':
        return isVisible(element);
      case 'Hidden':
        return !isVisible(element);
      case 'Detached':
        return !element.isConnected;
      case 'Attached':
      case 'None':
      default:
        return element.isConnected;
    }
  }

  function getFrameDocument(element) {
    if (!element) {
      return null;
    }

    try {
      return element.contentDocument || (element.contentWindow && element.contentWindow.document) || null;
    } catch (error) {
      return null;
    }
  }

  function scrollFrameIntoView(frameElement) {
    if (!frameElement || typeof frameElement.scrollIntoView !== 'function') {
      return;
    }

    scrollIntoViewIfNeeded(frameElement);

    const rect = frameElement.getBoundingClientRect();
    if (rect.bottom <= window.innerHeight && rect.top >= 0) {
      return;
    }

    const targetTop = window.scrollY + rect.top - Math.max(80, window.innerHeight * 0.25);
    window.scrollTo({ top: Math.max(0, targetTop), left: 0, behavior: 'auto' });
  }

  function findFrameCandidates(parent, level) {
    const isRoot = parent === document || parent === document.documentElement;
    const matches = findMatchingElements(parent, level, true, isRoot);
    if (matches.length > 0) {
      return matches;
    }

    if (!parent || typeof parent.querySelector !== 'function') {
      return [];
    }

    const props = (level.properties || []).filter((property) => property.isSelected !== false);
    const nameProp = props.find((property) => property.name === 'Name' || property.name === 'Title');
    if (nameProp && nameProp.value) {
      const value = String(nameProp.value);
      const byName = parent.querySelector('iframe[name="' + value.replace(/"/g, '\\"') + '"], frame[name="' + value.replace(/"/g, '\\"') + '"]');
      if (byName) {
        return [byName];
      }

      const byTitle = parent.querySelector('iframe[title="' + value.replace(/"/g, '\\"') + '"], frame[title="' + value.replace(/"/g, '\\"') + '"]');
      if (byTitle) {
        return [byTitle];
      }
    }

    return [];
  }

  function forceLoadFrame(frameElement) {
    if (!frameElement) {
      return;
    }

    const src = frameElement.getAttribute('src');
    if (!src) {
      return;
    }

    try {
      const absolute = new URL(src, document.baseURI || window.location.href).href;
      const frameDocument = getFrameDocument(frameElement);
      if (isFrameDocumentReady(frameElement, frameDocument)) {
        return;
      }

      frameElement.src = absolute;
    } catch (error) {
      // ignore invalid src
    }
  }

  function isFrameDocumentReady(frameElement, frameDocument) {
    if (!frameDocument) {
      return false;
    }

    if (frameDocument.readyState !== 'complete' && frameDocument.readyState !== 'interactive') {
      return false;
    }

    const src = frameElement && frameElement.getAttribute('src');
    if (!src) {
      return true;
    }

    try {
      const frameWindow = frameElement.contentWindow;
      const actualUrl = frameWindow && frameWindow.location ? String(frameWindow.location.href || '') : '';
      if (actualUrl && actualUrl !== 'about:blank') {
        return true;
      }
    } catch (error) {
      if (frameDocument.body && frameDocument.body.children.length > 0) {
        return true;
      }

      return frameDocument.body != null;
    }

    if (frameDocument.body && frameDocument.body.children.length > 0) {
      return true;
    }

    return frameDocument.readyState === 'complete' || frameDocument.readyState === 'interactive';
  }

  async function prepareLazyFrames(root) {
    const doc = root && root.nodeType === 9 ? root : document;
    if (!doc || !doc.documentElement) {
      return;
    }

    window.scrollTo({ top: doc.documentElement.scrollHeight, behavior: 'auto' });
    await new Promise((resolve) => setTimeout(resolve, 200));
  }

  async function waitForFrameDocument(frameElement, timeoutMs) {
    const waitMs = Math.max(timeoutMs || 10000, 0);
    const started = Date.now();
    let loadListenerAttached = false;

    while (Date.now() - started < waitMs) {
      scrollFrameIntoView(frameElement);
      forceLoadFrame(frameElement);

      const frameDocument = getFrameDocument(frameElement);
      if (isFrameDocumentReady(frameElement, frameDocument)) {
        return frameDocument;
      }

      if (!loadListenerAttached && typeof frameElement.addEventListener === 'function') {
        loadListenerAttached = true;
        frameElement.addEventListener('load', () => {}, { once: true });
      }

      await new Promise((resolve) => setTimeout(resolve, 50));
    }

    const frameDocument = getFrameDocument(frameElement);
    return isFrameDocumentReady(frameElement, frameDocument) ? frameDocument : null;
  }

  function resolveSearchRoot(frameLevels, root) {
    let current = root || document;
    const levels = parseSelectorLevels(frameLevels);

    for (let i = 0; i < levels.length; i += 1) {
      const level = levels[i];
      const matches = findMatchingElements(
        current,
        level,
        true,
        current === document || current === document.documentElement
      );
      if (matches.length === 0) {
        throw new Error('Frame not found for selector level ' + (i + 1) + '.');
      }

      const frameDocument = getFrameDocument(matches[0]);
      if (!frameDocument) {
        throw new Error('Cannot access frame document (cross-origin or not loaded).');
      }

      current = frameDocument;
    }

    return current;
  }

  async function resolveSearchRootAsync(frameLevels, root, timeout) {
    const waitMs = timeout || 10000;
    let current = root || document;
    const levels = parseSelectorLevels(frameLevels);

    if (levels.length > 0) {
      await prepareLazyFrames(current);
    }

    for (let i = 0; i < levels.length; i += 1) {
      const level = levels[i];
      const started = Date.now();
      let frameDocument = null;

      while (Date.now() - started < waitMs) {
        const matches = findFrameCandidates(
          current,
          level
        );

        if (matches.length > 0) {
          const remaining = Math.max(500, waitMs - (Date.now() - started));
          frameDocument = await waitForFrameDocument(matches[0], remaining);
          if (frameDocument) {
            current = frameDocument;
            break;
          }
        } else if (current === document || current === document.documentElement) {
          window.scrollBy(0, Math.min(window.innerHeight, 600));
        }

        await new Promise((resolve) => setTimeout(resolve, 100));
      }

      if (!frameDocument) {
        throw new Error('Frame not found or not loaded for selector level ' + (i + 1) + '.');
      }
    }

    return current;
  }

  function isVisible(element) {
    if (!element || !element.isConnected) {
      return false;
    }

    const style = window.getComputedStyle(element);
    return style.visibility !== 'hidden' && style.display !== 'none' && element.getClientRects().length > 0;
  }

  function isIntersectingViewport(element) {
    const rect = element.getBoundingClientRect();
    if (rect.width <= 0 && rect.height <= 0) {
      return false;
    }

    const viewHeight = window.innerHeight || document.documentElement.clientHeight;
    const viewWidth = window.innerWidth || document.documentElement.clientWidth;
    return rect.bottom > 0 && rect.right > 0 && rect.top < viewHeight && rect.left < viewWidth;
  }

  function scrollIntoViewIfNeeded(element) {
    if (!element || !element.isConnected || typeof element.scrollIntoView !== 'function') {
      return;
    }

    if (isIntersectingViewport(element)) {
      return;
    }

    pageTrace('scrollIntoViewIfNeeded tag=' + (element.tagName || '') + ' id=' + (element.id || ''));
    try {
      element.scrollIntoView({ block: 'center', inline: 'nearest' });
    } catch (error) {
      element.scrollIntoView();
    }
  }

  root.DomSelectorResolver = {
    findElements,
    waitForElements,
    parseSelectorLevels,
    matchLevel,
    isVisible,
    isIntersectingViewport,
    scrollIntoViewIfNeeded,
    scrollFrameIntoView,
    waitForFrameDocument,
    matchWaitState,
    resolveSearchRoot,
    resolveSearchRootAsync,
    isFrameDocumentReady,
    findFrameCandidates,
    getFrameDocument
  };
})(typeof globalThis !== 'undefined' ? globalThis : self);
