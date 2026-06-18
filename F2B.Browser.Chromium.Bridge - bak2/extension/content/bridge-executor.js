(function () {
  function pageTrace(step) {
    if (typeof globalThis.__f2bPageTrace === 'function') {
      globalThis.__f2bPageTrace(step);
    }
  }

  function sleep(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }

  function dispatchInputEvents(element) {
    element.dispatchEvent(new Event('input', { bubbles: true }));
    element.dispatchEvent(new Event('change', { bubbles: true }));
  }

  function getElementRect(element) {
    const rect = element.getBoundingClientRect();
    return {
      x: rect.x,
      y: rect.y,
      width: rect.width,
      height: rect.height
    };
  }

  function usesClickEventMethod(clickMethod) {
    return String(clickMethod || 'Javascript').toLowerCase() === 'clickevent';
  }

  function getElementClickPoint(element) {
    const rect = element.getBoundingClientRect();
    return {
      clientX: rect.left + Math.max(rect.width, 1) / 2,
      clientY: rect.top + Math.max(rect.height, 1) / 2
    };
  }

  function isSameClickTarget(element, candidate) {
    return !!candidate && (element === candidate || element.contains(candidate) || candidate.contains(element));
  }

  function createMouseEventInit(element, type) {
    const point = getElementClickPoint(element);
    return {
      bubbles: true,
      cancelable: true,
      view: window,
      clientX: point.clientX,
      clientY: point.clientY,
      button: 0,
      buttons: type === 'mouseup' || type === 'pointerup' ? 0 : 1,
      detail: type === 'dblclick' ? 2 : 1
    };
  }

  function dispatchPointerClickSequence(element) {
    const sequence = ['pointerdown', 'mousedown', 'pointerup', 'mouseup', 'click'];
    for (const type of sequence) {
      const Ctor = typeof PointerEvent !== 'undefined' && type.startsWith('pointer')
        ? PointerEvent
        : MouseEvent;
      element.dispatchEvent(new Ctor(type, createMouseEventInit(element, type)));
    }
  }

  function resolveClickEventTarget(element) {
    const point = getElementClickPoint(element);
    const hit = document.elementFromPoint(point.clientX, point.clientY);
    return isSameClickTarget(element, hit) ? hit : element;
  }

  function performJavascriptClick(element) {
    if (typeof element.click !== 'function') {
      throw new Error('element.click is not a function. Set ClickMethod to ClickEvent for SVG and other non-HTML elements.');
    }

    element.click();
  }

  function performJavascriptDoubleClick(element) {
    element.dispatchEvent(new MouseEvent('dblclick', { bubbles: true, cancelable: true }));
  }

  function performClickEventClick(element) {
    dispatchPointerClickSequence(resolveClickEventTarget(element));
  }

  function performClickEventDoubleClick(element) {
    const target = resolveClickEventTarget(element);
    dispatchPointerClickSequence(target);
    dispatchPointerClickSequence(target);
    target.dispatchEvent(new MouseEvent('dblclick', createMouseEventInit(target, 'dblclick')));
  }

  function performElementClick(element, clickMethod) {
    if (usesClickEventMethod(clickMethod)) {
      performClickEventClick(element);
      return;
    }

    performJavascriptClick(element);
  }

  function performElementDoubleClick(element, clickMethod) {
    if (usesClickEventMethod(clickMethod)) {
      performClickEventDoubleClick(element);
      return;
    }

    performJavascriptDoubleClick(element);
  }

  async function resolveTarget(message) {
    pageTrace('resolveTarget start timeout=' + (message.findTimeout || message.timeout || 5000));

    const frameLevels = message.frameSelectorLevels || [];
    const elementLevels = message.scopedSelectorLevels && message.scopedSelectorLevels.length
      ? message.scopedSelectorLevels
      : message.selectorLevels;

    let searchRoot = document;
    if (frameLevels.length > 0) {
      pageTrace('resolveSearchRoot frameLevels=' + frameLevels.length);
      searchRoot = await DomSelectorResolver.resolveSearchRootAsync(
        frameLevels,
        document,
        message.findTimeout || message.timeout || 10000
      );
    } else if (message.scopeElementPath) {
      searchRoot = document.querySelector(message.scopeElementPath) || document;
    }

    const element = await DomSelectorResolver.waitForElements(elementLevels, searchRoot, {
      timeout: message.findTimeout || message.timeout || 5000,
      delayBefore: message.delayBefore || 0,
      waitState: message.waitState || 'Attached',
      index: message.index || 0
    });

    pageTrace('resolveTarget found tag=' + (element.tagName || '') + ' id=' + (element.id || ''));
    return element;
  }

  async function validateAfterAction(message, validate, validationLevels) {
    if (!validate || validate === 'None' || !validationLevels || !validationLevels.length) {
      return;
    }

    const waitBeforeValidate = message.waitBeforeValidate || 1000;
    const timeout = message.timeout || 15000;
    const started = Date.now();

    await sleep(waitBeforeValidate);
    while (Date.now() - started < timeout) {
      const matches = DomSelectorResolver.findElements(validationLevels, document);
      const exists = matches.length > 0;
      if ((validate === 'ElementAppear' && exists) || (validate === 'ElementDisappear' && !exists)) {
        return;
      }

      await sleep(100);
    }

    throw new Error('Click validation failed: ' + validate);
  }

  async function executeBridgeCommand(message) {
    switch (message.action) {
      case 'element.find':
      case 'element.findScoped':
        await resolveTarget(message);
        return {};

      case 'element.click':
      case 'element.doubleClick': {
        const element = await resolveTarget(message);
        const count = message.count || 1;
        const interval = message.interval || 0;
        for (let i = 0; i < count; i += 1) {
          if (message.action === 'element.doubleClick') {
            performElementDoubleClick(element, message.clickMethod);
          } else {
            performElementClick(element, message.clickMethod);
          }

          if (interval > 0 && i < count - 1) {
            await sleep(interval);
          }
        }

        await validateAfterAction(message, message.validate, message.validationSelectorLevels);
        return {};
      }

      case 'element.input': {
        const element = await resolveTarget(message);
        const value = message.value || '';
        if (message.inputMethod === 'Type') {
          element.focus();
          element.value = '';
          for (const char of value) {
            element.value += char;
            dispatchInputEvents(element);
            if (message.typeDelay) {
              await sleep(message.typeDelay);
            }
          }
        } else {
          element.focus();
          element.value = value;
          dispatchInputEvents(element);
        }

        return {};
      }

      case 'element.select': {
        const element = await resolveTarget(message);
        if (element.tagName.toLowerCase() !== 'select') {
          throw new Error('Select is only supported on select elements.');
        }

        const valType = message.valType || 'Value';
        if (valType === 'Text') {
          for (const text of message.texts || []) {
            for (const option of element.options) {
              if (option.text === text) {
                option.selected = true;
              }
            }
          }
        } else if (valType === 'Index') {
          for (const idx of message.indices || []) {
            if (element.options[idx]) {
              element.options[idx].selected = true;
            }
          }
        } else {
          for (const value of message.values || []) {
            for (const option of element.options) {
              if (option.value === value) {
                option.selected = true;
              }
            }
          }
        }

        dispatchInputEvents(element);
        return {};
      }

      case 'element.check':
      case 'element.uncheck': {
        const element = await resolveTarget(message);
        element.checked = message.action === 'element.check';
        dispatchInputEvents(element);
        return {};
      }

      case 'element.isChecked': {
        const element = await resolveTarget(message);
        return { checked: !!element.checked };
      }

      case 'element.getText': {
        pageTrace('element.getText start');
        const element = await resolveTarget(message);
        const tag = (element.tagName || '').toLowerCase();
        const text = tag === 'input' || tag === 'textarea'
          ? (element.value || '')
          : (element.innerText || element.textContent || '');
        const result = text.trim();
        pageTrace('element.getText done length=' + result.length);
        return { text: result };
      }

      case 'element.getAttribute': {
        const element = await resolveTarget(message);
        return { value: element.getAttribute(message.name || '') || '' };
      }

      case 'element.getInputValue': {
        const element = await resolveTarget(message);
        return { value: element.value || '' };
      }

      case 'element.getSelected': {
        const element = await resolveTarget(message);
        return {
          selected: Array.from(element.selectedOptions || []).map((option) => option.value)
        };
      }

      case 'element.getRect': {
        const element = await resolveTarget(message);
        return getElementRect(element);
      }

      case 'element.getParent': {
        let element = await resolveTarget(message);
        const level = message.level || 1;
        for (let i = 0; i < level; i += 1) {
          if (!element.parentElement) {
            break;
          }
          element = element.parentElement;
        }

        return getElementRect(element);
      }

      case 'element.getChildren': {
        const parent = await resolveTarget(message);
        const childLevels = message.childSelectorLevels || [];
        const deepdive = !!message.deepdive;
        const root = deepdive ? parent : parent;
        const children = DomSelectorResolver.findElements(childLevels, root);
        return { count: children.length };
      }

      case 'element.setAttribute': {
        const element = await resolveTarget(message);
        element.setAttribute(message.name, message.value);
        return {};
      }

      case 'tab.runJs':
        throw new Error('tab.runJs is handled by the background script.');

      case 'element.runJs': {
        const element = await resolveTarget(message);
        const script = String(message.script || '').trim();
        if (/^return\s+this\.innerText\s*;?\s*$/i.test(script)) {
          return { result: (element.innerText || element.textContent || '').trim() };
        }

        if (/^return\s+this\.textContent\s*;?\s*$/i.test(script)) {
          return { result: (element.textContent || '').trim() };
        }

        if (/^return\s+this\.value\s*;?\s*$/i.test(script)) {
          return { result: element.value || '' };
        }

        throw new Error('Unsupported element.runJs script under extension CSP.');
      }

      case 'element.sendKeys':
      case 'tab.sendKeys': {
        const element = message.action === 'element.sendKeys' ? await resolveTarget(message) : document.body;
        element.focus();
        const tag = (element.tagName || '').toLowerCase();
        const isTextInput = tag === 'input' || tag === 'textarea';
        const inputType = (element.getAttribute('type') || 'text').toLowerCase();
        const acceptsText = isTextInput && !['checkbox', 'radio', 'button', 'submit', 'reset', 'file', 'hidden', 'image'].includes(inputType);

        for (const key of String(message.keys || '')) {
          if (acceptsText) {
            element.value += key;
            dispatchInputEvents(element);
          }

          element.dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true }));
          element.dispatchEvent(new KeyboardEvent('keyup', { key, bubbles: true }));
          if (message.delay) {
            await sleep(message.delay);
          }
        }

        return {};
      }

      case 'element.takeScreenshot': {
        const element = await resolveTarget(message);
        const rect = element.getBoundingClientRect();
        return {
          rect: {
            x: rect.x,
            y: rect.y,
            width: rect.width,
            height: rect.height
          }
        };
      }

      case 'tab.elementExists': {
        let searchRoot = document;
        const frameLevels = message.frameSelectorLevels || [];
        if (frameLevels.length > 0) {
          searchRoot = await DomSelectorResolver.resolveSearchRootAsync(
            frameLevels,
            document,
            message.timeout || 10000
          );
        }

        const matches = DomSelectorResolver.findElements(message.selectorLevels || [], searchRoot);
        const index = message.index || 0;
        return { exists: matches.length > index };
      }

      case 'tab.findElements': {
        let searchRoot = document;
        const frameLevels = message.frameSelectorLevels || [];
        if (frameLevels.length > 0) {
          searchRoot = await DomSelectorResolver.resolveSearchRootAsync(
            frameLevels,
            document,
            message.timeout || 10000
          );
        }

        const matches = DomSelectorResolver.findElements(message.selectorLevels || [], searchRoot);
        return { count: matches.length };
      }

      case 'element.findElements': {
        let searchRoot = document;
        const frameLevels = message.frameSelectorLevels || [];
        if (frameLevels.length > 0) {
          searchRoot = await DomSelectorResolver.resolveSearchRootAsync(
            frameLevels,
            document,
            message.timeout || 10000
          );
        }

        const parentMatches = DomSelectorResolver.findElements(message.selectorLevels || [], searchRoot);
        const parentIndex = message.index || 0;
        const parent = parentMatches[parentIndex];
        if (!parent) {
          return { count: 0 };
        }

        const levels = message.scopedSelectorLevels && message.scopedSelectorLevels.length
          ? message.scopedSelectorLevels
          : message.selectorLevels || [];
        const matches = DomSelectorResolver.findElements(levels, parent);
        return { count: matches.length };
      }

      case 'tab.parallelFindElement':
      case 'element.parallelFindElement': {
        const selectorSets = message.selectors || (message.selectorSets || []).map((levels) => ({ selectorLevels: levels }));
        const timeout = message.timeout || 15000;
        const waitState = message.waitState || 'Attached';
        const started = Date.now();
        let searchRoot = document;

        if (message.action === 'element.parallelFindElement') {
          searchRoot = await resolveTarget(message);
        } else {
          const frameLevels = message.frameSelectorLevels || [];
          if (frameLevels.length > 0) {
            searchRoot = await DomSelectorResolver.resolveSearchRootAsync(
              frameLevels,
              document,
              timeout
            );
          }
        }

        while (Date.now() - started < timeout) {
          for (let i = 0; i < selectorSets.length; i += 1) {
            const levels = selectorSets[i]?.selectorLevels || selectorSets[i] || [];
            const matches = DomSelectorResolver.findElements(levels, searchRoot);
            if (matches[0] && DomSelectorResolver.matchWaitState(matches[0], waitState)) {
              return { matchedIndex: i };
            }
          }

          await sleep(100);
        }

        return { matchedIndex: -1 };
      }

      case 'tab.getStorage':
      case 'browser.getStorage': {
        const scope = message.scope === 'session' ? sessionStorage : localStorage;
        const items = {};
        for (let i = 0; i < scope.length; i += 1) {
          const key = scope.key(i);
          items[key] = scope.getItem(key);
        }

        return { items };
      }

      case 'inspector.startPick': {
        if (!globalThis.F2bInspectorPick) {
          throw new Error('Inspector pick module is not loaded.');
        }

        return globalThis.F2bInspectorPick.startAssist();
      }

      case 'inspector.stopPick': {
        if (globalThis.F2bInspectorPick) {
          globalThis.F2bInspectorPick.stopAssist();
        }

        return {};
      }

      case 'inspector.startPickAssist':
        if (!globalThis.F2bInspectorPick) {
          throw new Error('Inspector pick module is not loaded.');
        }

        return globalThis.F2bInspectorPick.startAssist();

      case 'inspector.stopPickAssist':
        if (globalThis.F2bInspectorPick) {
          globalThis.F2bInspectorPick.stopAssist();
        }

        return {};

      case 'inspector.pausePickAssist':
        if (!globalThis.F2bInspectorPick) {
          throw new Error('Inspector pick module is not loaded.');
        }

        return globalThis.F2bInspectorPick.pauseAssist();

      case 'inspector.resumePickAssist':
        if (!globalThis.F2bInspectorPick) {
          throw new Error('Inspector pick module is not loaded.');
        }

        return globalThis.F2bInspectorPick.resumeAssist();

      case 'inspector.restartPickAssist':
        if (!globalThis.F2bInspectorPick) {
          throw new Error('Inspector pick module is not loaded.');
        }

        globalThis.F2bInspectorPick.stopAssist();
        return globalThis.F2bInspectorPick.startAssist();

      case 'inspector.hoverAtScreenPoint':
        if (!globalThis.F2bInspectorPick) {
          throw new Error('Inspector pick module is not loaded.');
        }

        return globalThis.F2bInspectorPick.hoverAtScreenPoint(message.screenX, message.screenY);

      case 'inspector.pickAtScreenPoint':
        if (!globalThis.F2bInspectorPick) {
          throw new Error('Inspector pick module is not loaded.');
        }

        return globalThis.F2bInspectorPick.pickAtScreenPoint(message.screenX, message.screenY);

      case 'inspector.pausePick': {
        if (!globalThis.F2bInspectorPick) {
          throw new Error('Inspector pick module is not loaded.');
        }

        return { paused: globalThis.F2bInspectorPick.pauseAssist().paused };
      }

      case 'inspector.resumePick': {
        if (!globalThis.F2bInspectorPick) {
          throw new Error('Inspector pick module is not loaded.');
        }

        return { resumed: globalThis.F2bInspectorPick.resumeAssist().resumed };
      }

      case 'inspector.buildSelector': {
        const builder = globalThis.F2bInspectorBuilder;
        if (!builder) {
          throw new Error('Inspector builder module is not loaded.');
        }

        const element = message.segments
          ? builder.resolveBySegments(message.segments)
          : null;
        if (!element) {
          throw new Error('Element not found for selector build.');
        }

        const tabTitle = message.tabTitle || document.title;
        const tabUrl = message.tabUrl || location.href;
        const fullLevels = builder.buildAutoSelectorLevelsFromElement(element, tabTitle, tabUrl);
        const minimalLevels = builder.buildMinimalSelectorLevelsFromElement(element, tabTitle, tabUrl);

        return {
          levels: fullLevels,
          minimalLevels: minimalLevels,
          segments: builder.getSegmentsFromElement(element),
          displayName: builder.buildDisplayName(element)
        };
      }

      case 'inspector.describe': {
        const builder = globalThis.F2bInspectorBuilder;
        if (!builder) {
          throw new Error('Inspector builder module is not loaded.');
        }

        const target = builder.resolveBySegments(message.segments);
        if (!target) {
          throw new Error('Element not found.');
        }

        return {
          properties: builder.describeElement(target),
          displayName: builder.buildDisplayName(target)
        };
      }

      case 'inspector.highlight': {
        const builder = globalThis.F2bInspectorBuilder;
        if (!builder) {
          throw new Error('Inspector builder module is not loaded.');
        }

        const target = message.segments
          ? builder.resolveBySegments(message.segments)
          : await resolveTarget(message);
        if (!target) {
          throw new Error('Element not found for highlight.');
        }

        builder.highlightElement(target, message.durationMs || 3000);
        return { highlighted: true };
      }

      case 'inspector.getDomChildren': {
        const builder = globalThis.F2bInspectorBuilder;
        if (!builder) {
          throw new Error('Inspector builder module is not loaded.');
        }

        const segments = message.segments || message.frameSegments || [];
        return {
          nodes: builder.getDomChildNodes(segments, message.maxChildren || 500)
        };
      }

      case 'inspector.validateProbe': {
        const builder = globalThis.F2bInspectorBuilder;
        const dom = globalThis.DomSelectorResolver;
        if (!builder || !dom) {
          throw new Error('Inspector modules are not loaded.');
        }

        const segments = message.segments || [];
        const frameLevels = message.frameSelectorLevels || [];
        const selectorLevels = message.selectorLevels || [];
        let searchRoot = document;
        let frameError = '';

        if (frameLevels.length > 0) {
          try {
            searchRoot = dom.resolveSearchRoot(frameLevels, document);
          } catch (error) {
            frameError = error.message || String(error);
          }
        }

        const segmentElement = segments.length ? builder.resolveBySegments(segments) : null;
        let selectorMatchCount = 0;
        let selectorError = '';
        try {
          selectorMatchCount = dom.findElements(selectorLevels, searchRoot).length;
        } catch (error) {
          selectorError = error.message || String(error);
        }

        const enabledLevels = dom.parseSelectorLevels(selectorLevels).filter((level) => level.isEnabled !== false);
        const idProperty = (enabledLevels[enabledLevels.length - 1]?.properties || []).find(
          (property) => property.isSelected !== false && (property.name === 'id' || property.name === 'AutomationId')
        );
        const probeId = idProperty ? idProperty.value : '';
        const byIdElement = probeId ? document.getElementById(probeId) : null;

        return {
          documentTitle: document.title || '',
          locationHref: location.href || '',
          segmentResolved: !!segmentElement,
          segmentElementId: segmentElement ? (segmentElement.id || '') : '',
          segmentElementTag: segmentElement ? (segmentElement.tagName || '') : '',
          selectorMatchCount: selectorMatchCount,
          selectorError: selectorError,
          frameError: frameError,
          getElementByIdFound: !!byIdElement,
          getElementByIdTag: byIdElement ? (byIdElement.tagName || '') : '',
          probeId: probeId
        };
      }

      default:
        throw new Error('Unsupported page action: ' + message.action);
    }
  }

  globalThis.__f2bExecuteBridgeCommand = executeBridgeCommand;

  chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (!message || message.type !== 'bridge-page-command') {
      return false;
    }

    executeBridgeCommand(message)
      .then((data) => sendResponse({ success: true, data: data || {} }))
      .catch((error) => sendResponse({ success: false, error: error.message || String(error) }));

    return true;
  });
})();
