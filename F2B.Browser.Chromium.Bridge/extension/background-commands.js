/* global chrome, isSocketOpen, reportCommandResult, instanceId, runBridgePageCommandInTab, sendToHost, createRpcTrace */

function logPageTrace(requestId, pageTrace) {
  if (!Array.isArray(pageTrace)) {
    return;
  }

  for (const item of pageTrace) {
    createRpcTrace(requestId)(`page/${item.step} @${item.elapsedMs}ms`);
  }
}

function stringsEqualIgnoreCase(left, right) {
  return String(left || '').toLowerCase() === String(right || '').toLowerCase();
}

function urlsMatchForSwitch(tabUrl, expectedUrl) {
  if (!expectedUrl) {
    return true;
  }

  if (!tabUrl) {
    return false;
  }

  if (stringsEqualIgnoreCase(tabUrl, expectedUrl)) {
    return true;
  }

  const left = normalizeUrlForMatch(tabUrl);
  const right = normalizeUrlForMatch(expectedUrl);
  if (left === right) {
    return true;
  }

  if (!left.startsWith('file://') || !right.startsWith('file://')) {
    return false;
  }

  try {
    const leftPath = decodeURIComponent(new URL(left).pathname).replace(/\//g, '\\').toLowerCase();
    const rightPath = decodeURIComponent(new URL(right).pathname).replace(/\//g, '\\').toLowerCase();
    return leftPath === rightPath;
  } catch (error) {
    return urlsRoughlyMatch(tabUrl, expectedUrl);
  }
}

function tabMatchesSwitchCriteria(tab, message) {
  if (message.title && !stringsEqualIgnoreCase(tab.title, message.title)) {
    return false;
  }

  if (message.titleRe && !new RegExp(message.titleRe, 'i').test(tab.title || '')) {
    return false;
  }

  if (message.url && !urlsMatchForSwitch(tab.url, message.url)) {
    return false;
  }

  if (message.urlRe && !new RegExp(message.urlRe, 'i').test(tab.url || '')) {
    return false;
  }

  return true;
}

function pickMatchingTab(matches, matchIndex) {
  if (!matches || matches.length === 0) {
    return null;
  }

  const sorted = matches.slice().sort((a, b) => a.id - b.id);
  if (matchIndex != null && matchIndex >= 0 && matchIndex < sorted.length) {
    return sorted[matchIndex];
  }

  if (matches.length === 1) {
    return matches[0];
  }

  const active = matches.find((tab) => tab.active);
  if (active) {
    return active;
  }

  return matches.reduce((best, tab) => (tab.id > best.id ? tab : best));
}

async function switchToTab(tab) {
  await chrome.tabs.update(tab.id, { active: true });
  await chrome.windows.update(tab.windowId, { focused: true });
  return { tabId: tab.id };
}

function waitForNewTab(existingIds, timeoutMs) {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => {
      chrome.tabs.onCreated.removeListener(onCreated);
      reject(new Error('No new tab opened within timeout.'));
    }, timeoutMs);

    function onCreated(tab) {
      if (existingIds.has(tab.id)) {
        return;
      }

      clearTimeout(timer);
      chrome.tabs.onCreated.removeListener(onCreated);
      resolve(tab);
    }

    chrome.tabs.onCreated.addListener(onCreated);
  });
}

function waitForDownloadComplete(timeoutMs) {
  return new Promise((resolve, reject) => {
    let downloadId = null;
    let settled = false;

    const timer = setTimeout(cleanup, timeoutMs);

    function cleanup() {
      if (settled) {
        return;
      }

      settled = true;
      clearTimeout(timer);
      chrome.downloads.onCreated.removeListener(onCreated);
      chrome.downloads.onChanged.removeListener(onChanged);
    }

    function fail(error) {
      cleanup();
      reject(error);
    }

    function onCreated(item) {
      downloadId = item.id;
    }

    function onChanged(delta) {
      if (downloadId == null || delta.id !== downloadId) {
        return;
      }

      if (delta.error) {
        fail(new Error('Download failed: ' + delta.error.current));
        return;
      }

      if (delta.state && delta.state.current === 'complete') {
        chrome.downloads.search({ id: downloadId }, (items) => {
          cleanup();
          if (!items || !items[0]) {
            reject(new Error('Download completed but item was not found.'));
            return;
          }

          resolve(items[0]);
        });
      }
    }

    chrome.downloads.onCreated.addListener(onCreated);
    chrome.downloads.onChanged.addListener(onChanged);
  });
}

