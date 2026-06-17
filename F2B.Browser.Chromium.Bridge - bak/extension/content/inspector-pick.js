(function (root) {
  const FLAUI_BORDER = '4px solid rgba(255, 165, 0, 0.94)';
  const FLAUI_FILL = 'rgba(173, 216, 230, 0.3)';

  let assistActive = root.__f2bPickAssistActive === true;
  let assistPaused = root.__f2bPickAssistPaused === true;
  let hoverOverlay = null;

  function chromeInsets() {
    return {
      left: Math.max(0, (window.outerWidth - window.innerWidth) / 2),
      top: Math.max(0, window.outerHeight - window.innerHeight)
    };
  }

  function viewportPointFromScreen(screenX, screenY) {
    const dpr = window.devicePixelRatio || 1;
    const chrome = chromeInsets();
    return {
      x: screenX / dpr - window.screenX - chrome.left,
      y: screenY / dpr - window.screenY - chrome.top
    };
  }

  function boundsToScreen(rect) {
    const dpr = window.devicePixelRatio || 1;
    const chrome = chromeInsets();
    return {
      x: Math.round((window.screenX + chrome.left + rect.left) * dpr),
      y: Math.round((window.screenY + chrome.top + rect.top) * dpr),
      width: Math.max(1, Math.round(rect.width * dpr)),
      height: Math.max(1, Math.round(rect.height * dpr))
    };
  }

  function isFrameElement(element) {
    const tag = element && element.tagName ? element.tagName.toLowerCase() : '';
    return tag === 'iframe' || tag === 'frame';
  }

  function boundsInTopViewport(element) {
    if (!element || !element.getBoundingClientRect) {
      return null;
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
        height: rect.height,
        right: rect.left + frameRect.left + rect.width,
        bottom: rect.top + frameRect.top + rect.height
      };
      doc = frame.ownerDocument;
    }

    return rect;
  }

  function elementAtViewportPoint(viewportX, viewportY, doc, hostWindow) {
    doc = doc || document;
    hostWindow = hostWindow || window;

    if (viewportX < 0 || viewportY < 0) {
      return null;
    }

    const hostWidth = hostWindow.innerWidth || doc.documentElement.clientWidth || 0;
    const hostHeight = hostWindow.innerHeight || doc.documentElement.clientHeight || 0;
    if (viewportX > hostWidth || viewportY > hostHeight) {
      return null;
    }

    let element = doc.elementFromPoint(viewportX, viewportY);
    if (!element || element.id === '__f2bInspectorHover') {
      return null;
    }

    if (!isFrameElement(element)) {
      return element;
    }

    let childDoc = null;
    try {
      childDoc = element.contentDocument;
    } catch (error) {
      return element;
    }

    if (!childDoc || !childDoc.defaultView) {
      return element;
    }

    const frameRect = element.getBoundingClientRect();
    const childX = viewportX - frameRect.left;
    const childY = viewportY - frameRect.top;
    if (childX < 0 || childY < 0 || childX > frameRect.width || childY > frameRect.height) {
      return element;
    }

    const inner = elementAtViewportPoint(childX, childY, childDoc, childDoc.defaultView);
    return inner || element;
  }

  function elementAtScreenPoint(screenX, screenY) {
    const point = viewportPointFromScreen(screenX, screenY);
    if (point.x < 0 || point.y < 0 || point.x > window.innerWidth || point.y > window.innerHeight) {
      return null;
    }

    return elementAtViewportPoint(point.x, point.y);
  }

  function setAssistActive(active) {
    assistActive = active === true;
    root.__f2bPickAssistActive = assistActive;
  }

  function setAssistPaused(paused) {
    assistPaused = paused === true;
    root.__f2bPickAssistPaused = assistPaused;
  }

  function clearHoverOverlay() {
    if (hoverOverlay && hoverOverlay.parentNode) {
      hoverOverlay.parentNode.removeChild(hoverOverlay);
    }

    hoverOverlay = null;
  }

  function updateHoverOverlay(element) {
    const rect = boundsInTopViewport(element);
    if (!rect) {
      clearHoverOverlay();
      return;
    }

    if (!hoverOverlay) {
      hoverOverlay = document.createElement('div');
      hoverOverlay.id = '__f2bInspectorHover';
      hoverOverlay.style.position = 'fixed';
      hoverOverlay.style.pointerEvents = 'none';
      hoverOverlay.style.zIndex = '2147483647';
      hoverOverlay.style.boxSizing = 'border-box';
      hoverOverlay.style.border = FLAUI_BORDER;
      hoverOverlay.style.background = FLAUI_FILL;
      document.documentElement.appendChild(hoverOverlay);
    }

    hoverOverlay.style.left = rect.left + 'px';
    hoverOverlay.style.top = rect.top + 'px';
    hoverOverlay.style.width = Math.max(1, rect.width) + 'px';
    hoverOverlay.style.height = Math.max(1, rect.height) + 'px';
  }

  function buildPickResult(target) {
    const builder = root.F2bInspectorBuilder;
    if (!builder || !target) {
      return null;
    }

    const tabTitle = document.title;
    const tabUrl = location.href;

    return {
      segments: builder.getSegmentsFromElement(target),
      levels: builder.buildSelectorLevelsFromElement(target, tabTitle, tabUrl),
      displayName: builder.buildDisplayName(target),
      tabTitle: tabTitle,
      tabUrl: tabUrl
    };
  }

  function startAssist() {
    setAssistActive(true);
    setAssistPaused(false);
    clearHoverOverlay();
    return { active: true };
  }

  function stopAssist() {
    setAssistActive(false);
    setAssistPaused(false);
    clearHoverOverlay();
    return { active: false };
  }

  function pauseAssist() {
    if (!assistActive) {
      return { paused: false };
    }

    setAssistPaused(true);
    clearHoverOverlay();
    return { paused: true };
  }

  function resumeAssist() {
    if (!assistActive || !assistPaused) {
      return { resumed: false };
    }

    setAssistPaused(false);
    return { resumed: true };
  }

  function hoverAtScreenPoint(screenX, screenY) {
    if (!assistActive || assistPaused) {
      return { hovered: false };
    }

    const target = elementAtScreenPoint(screenX, screenY);
    if (!target) {
      clearHoverOverlay();
      return { hovered: false };
    }

    updateHoverOverlay(target);
    const rect = boundsInTopViewport(target);
    if (!rect) {
      return { hovered: false };
    }

    return {
      hovered: true,
      bounds: boundsToScreen(rect)
    };
  }

  function pickAtScreenPoint(screenX, screenY) {
    if (!assistActive || assistPaused) {
      return { segments: [] };
    }

    const target = elementAtScreenPoint(screenX, screenY);
    const result = buildPickResult(target);
    if (!result) {
      return { segments: [] };
    }

    clearHoverOverlay();
    return result;
  }

  root.F2bInspectorPick = {
    startAssist: startAssist,
    stopAssist: stopAssist,
    pauseAssist: pauseAssist,
    resumeAssist: resumeAssist,
    hoverAtScreenPoint: hoverAtScreenPoint,
    pickAtScreenPoint: pickAtScreenPoint
  };
})(typeof globalThis !== 'undefined' ? globalThis : self);
