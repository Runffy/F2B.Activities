/* global globalThis, DomSelectorResolver */
(function (root) {
  function escapeRegex(value) {
    return String(value).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  function getControlType(element) {
    const tag = (element.tagName || '').toLowerCase();
    const type = (element.getAttribute('type') || '').toLowerCase();
    const ariaRole = (element.getAttribute('role') || '').toLowerCase();

    if (tag === 'button' || ariaRole === 'button') {
      return 'Button';
    }

    if (tag === 'textarea' || ariaRole === 'textbox') {
      return 'Edit';
    }

    if (tag === 'input') {
      if (type === 'checkbox' || ariaRole === 'checkbox') {
        return 'Checkbox';
      }

      if (type === 'radio' || ariaRole === 'radio') {
        return 'RadioButton';
      }

      if (['button', 'submit', 'reset', 'image'].includes(type)) {
        return 'Button';
      }

      return 'Edit';
    }

    if (tag === 'select' || ariaRole === 'combobox') {
      return 'ComboBox';
    }

    if (tag === 'a' || ariaRole === 'link') {
      return 'Link';
    }

    if (tag === 'img' || ariaRole === 'img') {
      return 'Image';
    }

    if (tag === 'iframe' || tag === 'frame') {
      return 'Frm';
    }

    if (tag === 'li' || ariaRole === 'listitem') {
      return 'ListItem';
    }

    if (tag === 'ul' || tag === 'ol' || ariaRole === 'list') {
      return 'List';
    }

    return 'Pane';
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

    const tag = (element.tagName || '').toLowerCase();
    if (tag === 'rect' || tag === 'path' || tag === 'svg' || tag === 'g') {
      return tag;
    }

    const text = getDirectElementText(element);
    if (text.length > 120) {
      return text.substring(0, 120) + '...';
    }

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
      ''
    );
  }

  function getClassName(element) {
    return (element.getAttribute('class') || '').trim();
  }

  function getIndexInParent(element) {
    const parent = element.parentElement;
    if (!parent) {
      return -1;
    }

    return Array.prototype.indexOf.call(parent.children, element);
  }

  function isSkippableTag(element) {
    const tag = (element.tagName || '').toLowerCase();
    return tag === 'html' || tag === 'head' || tag === 'body';
  }

  function buildPathToRoot(element) {
    const path = [];
    let current = element;

    while (current) {
      if (!isSkippableTag(current)) {
        path.unshift(current);
      }

      const parent = current.parentElement;
      if (parent) {
        current = parent;
        continue;
      }

      const frameEl = current.ownerDocument && current.ownerDocument.defaultView
        ? current.ownerDocument.defaultView.frameElement
        : null;
      if (!frameEl) {
        break;
      }

      if (!isSkippableTag(frameEl)) {
        path.unshift(frameEl);
      }

      const hostDocument = frameEl.ownerDocument;
      const hostFrame = hostDocument && hostDocument.defaultView
        ? hostDocument.defaultView.frameElement
        : null;
      if (hostFrame) {
        current = frameEl.parentElement || frameEl;
        continue;
      }

      break;
    }

    return path;
  }

  function splitOperationLevels(levels) {
    const enabled = levels.filter(function (level) {
      return level.isEnabled !== false && level.tagName !== 'wnd';
    });

    const frameLevels = [];
    const elementLevels = [];
    let seenElementLevel = false;

    for (let i = 0; i < enabled.length; i += 1) {
      const level = enabled[i];
      if (!seenElementLevel && level.tagName === 'frm') {
        frameLevels.push(level);
      } else {
        seenElementLevel = true;
        elementLevels.push(level);
      }
    }

    return {
      frameLevels: frameLevels,
      elementLevels: elementLevels
    };
  }

  function countOperationMatches(levels) {
    const split = splitOperationLevels(levels);
    if (split.frameLevels.length === 0 && split.elementLevels.length === 0) {
      return 0;
    }

    try {
      let root = document;
      if (split.frameLevels.length > 0) {
        root = DomSelectorResolver.resolveSearchRoot(split.frameLevels, document);
      }

      if (split.elementLevels.length === 0) {
        return split.frameLevels.length > 0 ? 1 : 0;
      }

      return DomSelectorResolver.findElements(split.elementLevels, root).length;
    } catch (error) {
      return 0;
    }
  }

  function createProperty(name, value, isSelected, isRegex) {
    return {
      name: name,
      value: value || '',
      isSelected: !!isSelected && !!value && isCompactPropertyValue(value),
      isRegex: !!isRegex
    };
  }

  function createSelectedProperty(name, value, isRegex) {
    return {
      name: name,
      value: value || '',
      isSelected: !!value,
      isRegex: !!isRegex
    };
  }

  function buildTitleReValue(title) {
    const text = String(title || '').trim();
    if (!text) {
      return '';
    }

    if (text.length <= 20) {
      return escapeRegex(text);
    }

    return escapeRegex(text.substring(0, 20)) + '.*';
  }

  function buildTextReValue(text) {
    const value = String(text || '').trim();
    if (!value) {
      return '';
    }

    if (value.length <= 20) {
      return escapeRegex(value);
    }

    return escapeRegex(value.substring(0, 20)) + '.*';
  }

  function getDirectElementText(element) {
    if (!element || !element.childNodes) {
      return '';
    }

    let text = '';
    for (let i = 0; i < element.childNodes.length; i += 1) {
      const node = element.childNodes[i];
      if (node.nodeType === 3) {
        text += node.textContent || '';
      }
    }

    text = text.trim();
    if (text.length > 500) {
      return text.substring(0, 500);
    }

    return text;
  }

  function getElementTextForSelector(element) {
    if (!element) {
      return '';
    }

    // Only direct text nodes on this element; exclude text from child elements.
    return getDirectElementText(element);
  }

  function isCompactPropertyValue(value) {
    if (value == null) {
      return false;
    }

    const text = String(value);
    if (!text) {
      return false;
    }

    if (/[\r\n]/.test(text)) {
      return false;
    }

    return text.length <= 20;
  }

  function findProperty(level, name) {
    if (!level || !level.properties) {
      return null;
    }

    return level.properties.find(function (item) {
      return item.name === name && item.value;
    }) || null;
  }

  function selectProperty(level, name) {
    const prop = findProperty(level, name);
    if (prop && isCompactPropertyValue(prop.value)) {
      prop.isSelected = true;
    }

    return prop;
  }

  function clearPropertySelections(level) {
    if (!level || !level.properties) {
      return;
    }

    level.properties.forEach(function (prop) {
      prop.isSelected = false;
    });
  }

  function getSelectedProperties(level) {
    if (!level || !level.properties) {
      return [];
    }

    return level.properties.filter(function (item) {
      return item.isSelected && item.value;
    });
  }

  function ensureLevelHasMinimumProperties(level) {
    if (!level || level.tagName !== 'ctrl') {
      return;
    }

    if (getSelectedProperties(level).length > 0) {
      return;
    }

    selectProperty(level, 'tag');
    selectProperty(level, 'idx');
  }

  function countMatches(parent, levelProperties, tagName) {
    if (!parent || !parent.children) {
      return 0;
    }

    const level = {
      tagName: tagName,
      properties: levelProperties.filter((item) => item.isSelected !== false)
    };

    let count = 0;
    for (let i = 0; i < parent.children.length; i += 1) {
      const child = parent.children[i];
      if (DomSelectorResolver.matchLevel(child, level)) {
        count += 1;
      }
    }

    return count;
  }

  function isRegexFriendlyAttr(name) {
    return name === 'href' || name === 'src' || name === 'url';
  }

  function populateCtrlProperties(level, element, isTarget) {
    if (!element || !element.attributes) {
      return;
    }

    for (let i = 0; i < element.attributes.length; i += 1) {
      const attr = element.attributes[i];
      level.properties.push(createProperty(
        attr.name,
        attr.value,
        false,
        isRegexFriendlyAttr(attr.name)
      ));
    }

    const tag = (element.tagName || '').toLowerCase();
    if (tag) {
      level.properties.push(createProperty('tag', tag, false, false));
    }

    const index = getIndexInParent(element);
    if (index >= 0) {
      level.properties.push(createProperty('idx', String(index), false, false));
    }

    const text = getElementName(element);
    if (text) {
      level.properties.push(createProperty('text', text, false, true));
    }

    applyDefaultCtrlSelections(level);

    if (isTarget && level.properties.length > 0) {
      const parent = element.parentElement;
      const selected = level.properties.filter(function (item) {
        return item.isSelected;
      });
      if (parent && selected.length > 0 && countMatches(parent, selected, 'ctrl') > 1) {
        const idxProp = level.properties.find(function (item) {
          return item.name === 'idx';
        });
        if (idxProp) {
          idxProp.isSelected = true;
        }
      }
    }
  }

  function applyDefaultCtrlSelections(level) {
    clearPropertySelections(level);

    const preferred = [
      'id',
      'data-automation-id',
      'data-automationid',
      'automationid',
      'name',
      'text',
      'class',
      'type',
      'placeholder'
    ];
    for (let i = 0; i < preferred.length; i += 1) {
      const prop = findProperty(level, preferred[i]);
      if (prop && isCompactPropertyValue(prop.value)) {
        prop.isSelected = true;
        return;
      }
    }
  }

  function populateFrmProperties(level, element) {
    if (!element || !element.attributes) {
      return;
    }

    for (let i = 0; i < element.attributes.length; i += 1) {
      const attr = element.attributes[i];
      level.properties.push(createProperty(
        attr.name,
        attr.value,
        false,
        isRegexFriendlyAttr(attr.name)
      ));
    }

    applyDefaultFrmSelections(level);
  }

  function applyDefaultFrmSelections(level) {
    level.properties.forEach(function (prop) {
      prop.isSelected = false;
    });

    const preferred = ['name', 'title', 'src'];
    for (let i = 0; i < preferred.length; i += 1) {
      const prop = level.properties.find(function (item) {
        return item.name === preferred[i] && item.value;
      });
      if (prop && isCompactPropertyValue(prop.value)) {
        prop.isSelected = true;
        return;
      }
    }
  }

  function populateWndProperties(level, tabTitle, tabUrl) {
    level.properties.push(createProperty('title', tabTitle || document.title || '', true, false));

    if (tabUrl || location.href) {
      level.properties.push(createProperty('url', tabUrl || location.href, false, false));
    }
  }

  function minimizeLevelPropertiesInContext(level, allLevels) {
    if (!level || level.tagName === 'wnd') {
      return;
    }

    const priority = [
      'id',
      'data-automation-id',
      'data-automationid',
      'automationid',
      'name',
      'text',
      'class',
      'tag',
      'type',
      'href',
      'placeholder',
      'idx'
    ];
    const candidates = level.properties
      .filter(function (prop) {
        return prop.value &&
          isCompactPropertyValue(prop.value) &&
          (priority.indexOf(prop.name) >= 0 || prop.name.indexOf('data-') === 0);
      })
      .sort(function (a, b) {
        const aIndex = priority.indexOf(a.name);
        const bIndex = priority.indexOf(b.name);
        const aRank = aIndex >= 0 ? aIndex : 1000;
        const bRank = bIndex >= 0 ? bIndex : 1000;
        if (aRank !== bRank) {
          return aRank - bRank;
        }
        return a.name.localeCompare(b.name);
      });

    if (candidates.length === 0) {
      ensureLevelHasMinimumProperties(level);
      return;
    }

    for (let count = 1; count <= candidates.length; count += 1) {
      candidates.forEach(function (prop) {
        prop.isSelected = false;
      });

      for (let i = 0; i < count; i += 1) {
        candidates[i].isSelected = true;
      }

      if (countOperationMatches(allLevels) === 1) {
        return;
      }
    }

    applyLevelPropertyFallback(level, allLevels);
  }

  function applyLevelPropertyFallback(level, allLevels) {
    clearPropertySelections(level);

    const fallbackOrder = ['text', 'class', 'type', 'placeholder', 'name'];
    for (let i = 0; i < fallbackOrder.length; i += 1) {
      const prop = findProperty(level, fallbackOrder[i]);
      if (!prop || !isCompactPropertyValue(prop.value)) {
        continue;
      }

      prop.isSelected = true;
      if (countOperationMatches(allLevels) === 1) {
        return;
      }
    }

    ensureLevelHasMinimumProperties(level);
  }

  function minimizeSelectorLevels(levels) {
    const lastIndex = levels.length - 1;

    levels.forEach(function (level) {
      level.canDisable = true;
    });

    for (let i = 0; i < levels.length; i += 1) {
      const level = levels[i];
      if (level.tagName === 'wnd' || level.tagName === 'frm') {
        level.isEnabled = true;
      } else if (i === lastIndex) {
        level.isEnabled = true;
      } else {
        level.isEnabled = false;
      }
    }

    if (countOperationMatches(levels) !== 1) {
      for (let i = lastIndex - 1; i >= 1; i -= 1) {
        if (levels[i].tagName !== 'ctrl') {
          continue;
        }

        levels[i].isEnabled = true;
        if (countOperationMatches(levels) === 1) {
          break;
        }
      }
    }

    const enabledIndices = [];
    for (let i = 0; i < levels.length; i += 1) {
      if (levels[i].isEnabled !== false && levels[i].tagName !== 'wnd') {
        enabledIndices.push(i);
      }
    }

    for (let ei = enabledIndices.length - 1; ei >= 0; ei -= 1) {
      minimizeLevelPropertiesInContext(levels[enabledIndices[ei]], levels);
    }

    ensureSelectorUniqueness(levels);
    return levels;
  }

  function ensureSelectorUniqueness(levels) {
    for (let i = 0; i < levels.length; i += 1) {
      const level = levels[i];
      if (level.isEnabled !== false && level.tagName === 'ctrl') {
        ensureLevelHasMinimumProperties(level);
      }
    }

    if (countOperationMatches(levels) === 1) {
      return levels;
    }

    for (let i = levels.length - 1; i >= 1; i -= 1) {
      const level = levels[i];
      if (level.tagName !== 'ctrl') {
        continue;
      }

      level.isEnabled = true;
      applyLevelPropertyFallback(level, levels);
      ensureLevelHasMinimumProperties(level);

      if (countOperationMatches(levels) === 1) {
        return levels;
      }
    }

    for (let i = 0; i < levels.length; i += 1) {
      const level = levels[i];
      if (level.isEnabled !== false && level.tagName === 'ctrl') {
        selectProperty(level, 'tag');
        selectProperty(level, 'idx');
      }
    }

    return levels;
  }

  function buildLevelForElement(element, tagName, index, totalCount, tabTitle, tabUrl) {
    const level = {
      tagName: tagName,
      properties: [],
      isEnabled: true,
      canDisable: true
    };

    if (tagName === 'wnd') {
      populateWndProperties(level, tabTitle, tabUrl);
      return level;
    }

    if (tagName === 'frm') {
      populateFrmProperties(level, element);
      return level;
    }

    populateCtrlProperties(level, element, index === totalCount - 1);
    return level;
  }

  function populateAutoWndLevel(level, tabTitle, tabUrl) {
    const fullTitle = tabTitle || document.title || '';
    const titleRe = buildTitleReValue(fullTitle);
    if (titleRe) {
      level.properties.push(createSelectedProperty('title', titleRe, true));
    }

    if (fullTitle && fullTitle !== titleRe) {
      level.properties.push(createProperty('title', fullTitle, false, false));
    }

    if (tabUrl || location.href) {
      level.properties.push(createProperty('url', tabUrl || location.href, false, false));
    }
  }

  function findProperties(level, name) {
    if (!level || !level.properties) {
      return [];
    }

    return level.properties.filter(function (item) {
      return item.name === name && item.value;
    });
  }

  function hasProperty(level, name) {
    return findProperty(level, name) != null;
  }

  function populateAllElementAttributes(level, element) {
    if (!element || !element.attributes) {
      return;
    }

    for (let i = 0; i < element.attributes.length; i += 1) {
      const attr = element.attributes[i];
      if (!attr.value) {
        continue;
      }

      if (hasProperty(level, attr.name)) {
        continue;
      }

      level.properties.push(createProperty(
        attr.name,
        attr.value,
        false,
        isRegexFriendlyAttr(attr.name)
      ));
    }
  }

  function ensureDerivedCtrlProperties(level, element, includeText) {
    if (!element) {
      return;
    }

    const tag = (element.tagName || '').toLowerCase();
    if (tag && !hasProperty(level, 'tag')) {
      level.properties.push(createProperty('tag', tag, false, false));
    }

    const index = getIndexInParent(element);
    if (index >= 0 && !hasProperty(level, 'idx')) {
      level.properties.push(createProperty('idx', String(index), false, false));
    }

    if (includeText) {
      const text = getElementTextForSelector(element);
      if (text && !hasProperty(level, 'text')) {
        level.properties.push(createProperty('text', text, false, true));
      }
    }
  }

  function applyAutoCtrlSelections(level, element, isTarget) {
    clearPropertySelections(level);

    const id = element.id || element.getAttribute('id');
    const text = getElementTextForSelector(element);
    const name = element.getAttribute('name');
    const index = getIndexInParent(element);

    if (isTarget) {
      selectPropertyByName(level, 'tag', true);

      if (id) {
        selectPropertyByName(level, 'id', true);
        return;
      }

      if (text) {
        if (text.length > 20) {
          level.properties.unshift(createSelectedProperty('text', buildTextReValue(text), true));
        } else {
          selectPropertyByName(level, 'text', true);
        }

        return;
      }

      if (name) {
        selectPropertyByName(level, 'name', true);
        return;
      }

      if (index >= 0) {
        selectPropertyByName(level, 'idx', true);
      }

      return;
    }

    selectPropertyByName(level, 'tag', true);
    if (index >= 0) {
      selectPropertyByName(level, 'idx', true);
    }
  }

  function selectPropertyByName(level, name, force) {
    const prop = findProperty(level, name);
    if (prop && (force || isCompactPropertyValue(prop.value))) {
      prop.isSelected = true;
    }

    return prop;
  }

  function populateAutoCtrlLevel(level, element, isTarget) {
    if (!element) {
      return;
    }

    populateAllElementAttributes(level, element);
    ensureDerivedCtrlProperties(level, element, isTarget);
    applyAutoCtrlSelections(level, element, isTarget);
  }

  function populateAutoFrmLevel(level, element) {
    if (!element) {
      return;
    }

    populateAllElementAttributes(level, element);

    const index = getIndexInParent(element);
    if (index >= 0 && !hasProperty(level, 'idx')) {
      level.properties.push(createProperty('idx', String(index), false, false));
    }

    clearPropertySelections(level);

    const name = element.getAttribute('name');
    const id = element.id || element.getAttribute('id');
    if (name) {
      selectPropertyByName(level, 'name', true);
    } else if (id) {
      selectPropertyByName(level, 'id', true);
    } else if (index >= 0) {
      selectPropertyByName(level, 'idx', true);
    }
  }

  function buildAutoLevelForElement(element, tagName, tabTitle, tabUrl, isTarget) {
    const level = {
      tagName: tagName,
      properties: [],
      isEnabled: true,
      canDisable: tagName !== 'wnd' && tagName !== 'frm'
    };

    if (tagName === 'wnd') {
      populateAutoWndLevel(level, tabTitle, tabUrl);
      return level;
    }

    if (tagName === 'frm') {
      populateAutoFrmLevel(level, element);
      return level;
    }

    populateAutoCtrlLevel(level, element, isTarget);
    return level;
  }

  function buildAutoSelectorLevelsFromElement(element, tabTitle, tabUrl) {
    const path = buildPathToRoot(element);
    const levels = [];

    levels.push(buildAutoLevelForElement(null, 'wnd', tabTitle, tabUrl, false));

    for (let i = 0; i < path.length; i += 1) {
      const el = path[i];
      const tag = (el.tagName || '').toLowerCase();
      const tagName = tag === 'iframe' || tag === 'frame' ? 'frm' : 'ctrl';
      const isTarget = i === path.length - 1;
      levels.push(buildAutoLevelForElement(el, tagName, tabTitle, tabUrl, isTarget));
    }

    return levels;
  }

  function buildUrlReValue(url) {
    const text = String(url || '').trim();
    if (!text) {
      return '';
    }

    let source = text;
    try {
      source = new URL(text, location.href).pathname || text;
    } catch (error) {
      source = text;
    }

    if (source.length <= 20) {
      return escapeRegex(source);
    }

    return escapeRegex(source.substring(0, 20)) + '.*';
  }

  function isStableId(id) {
    if (!id) {
      return false;
    }

    if (/^:r[0-9a-z]+:$/i.test(id)) {
      return false;
    }

    return id.length <= 120;
  }

  function escapeXPathLiteral(value) {
    const text = String(value || '');
    if (text.indexOf('"') === -1) {
      return '"' + text + '"';
    }

    if (text.indexOf("'") === -1) {
      return "'" + text + "'";
    }

    return 'concat("' + text.replace(/"/g, '",\'"\',"') + '")';
  }

  function buildRelativeXPath(element) {
    if (!element || element.nodeType !== 1) {
      return '';
    }

    const segments = [];
    let current = element;
    let anchored = false;

    while (current && current.nodeType === 1) {
      const tag = (current.tagName || '').toLowerCase();
      if (tag === 'html') {
        break;
      }

      if (!anchored && current.id && isStableId(current.id)) {
        segments.unshift('*[@id=' + escapeXPathLiteral(current.id) + ']');
        anchored = true;
        current = current.parentElement;
        break;
      }

      const parent = current.parentElement;
      if (!parent) {
        segments.unshift(tag);
        break;
      }

      const sameTag = Array.from(parent.children).filter(function (child) {
        return child.nodeType === 1 && (child.tagName || '').toLowerCase() === tag;
      });

      let segment = tag;
      if (sameTag.length > 1) {
        segment = tag + '[' + (sameTag.indexOf(current) + 1) + ']';
      }

      segments.unshift(segment);
      if (tag === 'body') {
        break;
      }

      current = parent;
    }

    return segments.length > 0 ? '//' + segments.join('/') : '';
  }

  function buildMinimalWndLevel(tabTitle) {
    return {
      tagName: 'wnd',
      properties: [
        createSelectedProperty('title', buildTitleReValue(tabTitle || document.title || ''), true)
      ],
      isEnabled: true,
      canDisable: false
    };
  }

  function buildMinimalFrmLevel(element) {
    const level = {
      tagName: 'frm',
      properties: [],
      isEnabled: true,
      canDisable: false
    };

    const name = element.getAttribute('name');
    const src = element.getAttribute('src');
    const index = getIndexInParent(element);

    if (name) {
      level.properties.push(createSelectedProperty('name', name, false));
    } else if (src) {
      level.properties.push(createSelectedProperty('src', buildUrlReValue(src), true));
    } else if (index >= 0) {
      level.properties.push(createSelectedProperty('idx', String(index), false));
    }

    return level;
  }

  function buildMinimalTargetProperties(element) {
    const properties = [];
    const tag = (element.tagName || '').toLowerCase();
    if (tag) {
      properties.push(createSelectedProperty('tag', tag, false));
    }

    const id = element.id || element.getAttribute('id');
    if (id) {
      properties.push(createSelectedProperty('id', id, false));
      return properties;
    }

    const text = getDirectElementText(element);
    if (text) {
      if (text.length > 20) {
        properties.push(createSelectedProperty('text', buildTextReValue(text), true));
      } else {
        properties.push(createSelectedProperty('text', text, false));
      }
      return properties;
    }

    const name = element.getAttribute('name');
    if (name) {
      properties.push(createSelectedProperty('name', name, false));
      return properties;
    }

    if (element.value != null && String(element.value) !== '') {
      properties.push(createSelectedProperty('value', String(element.value), false));
      return properties;
    }

    const cls = (element.getAttribute('class') || '').trim();
    if (cls) {
      const firstClass = cls.split(/\s+/).filter(Boolean)[0];
      if (firstClass) {
        properties.push(createSelectedProperty('class', firstClass, false));
        return properties;
      }
    }

    const index = getIndexInParent(element);
    if (index >= 0) {
      properties.push(createSelectedProperty('idx', String(index), false));
    }

    return properties;
  }

  function buildMinimalTargetCtrlLevel(element) {
    return {
      tagName: 'ctrl',
      properties: buildMinimalTargetProperties(element),
      isEnabled: true,
      canDisable: true
    };
  }

  function buildMinimalTargetCtrlWithXPath(element) {
    const tag = (element.tagName || '').toLowerCase();
    const properties = [];
    if (tag) {
      properties.push(createSelectedProperty('tag', tag, false));
    }

    const xpath = buildRelativeXPath(element);
    if (xpath) {
      properties.push(createSelectedProperty('xpath', xpath, false));
    }

    return {
      tagName: 'ctrl',
      properties: properties,
      isEnabled: true,
      canDisable: true
    };
  }

  function buildMinimalSelectorLevelsFromElement(element, tabTitle, tabUrl) {
    const path = buildPathToRoot(element);
    const levels = [buildMinimalWndLevel(tabTitle)];

    for (let i = 0; i < path.length - 1; i += 1) {
      const el = path[i];
      const tag = (el.tagName || '').toLowerCase();
      if (tag === 'iframe' || tag === 'frame') {
        levels.push(buildMinimalFrmLevel(el));
      }
    }

    levels.push(buildMinimalTargetCtrlLevel(element));

    if (countOperationMatches(levels) === 1) {
      return levels;
    }

    levels[levels.length - 1] = buildMinimalTargetCtrlWithXPath(element);
    return levels;
  }

  function buildSelectorLevelsFromElement(element, tabTitle, tabUrl, options) {
    const optimize = options && options.optimize === true;
    if (!optimize) {
      return buildAutoSelectorLevelsFromElement(element, tabTitle, tabUrl);
    }

    const path = buildPathToRoot(element);
    const levels = [];

    levels.push(buildLevelForElement(null, 'wnd', 0, path.length + 1, tabTitle, tabUrl));

    for (let i = 0; i < path.length; i += 1) {
      const el = path[i];
      const tag = (el.tagName || '').toLowerCase();
      const tagName = tag === 'iframe' || tag === 'frame' ? 'frm' : 'ctrl';
      levels.push(buildLevelForElement(el, tagName, i + 1, path.length + 1, tabTitle, tabUrl));
    }

    return minimizeSelectorLevels(levels);
  }

  function describeElement(element) {
    if (!element) {
      return [];
    }

    const level = { properties: [] };
    populateCtrlProperties(level, element, false);

    return level.properties
      .filter(function (item) {
        return item.value !== '' && item.value != null && item.value !== '-1';
      })
      .map(function (item) {
        return { name: item.name, value: item.value };
      });
  }

  function buildVisualTreeLabel(element) {
    const tag = (element.tagName || '').toLowerCase();
    if (!tag) {
      return '<unknown>';
    }

    return '<' + tag + '>';
  }

  function buildDisplayName(element) {
    const role = getControlType(element);
    const dataName = element.getAttribute && element.getAttribute('data-name');
    const nameSource = dataName
      ? dataName.trim()
      : getElementName(element);
    const name = nameSource.length > 80 ? nameSource.substring(0, 80) + '...' : nameSource;
    const automationId = getAutomationId(element);
    return (role + ' "' + name + '" ' + automationId).trim();
  }

  function encodeSegments(segments) {
    return JSON.stringify(segments || []);
  }

  function decodeSegments(value) {
    if (!value) {
      return [];
    }

    if (Array.isArray(value)) {
      return value;
    }

    try {
      return JSON.parse(value);
    } catch (error) {
      return [];
    }
  }

  function normalizeSegment(raw) {
    if (!raw) {
      return null;
    }

    const type = String(raw.type || raw.Type || 'dom').toLowerCase();
    const indexValue = raw.index != null ? raw.index : raw.Index;

    if (type === 'frame') {
      return {
        type: 'enterFrame',
        index: Number(indexValue)
      };
    }

    if (type === 'enterframe') {
      return {
        type: 'enterFrame',
        index: Number(indexValue)
      };
    }

    return {
      type: type,
      index: Number(indexValue)
    };
  }

  function normalizeSegments(value) {
    return decodeSegments(value)
      .map(normalizeSegment)
      .filter((segment) => segment && (segment.type === 'enterFrame' || !Number.isNaN(segment.index)));
  }

  function resolveBySegments(segments) {
    let current = document.documentElement;
    const list = normalizeSegments(segments);

    for (let i = 0; i < list.length; i += 1) {
      const segment = list[i];

      if (segment.type === 'enterFrame') {
        const frame = current.children[segment.index];
        if (!frame || !frame.contentDocument) {
          return null;
        }

        current = frame.contentDocument.documentElement || frame.contentDocument.body;
        continue;
      }

      if (segment.type === 'dom') {
        current = current.children[segment.index];
        if (!current) {
          return null;
        }
      }
    }

    return current;
  }

  function getSegmentsFromElement(element) {
    const segments = [];
    let current = element;

    while (current) {
      const parent = current.parentElement;
      if (parent) {
        segments.unshift({ type: 'dom', index: Array.prototype.indexOf.call(parent.children, current) });
        current = parent;

        if (current === current.ownerDocument.body || current === current.ownerDocument.documentElement) {
          const frameEl = current.ownerDocument.defaultView && current.ownerDocument.defaultView.frameElement;
          if (frameEl) {
            const iframeParent = frameEl.parentElement;
            if (iframeParent) {
              segments.unshift({
                type: 'enterFrame',
                index: Array.prototype.indexOf.call(iframeParent.children, frameEl)
              });
              current = iframeParent;
              continue;
            }
          }
        }

        continue;
      }

      break;
    }

    return segments;
  }

  function getDomChildNodes(segments, maxChildren) {
    const limit = maxChildren || 80;
    let parent = document.documentElement;
    const path = normalizeSegments(segments);

    for (let i = 0; i < path.length; i += 1) {
      const segment = path[i];
      if (segment.type === 'enterFrame') {
        const frame = parent.children[segment.index];
        parent = frame && frame.contentDocument
          ? frame.contentDocument.documentElement || frame.contentDocument.body
          : null;
      } else if (segment.type === 'dom') {
        parent = parent.children[segment.index];
      }

      if (!parent) {
        return [];
      }
    }

    const nodes = [];
    const children = Array.from(parent.children || []);
    for (let i = 0; i < children.length && nodes.length < limit; i += 1) {
      const child = children[i];
      const tag = (child.tagName || '').toLowerCase();
      const nodeSegments = path.concat([{ type: 'dom', index: i }]);
      const childCount = (child.children || []).length;
      const isFrame = tag === 'iframe' || tag === 'frame';
      nodes.push({
        displayName: buildVisualTreeLabel(child),
        segments: nodeSegments,
        frameSegments: isFrame ? nodeSegments.concat([{ type: 'enterFrame', index: i }]) : null,
        childCount: isFrame ? (child.contentDocument ? (child.contentDocument.body || child.contentDocument.documentElement).children.length : 0) : childCount,
        hasFrameChildren: isFrame,
        tagName: tag
      });
    }

    return nodes;
  }

  const HIGHLIGHT_BORDER = '3px solid rgb(255, 0, 0)';
  const HIGHLIGHT_FILL = 'transparent';

  function highlightElement(element, durationMs) {
    if (!element || !element.getBoundingClientRect) {
      return false;
    }

    let rect = element.getBoundingClientRect();
    let doc = element.ownerDocument;
    while (doc && doc !== document) {
      const frame = doc.defaultView && doc.defaultView.frameElement;
      if (!frame) {
        break;
      }

      const frameRect = frame.getBoundingClientRect();
      rect = {
        left: rect.left + frameRect.left,
        top: rect.top + frameRect.top,
        width: rect.width,
        height: rect.height
      };
      doc = frame.ownerDocument;
    }

    const overlayId = '__f2bInspectorHighlight';
    let overlay = document.getElementById(overlayId);
    if (!overlay) {
      overlay = document.createElement('div');
      overlay.id = overlayId;
      overlay.style.position = 'fixed';
      overlay.style.pointerEvents = 'none';
      overlay.style.zIndex = '2147483646';
      overlay.style.boxSizing = 'border-box';
      document.documentElement.appendChild(overlay);
    }

    overlay.style.left = rect.left + 'px';
    overlay.style.top = rect.top + 'px';
    overlay.style.width = Math.max(1, rect.width) + 'px';
    overlay.style.height = Math.max(1, rect.height) + 'px';
    overlay.style.border = HIGHLIGHT_BORDER;
    overlay.style.background = HIGHLIGHT_FILL;

    clearTimeout(overlay.__f2bHideTimer);
    overlay.__f2bHideTimer = setTimeout(() => {
      if (overlay.parentNode) {
        overlay.parentNode.removeChild(overlay);
      }
    }, durationMs || 3000);

    return true;
  }

  root.F2bInspectorBuilder = {
    buildSelectorLevelsFromElement,
    buildAutoSelectorLevelsFromElement,
    buildMinimalSelectorLevelsFromElement,
    buildFastSelectorLevelsFromElement: buildAutoSelectorLevelsFromElement,
    describeElement,
    buildVisualTreeLabel,
    buildDisplayName,
    getSegmentsFromElement,
    normalizeSegments,
    resolveBySegments,
    decodeSegments,
    encodeSegments,
    getDomChildNodes,
    highlightElement,
    buildPathToRoot
  };
})(typeof globalThis !== 'undefined' ? globalThis : self);