function arrayBufferToBase64(buffer) {
  let binary = '';
  const bytes = new Uint8Array(buffer);
  for (let i = 0; i < bytes.length; i += 1) {
    binary += String.fromCharCode(bytes[i]);
  }

  return btoa(binary);
}

async function clickForNewTab(message) {
  const timeout = message.timeout || 15000;
  const beforeTabs = await chrome.tabs.query({});
  const beforeIds = new Set(beforeTabs.map((tab) => tab.id));
  const newTabPromise = waitForNewTab(beforeIds, timeout);

  await runPageCommand(Object.assign({}, message, { action: 'element.click' }));
  const newTab = await newTabPromise;
  await waitForTabComplete(newTab.id, timeout);
  return { tabId: newTab.id };
}

async function readFileDownloadLink(message, tab) {
  const hrefData = await runPageCommand(Object.assign({}, message, {
    action: 'element.getAttribute',
    name: 'href'
  }));

  const href = hrefData.value || '';
  if (!href) {
    throw new Error('Download link has no href attribute.');
  }

  let absoluteUrl = href;
  try {
    absoluteUrl = new URL(href, tab.url || '').href;
  } catch (error) {
    absoluteUrl = href;
  }

  let item = null;
  try {
    const response = await fetch(absoluteUrl);
    if (!response.ok) {
      throw new Error('Failed to fetch download URL: HTTP ' + response.status);
    }

    const buffer = await response.arrayBuffer();
    item = {
      url: absoluteUrl,
      fileName: absoluteUrl.split('/').pop().split('?')[0].split('#')[0] || 'download.bin',
      base64: arrayBufferToBase64(buffer)
    };
  } catch (fetchError) {
    const results = await chrome.scripting.executeScript({
      target: { tabId: tab.id },
      func: (url) => {
        let binary = '';
        const xhr = new XMLHttpRequest();
        xhr.open('GET', url, false);
        try {
          xhr.send(null);
        } catch (error) {
          throw new Error('Failed to read download URL: ' + (error.message || error));
        }

        if (xhr.status !== 0 && xhr.status !== 200) {
          throw new Error('Failed to read download URL: HTTP ' + xhr.status);
        }

        const text = xhr.responseText || '';
        for (let i = 0; i < text.length; i += 1) {
          binary += String.fromCharCode(text.charCodeAt(i) & 0xff);
        }

        const fileName = url.split('/').pop().split('?')[0].split('#')[0] || 'download.bin';
        return {
          url: url,
          fileName: fileName,
          base64: btoa(binary)
        };
      },
      args: [absoluteUrl]
    });

    if (!results || !results[0] || !results[0].result) {
      throw new Error('Failed to read download content from file page: ' + (fetchError.message || fetchError));
    }

    item = results[0].result;
  }

  if (!item) {
    throw new Error('Failed to read download content from file page.');
  }
  return {
    url: item.url,
    suggestedFileName: item.fileName,
    savedPath: message.saveAsPath || '',
    fileBase64: message.saveAsPath ? item.base64 : null
  };
}

async function clickForDownload(message) {
  const timeout = message.timeout || 60000;
  const saveAsPath = message.saveAsPath || '';
  const tab = await resolveTargetTab(message);

  if ((tab.url || '').startsWith('file://')) {
    return readFileDownloadLink(message, tab);
  }

  const downloadPromise = waitForDownloadComplete(timeout);

  await runPageCommand(Object.assign({}, message, { action: 'element.click' }));
  const item = await downloadPromise;
  const url = item.finalUrl || item.url || '';
  const suggestedFileName = (item.filename || '').split(/[/\\]/).pop() || 'download.bin';

  let fileBase64 = null;
  if (saveAsPath && url) {
    const response = await fetch(url);
    if (!response.ok) {
      throw new Error('Failed to fetch downloaded content: ' + response.status);
    }

    fileBase64 = arrayBufferToBase64(await response.arrayBuffer());
  }

  return {
    url: url,
    suggestedFileName: suggestedFileName,
    savedPath: saveAsPath,
    fileBase64: fileBase64
  };
}

function normalizeTabPropertyName(name) {
  const key = String(name || '').toLowerCase();
  if (key === 'title' || key === 'name') {
    return 'title';
  }

  if (key === 'url') {
    return 'url';
  }

  if (key === 'idx' || key === 'indexinparent') {
    return 'idx';
  }

  return key;
}

function matchTabProperty(tab, property) {
  if (!property || property.isSelected === false) {
    return true;
  }

  const normalized = normalizeTabPropertyName(property.name);
  if (normalized === 'idx') {
    return true;
  }

  const expected = property.value || '';
  const actualMap = {
    title: tab.title || '',
    url: tab.url || ''
  };

  const actual = actualMap[normalized];
  if (actual == null) {
    return true;
  }

  if (property.isRegex) {
    try {
      return new RegExp(expected, 'i').test(actual);
    } catch (error) {
      return false;
    }
  }

  if (normalized === 'title') {
    return stringsEqualIgnoreCase(actual, expected);
  }

  return actual === expected;
}

function matchTabLevel(tab, level) {
  return (level.properties || []).every((property) => matchTabProperty(tab, property));
}

async function resolveTargetTab(message) {
  if (message.tabId) {
    try {
      return await chrome.tabs.get(message.tabId);
    } catch (error) {
      throw new Error('Target tab not found: ' + message.tabId);
    }
  }

  const tabLevels = message.tabSelectorLevels || [];
  const tabs = await chrome.tabs.query({});

  if (tabLevels.length > 0) {
    const level = tabLevels[0];
    const matched = tabs.filter((tab) => matchTabLevel(tab, level) && isInjectableUrl(tab.url));
    const indexProperty = (level.properties || []).find(
      (property) => property.isSelected !== false && property.name === 'IndexInParent'
    );
    const matchIndex = indexProperty ? parseInt(indexProperty.value, 10) : null;
    const target = pickMatchingTab(
      matched,
      Number.isFinite(matchIndex) ? matchIndex : null
    );
    if (target) {
      return target;
    }
  }

  const focused = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
  if (focused[0] && isInjectableUrl(focused[0].url)) {
    return focused[0];
  }

  const injectable = tabs.find((tab) => isInjectableUrl(tab.url));
  if (injectable) {
    return injectable;
  }

  throw new Error('No injectable tab found for command.');
}

async function ensureContentScript(tabId, frameId = 0) {
  const target = frameId === 0 ? { tabId: tabId } : { tabId: tabId, frameIds: [frameId] };

  try {
    await chrome.scripting.executeScript({
      target: target,
      files: ['content/dom-selector.js', 'content/inspector-builder.js', 'content/inspector-pick.js', 'content/bridge-executor.js']
    });
  } catch (error) {
    if (isRestrictedUrlError(error)) {
      throw new Error('RESTRICTED_URL:' + (error.message || error));
    }

    throw error;
  }
}

function isRestrictedUrlError(error) {
  const message = String((error && error.message) || error || '');
  return message.includes('Cannot access a chrome:// URL') ||
    message.includes('Cannot access contents of url') ||
    message.includes('Cannot access contents') ||
    message.includes('RESTRICTED_URL:');
}

function createInspectorPickCancelled(reason) {
  return {
    segments: [],
    cancelled: true,
    reason: reason || ''
  };
}

async function runInspectorPageCommand(message) {
  const tab = message.tabId ? await chrome.tabs.get(message.tabId) : await resolveTargetTab(message);
  if (!isInjectableUrl(tab.url)) {
    return createInspectorPickCancelled('restricted-url');
  }

  await chrome.tabs.update(tab.id, { active: true });
  await chrome.windows.update(tab.windowId, { focused: true });
  message.tabTitle = tab.title || '';
  message.tabUrl = tab.url || '';

  try {
    return await runPageCommand(message);
  } catch (error) {
    if (isRestrictedUrlError(error) || String(error.message || '').indexOf('RESTRICTED_URL:') === 0) {
      return createInspectorPickCancelled('restricted-url');
    }

    throw error;
  }
}

async function isInspectorPickLoaded(tabId, frameId = 0) {
  const target = frameId === 0 ? { tabId: tabId } : { tabId: tabId, frameIds: [frameId] };

  try {
    const results = await chrome.scripting.executeScript({
      target: target,
      func: () => typeof globalThis.F2bInspectorPick === 'object' &&
        typeof globalThis.F2bInspectorPick.startAssist === 'function'
    });

    return !!(results && results[0] && results[0].result === true);
  } catch (error) {
    return false;
  }
}

async function ensureInspectorPickScript(tabId, frameId = 0) {
  if (await isBridgeExecutorLoaded(tabId, frameId) && await isInspectorPickLoaded(tabId, frameId)) {
    return;
  }

  await ensureContentScript(tabId, frameId);
}

function isInspectorPickPageAction(action) {
  return action === 'inspector.startPickAssist' ||
    action === 'inspector.restartPickAssist' ||
    action === 'inspector.stopPickAssist' ||
    action === 'inspector.pausePickAssist' ||
    action === 'inspector.resumePickAssist' ||
    action === 'inspector.hoverAtScreenPoint' ||
    action === 'inspector.pickAtScreenPoint';
}

async function restartInspectorPickAssist(message) {
  const tab = message.tabId ? await chrome.tabs.get(message.tabId) : await resolveTargetTab(message);
  if (!isInjectableUrl(tab.url)) {
    throw new Error('RESTRICTED_URL:' + (tab.url || ''));
  }

  message.tabId = tab.id;
  message.tabTitle = tab.title || '';
  message.tabUrl = tab.url || '';

  try {
    await runInspectorQuietPageCommand(Object.assign({}, message, { action: 'inspector.stopPickAssist' }));
  } catch (error) {
    // Pick module may be missing after navigation; continue with reinject.
  }

  await ensureContentScript(tab.id);
  return runInspectorQuietPageCommand(Object.assign({}, message, { action: 'inspector.startPickAssist' }));
}

async function isBridgeExecutorLoaded(tabId, frameId = 0) {
  const target = frameId === 0 ? { tabId: tabId } : { tabId: tabId, frameIds: [frameId] };

  try {
    const results = await chrome.scripting.executeScript({
      target: target,
      func: () => typeof globalThis.__f2bExecuteBridgeCommand === 'function'
    });

    return !!(results && results[0] && results[0].result === true);
  } catch (error) {
    return false;
  }
}

async function runInspectorQuietPageCommand(message) {
  const tab = message.tabId ? await chrome.tabs.get(message.tabId) : await resolveTargetTab(message);
  if (!isInjectableUrl(tab.url)) {
    throw new Error('RESTRICTED_URL:' + (tab.url || ''));
  }

  message.tabTitle = tab.title || '';
  message.tabUrl = tab.url || '';

  if (!(await isBridgeExecutorLoaded(tab.id))) {
    await ensureContentScript(tab.id);
  } else if (!(await isInspectorPickLoaded(tab.id))) {
    await ensureContentScript(tab.id);
  }

  const pageMessage = Object.assign({}, message, {
    type: 'bridge-page-command',
    tabId: tab.id,
    frameSelectorLevels: []
  });

  const trace = createRpcTrace(message.id || 'inspector-quiet');
  const response = await executeQuietPageCommandInTab(tab.id, pageMessage, trace, 0);
  if (!response || !response.success) {
    throw new Error(response?.error || 'Page command failed.');
  }

  return response.data || {};
}

async function executeQuietPageCommandInTab(tabId, pageMessage, trace, frameId = 0) {
  const log = typeof trace === 'function' ? trace : function () {};

  if (await isBridgeExecutorLoaded(tabId, frameId)) {
    try {
      const response = await chrome.tabs.sendMessage(tabId, pageMessage, { frameId: frameId });
      if (response && typeof response.success === 'boolean') {
        log('sendMessage ok frameId=' + frameId);
        return response;
      }
    } catch (error) {
      log('sendMessage fallback frameId=' + frameId + ': ' + (error.message || error));
    }
  }

  return executePageCommandInTab(tabId, pageMessage, trace, frameId);
}

function normalizeUrlForMatch(url) {
  if (!url) {
    return '';
  }

  try {
    return decodeURIComponent(String(url)).replace(/\\/g, '/').toLowerCase();
  } catch (error) {
    return String(url).replace(/\\/g, '/').toLowerCase();
  }
}

function urlsRoughlyMatch(left, right) {
  const a = normalizeUrlForMatch(left);
  const b = normalizeUrlForMatch(right);
  if (!a || !b) {
    return false;
  }

  if (a === b) {
    return true;
  }

  const fileName = (value) => value.split('/').pop().split('?')[0].split('#')[0];
  const leftName = fileName(a);
  const rightName = fileName(b);
  return leftName && leftName === rightName;
}

function findChildFrameByUrl(childFrames, expectedUrl) {
  if (!childFrames || childFrames.length === 0) {
    return null;
  }

  if (!expectedUrl) {
    return childFrames[0];
  }

  const exact = childFrames.find((frame) => urlsRoughlyMatch(frame.url, expectedUrl));
  if (exact) {
    return exact;
  }

  return childFrames.find((frame) => {
    const url = normalizeUrlForMatch(frame.url);
    return url && url !== 'about:blank';
  }) || null;
}

async function prepareLazyFramesInFrame(tabId, parentFrameId) {
  await ensureContentScript(tabId, parentFrameId);
  await chrome.scripting.executeScript({
    target: parentFrameId === 0 ? { tabId: tabId } : { tabId: tabId, frameIds: [parentFrameId] },
    func: () => {
      const doc = document.scrollingElement || document.documentElement || document.body;
      if (doc) {
        window.scrollTo({ top: doc.scrollHeight, behavior: 'auto' });
      }
    }
  });
  await sleep(200);
}

async function locateIframeMetaInFrame(tabId, parentFrameId, frameLevel) {
  await ensureContentScript(tabId, parentFrameId);
  const target = parentFrameId === 0 ? { tabId: tabId } : { tabId: tabId, frameIds: [parentFrameId] };
  const results = await chrome.scripting.executeScript({
    target: target,
    func: (level) => {
      const resolver = globalThis.DomSelectorResolver;
      if (!resolver || typeof resolver.findFrameCandidates !== 'function') {
        return null;
      }

      const matches = resolver.findFrameCandidates(document, level);
      if (!matches.length) {
        return null;
      }

      const frameElement = matches[0];
      if (typeof resolver.scrollFrameIntoView === 'function') {
        resolver.scrollFrameIntoView(frameElement);
      } else if (typeof frameElement.scrollIntoView === 'function') {
        frameElement.scrollIntoView({ block: 'center', inline: 'nearest' });
      }

      const src = frameElement.getAttribute('src') || '';
      let absolute = src;
      try {
        absolute = new URL(src, document.baseURI || window.location.href).href;
      } catch (error) {
        absolute = src;
      }

      // Do not assign frameElement.src here. On file:// pages the parent frame often
      // cannot read contentDocument, which caused false "not loaded" reload loops.
      return {
        src: absolute,
        name: frameElement.getAttribute('name') || frameElement.getAttribute('title') || ''
      };
    },
    args: [frameLevel]
  });

  return results && results[0] ? results[0].result : null;
}

async function resolveTargetFrameId(tabId, frameLevels, timeoutMs, trace) {
  if (!frameLevels || frameLevels.length === 0) {
    return 0;
  }

  const waitMs = Math.max(timeoutMs || 10000, 1000);
  let parentFrameId = 0;

  for (let index = 0; index < frameLevels.length; index += 1) {
    const level = frameLevels[index];
    const started = Date.now();
    let resolvedFrameId = null;

    while (Date.now() - started < waitMs && resolvedFrameId == null) {
      if (index === 0 && parentFrameId === 0) {
        await prepareLazyFramesInFrame(tabId, parentFrameId);
      }

      const iframeMeta = await locateIframeMetaInFrame(tabId, parentFrameId, level);
      if (iframeMeta) {
        const frames = await chrome.webNavigation.getAllFrames({ tabId: tabId });
        const childFrames = frames.filter((frame) => frame.parentFrameId === parentFrameId);
        const matched = findChildFrameByUrl(childFrames, iframeMeta.src);
        if (matched && matched.frameId != null) {
          resolvedFrameId = matched.frameId;
          trace('resolveTargetFrameId level=' + (index + 1) + ' frameId=' + resolvedFrameId + ' url=' + (matched.url || ''));
        } else if (iframeMeta) {
          trace('resolveTargetFrameId waiting level=' + (index + 1) +
            ' expected=' + (iframeMeta.src || '') +
            ' children=' + childFrames.map((frame) => frame.frameId + ':' + (frame.url || '')).join('|'));
        }
      } else if (parentFrameId === 0) {
        await chrome.scripting.executeScript({
          target: { tabId: tabId },
          func: () => {
            window.scrollBy(0, Math.min(window.innerHeight || 600, 600));
          }
        });
      }

      if (resolvedFrameId == null) {
        await sleep(100);
      }
    }

    if (resolvedFrameId == null) {
      throw new Error('Frame not found or not loaded for selector level ' + (index + 1) + '.');
    }

    parentFrameId = resolvedFrameId;
  }

  return parentFrameId;
}

async function runPageCommand(message) {
  const trace = createRpcTrace(message.id);
  trace('frameSelectorLevels=' + (message.frameSelectorLevels || []).length +
    ' selectorLevels=' + (message.selectorLevels || []).length);

  const tab = await resolveTargetTab(message);
  trace('resolveTargetTab tabId=' + tab.id + ' url=' + (tab.url || ''));

  if (!isInjectableUrl(tab.url)) {
    throw new Error('RESTRICTED_URL:' + (tab.url || ''));
  }

  await ensureContentScript(tab.id);
  trace('ensureContentScript done');

  const frameLevels = message.frameSelectorLevels || [];
  let targetFrameId = 0;
  if (frameLevels.length > 0) {
    targetFrameId = await resolveTargetFrameId(
      tab.id,
      frameLevels,
      message.findTimeout || message.timeout || 15000,
      trace
    );
  }

  const pageMessage = Object.assign({}, message, {
    type: 'bridge-page-command',
    tabId: tab.id,
    frameSelectorLevels: []
  });

  const response = await executePageCommandInTab(tab.id, pageMessage, trace, targetFrameId);
  trace('executePageCommandInTab done success=' + !!(response && response.success));

  if (response && response.pageTrace) {
    logPageTrace(message.id, response.pageTrace);
  }

  if (!response || !response.success) {
    throw new Error(response?.error || 'Page command failed.');
  }

  response.data = response.data || {};
  response.data.tabId = tab.id;
  return response.data;
}

async function executePageCommandInTab(tabId, pageMessage, trace, frameId = 0) {
  const log = typeof trace === 'function' ? trace : function () {};
  const target = frameId === 0 ? { tabId: tabId } : { tabId: tabId, frameIds: [frameId] };

  if (!(await isBridgeExecutorLoaded(tabId, frameId))) {
    await ensureContentScript(tabId, frameId);
  } else if (isInspectorPickPageAction(pageMessage.action) && !(await isInspectorPickLoaded(tabId, frameId))) {
    await ensureContentScript(tabId, frameId);
  }

  for (let attempt = 0; attempt < 2; attempt += 1) {
    if (attempt > 0) {
      log('executePageCommand retry attempt=' + attempt);
      await ensureContentScript(tabId, frameId);
    }

    try {
      log('executeScript attempt=' + attempt + ' frameId=' + frameId);
      const results = await chrome.scripting.executeScript({
        target: target,
        func: runBridgePageCommandInTab,
        args: [pageMessage]
      });

      if (!results || !results[0]) {
        log('executeScript empty result attempt=' + attempt);
        if (attempt === 0) {
          continue;
        }

        throw new Error('Page command returned no result.');
      }

      log('executeScript returned attempt=' + attempt);
      return results[0].result;
    } catch (error) {
      log('executeScript error attempt=' + attempt + ': ' + (error.message || error));
      if (attempt === 1) {
        throw error;
      }
    }
  }

  throw new Error('Page command returned no result.');
}

async function executeBridgeCommand(message) {
  switch (message.action) {
    case 'alert':
      return showAlert(message.message || 'Hello from F2B Bridge');

    case 'browser.newTab': {
      const tab = await chrome.tabs.create({
        url: message.url || 'about:blank',
        active: true
      });

      if (message.url) {
        await waitForTabComplete(tab.id, message.timeout || 15000);
      }

      return { tabId: tab.id };
    }

    case 'browser.getAllTabs': {
      const tabs = await chrome.tabs.query({});
      return {
        tabs: tabs.map((tab, index) => ({
          tabId: tab.id,
          windowId: tab.windowId,
          url: tab.url,
          title: tab.title,
          active: tab.active,
          index: index
        }))
      };
    }

    case 'browser.getActivatedTab':
    case 'browser.getLatestTab': {
      const tabs = await chrome.tabs.query({ currentWindow: true });
      const tab = message.action === 'browser.getLatestTab'
        ? tabs[tabs.length - 1]
        : tabs.find((item) => item.active) || tabs[0];
      return { tabId: tab?.id || 0 };
    }

    case 'browser.resolveNewWindowTab': {
      const clientKnown = new Set((message.knownWindowIds || []).map((id) => Number(id)).filter((id) => id > 0));
      const snapshotWindows = await chrome.windows.getAll();
      const known = new Set(snapshotWindows.map((window) => window.id).filter((id) => id > 0));
      for (const id of clientKnown) {
        known.add(id);
      }

      const timeout = message.timeout || 5000;
      const started = Date.now();

      while (Date.now() - started < timeout) {
        const tabs = await chrome.tabs.query({});
        const tab = pickNewWindowTab(tabs, known);
        if (tab) {
          return {
            windowId: tab.windowId,
            tabId: tab.id,
            url: tab.url,
            title: tab.title,
            active: tab.active
          };
        }

        await sleep(100);
      }

      throw new Error(
        'New browser window was not detected within ' + timeout + 'ms. '
        + 'Ensure the F2B Bridge extension is loaded in this Chromium profile.');
    }

    case 'browser.open': {
      const created = await chrome.windows.create({
        url: message.url || 'about:blank',
        focused: true
      });

      const tabs = await chrome.tabs.query({ windowId: created.id });
      const tab = tabs[0];
      if (message.url && tab) {
        await waitForTabComplete(tab.id, message.timeout || 15000);
      }

      return { windowId: created.id, tabId: tab?.id || 0 };
    }

    case 'browser.close': {
      const windowId = message.windowId;
      if (windowId) {
        await chrome.windows.remove(windowId);
      }

      return { windowId: windowId || 0 };
    }

    case 'browser.switchTab': {
      const tabs = await chrome.tabs.query({});
      let target = null;

      if (message.tabId) {
        target = tabs.find((tab) => tab.id === message.tabId);
      } else if (message.index != null) {
        const sorted = tabs.slice().sort((a, b) => a.id - b.id);
        target = sorted[message.index];
      } else {
        const matched = tabs.filter((tab) => tabMatchesSwitchCriteria(tab, message));
        target = pickMatchingTab(matched, message.matchIndex);
      }

      if (!target) {
        throw new Error('No matching tab found to switch.');
      }

      return switchToTab(target);
    }

    case 'browser.getCookies':
    case 'tab.getCookies': {
      const tab = message.tabId ? await chrome.tabs.get(message.tabId) : await resolveTargetTab(message);
      const cookies = await chrome.cookies.getAll({ url: tab.url });
      return {
        cookies: cookies.map((cookie) => ({
          name: cookie.name,
          value: cookie.value,
          domain: cookie.domain,
          path: cookie.path
        }))
      };
    }

    case 'browser.getStorage': {
      message = Object.assign({}, message, { action: 'tab.getStorage' });
      return runPageCommand(message);
    }

    case 'tab.getStorage':
      return runPageCommand(message);

    case 'tab.getInfo': {
      const tab = await chrome.tabs.get(message.tabId);
      return {
        tabId: tab.id,
        url: tab.url,
        title: tab.title,
        active: tab.active,
        index: tab.index,
        isClosed: false
      };
    }

    case 'tab.activate': {
      const tab = await chrome.tabs.get(message.tabId);
      await chrome.tabs.update(tab.id, { active: true });
      await chrome.windows.update(tab.windowId, { focused: true });
      return { tabId: tab.id, active: true };
    }

    case 'tab.navigate': {
      const tab = await chrome.tabs.update(message.tabId, { url: message.url, active: true });
      await waitForTabComplete(tab.id, message.timeout || 15000);
      return { tabId: tab.id };
    }

    case 'tab.back':
      await chrome.scripting.executeScript({
        target: { tabId: message.tabId },
        func: () => { history.back(); }
      });
      return { tabId: message.tabId };

    case 'tab.forward':
      await chrome.scripting.executeScript({
        target: { tabId: message.tabId },
        func: () => { history.forward(); }
      });
      return { tabId: message.tabId };

    case 'tab.refresh':
      await chrome.tabs.reload(message.tabId);
      return { tabId: message.tabId };

    case 'tab.close':
      await chrome.tabs.remove(message.tabId);
      return { tabId: message.tabId };

    case 'tab.takeScreenshot':
    case 'element.takeScreenshot':
      return captureScreenshot(message);

    case 'element.clickForNewTab':
      return clickForNewTab(message);

    case 'element.clickForDownload':
      return clickForDownload(message);

    case 'tab.parallelFindElement':
    case 'element.parallelFindElement':
      message.selectors = (message.selectorSets || []).map((levels) => ({ selectorLevels: levels }));
      return runPageCommand(message);

    case 'tab.runJs':
      return runTabJs(message);

    case 'inspector.pausePick':
    case 'inspector.resumePick':
    case 'inspector.startPickAssist':
    case 'inspector.stopPickAssist':
    case 'inspector.pausePickAssist':
    case 'inspector.resumePickAssist':
    case 'inspector.hoverAtScreenPoint':
    case 'inspector.pickAtScreenPoint':
      return runInspectorQuietPageCommand(message);

    case 'inspector.restartPickAssist':
      return restartInspectorPickAssist(message);

    case 'inspector.startPick':
    case 'inspector.stopPick':
    case 'inspector.buildSelector':
    case 'inspector.describe':
    case 'inspector.highlight':
    case 'inspector.getDomChildren': {
      if (message.action === 'inspector.startPick') {
        return runInspectorPageCommand(message);
      }

      const tab = message.tabId ? await chrome.tabs.get(message.tabId) : await resolveTargetTab(message);
      if (!isInjectableUrl(tab.url)) {
        throw new Error('RESTRICTED_URL:' + (tab.url || ''));
      }

      await chrome.tabs.update(tab.id, { active: true });
      await chrome.windows.update(tab.windowId, { focused: true });
      message.tabTitle = tab.title || '';
      message.tabUrl = tab.url || '';
      return runPageCommand(message);
    }

    default:
      return runPageCommand(message);
  }
}

function executeUserScriptInMainWorld(script, arg) {
  try {
    const source = String(script || '').trim();
    if (/^return\s+document\.title\s*;?\s*$/i.test(source)) {
      return { value: document.title };
    }

    if (/^return\s+document\.URL\s*;?\s*$/i.test(source)) {
      return { value: document.URL };
    }

    const fn = new Function('arg', 'element', 'document', 'window', source || 'return null;');
    return { value: fn(arg, null, document, window) };
  } catch (error) {
    return { error: error.message || String(error) };
  }
}

async function runTabJs(message) {
  const tab = message.tabId ? await chrome.tabs.get(message.tabId) : await resolveTargetTab(message);
  const results = await chrome.scripting.executeScript({
    target: { tabId: tab.id },
    world: 'MAIN',
    func: executeUserScriptInMainWorld,
    args: [message.script, message.arg == null ? null : message.arg]
  });

  const payload = results && results[0] ? results[0].result : null;
  if (!payload) {
    return { tabId: tab.id, result: null };
  }

  if (payload.error) {
    throw new Error(payload.error);
  }

  return { tabId: tab.id, result: payload.value };
}

async function waitForTabComplete(tabId, timeout) {
  const started = Date.now();
  while (Date.now() - started < timeout) {
    const tab = await chrome.tabs.get(tabId);
    if (tab.status === 'complete') {
      return;
    }

    await sleep(100);
  }
}

function pickNewWindowTab(tabs, knownWindowIds) {
  const pool = tabs.filter((tab) => tab.windowId > 0);
  if (pool.length === 0) {
    return null;
  }

  let candidates = pool;
  if (knownWindowIds.size > 0) {
    candidates = pool.filter((tab) => !knownWindowIds.has(tab.windowId));
    if (candidates.length === 0) {
      return null;
    }

    const maxWindowId = Math.max(...candidates.map((tab) => tab.windowId));
    candidates = candidates.filter((tab) => tab.windowId === maxWindowId);
  } else {
    return null;
  }

  return candidates.find((tab) => tab.active) || candidates[candidates.length - 1];
}

async function captureScreenshot(message) {
  const tab = message.tabId ? await chrome.tabs.get(message.tabId) : await resolveTargetTab(message);
  await chrome.windows.update(tab.windowId, { focused: true });
  await chrome.tabs.update(tab.id, { active: true });
  const dataUrl = await chrome.tabs.captureVisibleTab(tab.windowId, { format: 'png' });
  return {
    tabId: tab.id,
    dataUrl: dataUrl,
    path: message.path || ''
  };
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function handleHostCommand(sendFn, rawMessage) {
  let message;
  try {
    message = JSON.parse(rawMessage);
  } catch (error) {
    return;
  }

  if (message.type !== 'command' || !message.id) {
    return;
  }

  const trace = createRpcTrace(message.id);
  trace('handleHostCommand start action=' + (message.action || ''));

  const keepAliveTimer = setInterval(() => {
    chrome.storage.local.get('f2bBridgeInstanceId');
  }, 20000);

  try {
    const data = await executeBridgeCommand(message);
    trace('executeBridgeCommand done, sending result');
    await sendFn(JSON.stringify({
      type: 'result',
      id: message.id,
      success: true,
      data: data || {}
    }));
    trace('result sent');
  } catch (error) {
    trace('executeBridgeCommand failed: ' + (error.message || error));
    await sendFn(JSON.stringify({
      type: 'result',
      id: message.id,
      success: false,
      error: error.message || String(error),
      data: {}
    }));
    trace('error result sent');
  } finally {
    clearInterval(keepAliveTimer);
  }
}
