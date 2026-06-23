const BRIDGE_HOST = '127.0.0.1';
const BRIDGE_PORT = 17654;
const CONNECT_TIMEOUT_MS = 5000;
const HEARTBEAT_INTERVAL_MS = 25000;
const WS_URL = `ws://${BRIDGE_HOST}:${BRIDGE_PORT}/`;
const HTTP_BASE = `http://${BRIDGE_HOST}:${BRIDGE_PORT}`;
const STORAGE_KEY_INSTANCE_ID = 'spiderAgentInstanceId';
const STORAGE_KEY_MANIFEST_VERSION = 'spiderAgentManifestVersion';
const ALARM_NAME = 'spiderAgentBridgeKeepAlive';
const MAX_UNREACHABLE_BEFORE_PAUSE = 12;

const attachedTabs = new Map();
const requestStore = new Map();

let activeSocket = null;
let reconnectLoopRunning = false;
let reconnectLoopGeneration = 0;
let heartbeatTimer = null;
let instanceId = null;
let instanceLabel = null;
let autoReconnectPaused = false;
let consecutiveUnreachableCount = 0;
let recordingSessionId = null;
let isRecording = false;

const bootstrapReady = bootstrap();

chrome.runtime.onStartup.addListener(onServiceWake);
chrome.runtime.onInstalled.addListener(onServiceWake);
chrome.alarms.onAlarm.addListener(onAlarm);

async function bootstrap() {
  const manifestVersion = chrome.runtime.getManifest().version;
  const storedVersion = await chrome.storage.local.get(STORAGE_KEY_MANIFEST_VERSION);
  if (storedVersion[STORAGE_KEY_MANIFEST_VERSION] !== manifestVersion) {
    await chrome.storage.local.set({ [STORAGE_KEY_MANIFEST_VERSION]: manifestVersion });
    if (storedVersion[STORAGE_KEY_MANIFEST_VERSION]) {
      chrome.runtime.reload();
      return;
    }
  }

  instanceId = await getOrCreateInstanceId();
  instanceLabel = buildInstanceLabel();
  ensureAlarm();
  console.log('[SpiderAgent] bootstrap ready as', instanceId);
  scheduleBridgeConnection('bootstrap');
}

function onServiceWake() {
  ensureAlarm();
  scheduleBridgeConnection('service_wake');
}

function onAlarm(alarm) {
  if (alarm.name === ALARM_NAME) {
    passiveBridgeProbe();
  }
}

function ensureAlarm() {
  chrome.alarms.create(ALARM_NAME, { periodInMinutes: 1 });
}

function closeActiveSocket() {
  stopHeartbeat();

  if (activeSocket != null) {
    try {
      activeSocket.onopen = null;
      activeSocket.onmessage = null;
      activeSocket.onclose = null;
      activeSocket.onerror = null;
      activeSocket.close();
    } catch (error) {
      // Ignore close failures on half-open sockets.
    }

    activeSocket = null;
  }
}

function cancelReconnectLoop() {
  reconnectLoopGeneration += 1;
  reconnectLoopRunning = false;
}

function scheduleBridgeConnection(reason) {
  void bootstrapReady.then(() => {
    if (!instanceId || autoReconnectPaused) {
      return;
    }

    if (isSocketOpen() || isSocketConnecting() || reconnectLoopRunning) {
      return;
    }

    console.log('[SpiderAgent] schedule connect:', reason);
    startReconnectLoop();
  });
}

function passiveBridgeProbe() {
  void bootstrapReady.then(async () => {
    if (!instanceId || isSocketOpen() || isSocketConnecting() || reconnectLoopRunning) {
      return;
    }

    const reachable = await isBridgeServerReachable();
    if (!reachable) {
      return;
    }

    autoReconnectPaused = false;
    consecutiveUnreachableCount = 0;
    scheduleBridgeConnection('passive_probe');
  });
}

function isSocketOpen() {
  return activeSocket != null && activeSocket.readyState === WebSocket.OPEN;
}

function isSocketConnecting() {
  return activeSocket != null && activeSocket.readyState === WebSocket.CONNECTING;
}

async function isBridgeServerReachable() {
  try {
    const response = await fetch(`${HTTP_BASE}/health`, {
      method: 'GET',
      cache: 'no-store'
    });

    return response.ok;
  } catch (error) {
    return false;
  }
}

function unreachableBackoffMs() {
  const steps = [500, 1000, 2000, 5000, 10000, 30000];
  const index = Math.min(consecutiveUnreachableCount, steps.length - 1);
  return steps[index];
}

async function startReconnectLoop() {
  await bootstrapReady;

  if (!instanceId || autoReconnectPaused) {
    return;
  }

  if (reconnectLoopRunning || isSocketOpen() || isSocketConnecting()) {
    return;
  }

  reconnectLoopRunning = true;
  const generation = ++reconnectLoopGeneration;

  try {
    while (!autoReconnectPaused && generation === reconnectLoopGeneration) {
      if (isSocketOpen()) {
        return;
      }

      const reachable = await isBridgeServerReachable();
      if (!reachable) {
        consecutiveUnreachableCount += 1;

        if (consecutiveUnreachableCount >= MAX_UNREACHABLE_BEFORE_PAUSE) {
          autoReconnectPaused = true;
          console.log('[SpiderAgent] host offline, auto-reconnect paused until manual retry or passive probe');
          return;
        }

        await sleep(unreachableBackoffMs());
        continue;
      }

      consecutiveUnreachableCount = 0;

      try {
        await connectOnce(generation);
        return;
      } catch (error) {
        console.warn('[SpiderAgent] connect failed:', error.message);
        closeActiveSocket();
        consecutiveUnreachableCount += 1;

        if (consecutiveUnreachableCount >= MAX_UNREACHABLE_BEFORE_PAUSE) {
          autoReconnectPaused = true;
          console.log('[SpiderAgent] repeated connect failures, auto-reconnect paused');
          return;
        }

        await sleep(unreachableBackoffMs());
      }
    }
  } finally {
    if (generation === reconnectLoopGeneration) {
      reconnectLoopRunning = false;
    }
  }
}

function connectOnce(generation) {
  return bootstrapReady.then(() => new Promise((resolve, reject) => {
    if (!instanceId || autoReconnectPaused || generation !== reconnectLoopGeneration) {
      reject(new Error('connect cancelled'));
      return;
    }

    if (isSocketOpen() || isSocketConnecting()) {
      resolve();
      return;
    }

    let settled = false;

    const finish = (handler) => {
      if (settled) {
        return;
      }

      settled = true;
      handler();
    };

    void isBridgeServerReachable().then((reachable) => {
      if (!reachable) {
        finish(() => reject(new Error('host unreachable')));
        return;
      }

      if (generation !== reconnectLoopGeneration || autoReconnectPaused) {
        finish(() => reject(new Error('connect cancelled')));
        return;
      }

      const ws = new WebSocket(WS_URL);
      activeSocket = ws;

      const timeoutId = setTimeout(() => {
        finish(() => {
          clearTimeout(timeoutId);
          try {
            ws.close();
          } catch (error) {
            // Ignore.
          }

          if (activeSocket === ws) {
            activeSocket = null;
          }

          reject(new Error('connection timeout'));
        });
      }, CONNECT_TIMEOUT_MS);

      ws.onopen = () => {
        finish(() => {
          clearTimeout(timeoutId);
          activeSocket = ws;
          consecutiveUnreachableCount = 0;
          autoReconnectPaused = false;

          ws.send(JSON.stringify({
            type: 'hello',
            instanceId: instanceId,
            label: instanceLabel
          }));

          console.log('[SpiderAgent] connected to host as', instanceId);

          ws.onmessage = (event) => {
            handleHostMessage(event.data);
          };

          ws.onclose = () => {
            stopHeartbeat();

            if (activeSocket === ws) {
              activeSocket = null;
            }

            handleBridgeDisconnected('socket_closed');
          };

          ws.onerror = () => {
            // Reconnect loop handles recovery; avoid surfacing as uncaught errors.
          };

          startHeartbeat(ws);
          resolve();
        });
      };

      ws.onerror = () => {
        finish(() => {
          clearTimeout(timeoutId);
          if (activeSocket === ws) {
            activeSocket = null;
          }
          reject(new Error('websocket error'));
        });
      };
    });
  }));
}

function startHeartbeat(ws) {
  stopHeartbeat();

  heartbeatTimer = setInterval(() => {
    if (ws.readyState !== WebSocket.OPEN) {
      stopHeartbeat();
      return;
    }

    try {
      ws.send(JSON.stringify({ type: 'ping', instanceId: instanceId }));
    } catch (error) {
      console.warn('[SpiderAgent] heartbeat send failed:', error?.message ?? error);
      stopHeartbeat();
    }
  }, HEARTBEAT_INTERVAL_MS);
}

function stopHeartbeat() {
  if (heartbeatTimer != null) {
    clearInterval(heartbeatTimer);
    heartbeatTimer = null;
  }
}

function handleBridgeDisconnected(reason) {
  void cleanupRecordingDueToDisconnect();

  if (autoReconnectPaused) {
    return;
  }

  scheduleBridgeConnection(reason);
}

function pauseAutoReconnect(reason) {
  autoReconnectPaused = true;
  cancelReconnectLoop();
  closeActiveSocket();
  console.log('[SpiderAgent] auto-reconnect paused:', reason);
}

function resumeAutoReconnect(reason) {
  autoReconnectPaused = false;
  consecutiveUnreachableCount = 0;
  cancelReconnectLoop();
  console.log('[SpiderAgent] auto-reconnect resumed:', reason);
  scheduleBridgeConnection(reason);
}

async function cleanupRecordingDueToDisconnect() {
  if (!isRecording && attachedTabs.size === 0) {
    return;
  }

  if (isRecording) {
    isRecording = false;
    recordingSessionId = null;
    removeRecordingListeners();
    console.log('[SpiderAgent] host disconnected, recording stopped locally');
  }

  for (const tabId of [...attachedTabs.keys()]) {
    await detachDebugger(tabId);
  }
}

function sendToHost(message) {
  if (!isSocketOpen()) {
    return false;
  }

  try {
    activeSocket.send(JSON.stringify(message));
    return true;
  } catch (error) {
    console.warn('[SpiderAgent] send failed:', error?.message ?? error);
    if (activeSocket) {
      try {
        activeSocket.close();
      } catch {
      }
      activeSocket = null;
    }
    stopHeartbeat();
    return false;
  }
}

function handleHostMessage(rawMessage) {
  let message;
  try {
    message = JSON.parse(rawMessage);
  } catch (error) {
    console.warn('[SpiderAgent] invalid message:', rawMessage);
    return;
  }

  switch (message.type) {
    case 'pong':
    case 'ping':
      return;
    case 'bridge_connected':
      log('已连接到 SpiderAgent 主程序。');
      return;
    case 'bridge_shutdown':
      pauseAutoReconnect('host_shutdown');
      void cleanupRecordingDueToDisconnect();
      return;
    case 'start_recording':
      startRecording(message.sessionId);
      return;
    case 'stop_recording':
      stopRecording(message.sessionId);
      return;
    default:
      return;
  }
}

async function startRecording(sessionId) {
  recordingSessionId = sessionId;
  isRecording = true;
  installRecordingListeners();

  const tabs = await chrome.tabs.query({});
  for (const tab of tabs) {
    if (tab.id && isAttachableUrl(tab.url)) {
      await attachDebugger(tab.id);
    }
  }

  sendToHost({ type: 'recording_started', sessionId });

  if (attachedTabs.size === 0) {
    log('开始录制。尚未监听任何标签页——请在 Chrome 中打开或切换到 http/https 页面。');
  } else {
    log(`开始录制，已监听 ${attachedTabs.size} 个标签页。新开的标签页会自动加入监听。`);
  }
}

async function stopRecording(sessionId) {
  isRecording = false;
  recordingSessionId = sessionId;
  removeRecordingListeners();

  for (const tabId of [...attachedTabs.keys()]) {
    await detachDebugger(tabId);
  }

  sendToHost({ type: 'recording_stopped', sessionId });
  log('录制已停止。');
}

function isAttachableUrl(url) {
  return typeof url === 'string'
    && (url.startsWith('http://') || url.startsWith('https://'));
}

function installRecordingListeners() {
  if (globalThis.__spiderAgentRecordingListenersInstalled) {
    return;
  }

  chrome.tabs.onCreated.addListener(onRecordingTabCreated);
  chrome.tabs.onUpdated.addListener(onRecordingTabUpdated);
  chrome.tabs.onActivated.addListener(onRecordingTabActivated);
  globalThis.__spiderAgentRecordingListenersInstalled = true;
}

function removeRecordingListeners() {
  if (!globalThis.__spiderAgentRecordingListenersInstalled) {
    return;
  }

  chrome.tabs.onCreated.removeListener(onRecordingTabCreated);
  chrome.tabs.onUpdated.removeListener(onRecordingTabUpdated);
  chrome.tabs.onActivated.removeListener(onRecordingTabActivated);
  globalThis.__spiderAgentRecordingListenersInstalled = false;
}

async function onRecordingTabCreated(tab) {
  if (!isRecording || !tab.id) {
    return;
  }

  if (isAttachableUrl(tab.url)) {
    await attachDebugger(tab.id);
  }
}

async function onRecordingTabUpdated(tabId, changeInfo, tab) {
  if (!isRecording) {
    return;
  }

  const url = changeInfo.url ?? tab?.url;
  if (url && isAttachableUrl(url)) {
    await attachDebugger(tabId);
  }

  if (changeInfo.status === 'complete' && attachedTabs.has(tabId)) {
    await collectExistingScripts(tabId);
  }
}

async function onRecordingTabActivated(activeInfo) {
  if (!isRecording) {
    return;
  }

  await attachDebugger(activeInfo.tabId);
}

function debuggerAttach(tabId) {
  return new Promise((resolve, reject) => {
    chrome.debugger.attach({ tabId }, '1.3', () => {
      if (chrome.runtime.lastError) {
        reject(new Error(chrome.runtime.lastError.message));
      } else {
        resolve();
      }
    });
  });
}

function debuggerSendCommand(tabId, method, params = {}) {
  return new Promise((resolve, reject) => {
    chrome.debugger.sendCommand({ tabId }, method, params, (result) => {
      if (chrome.runtime.lastError) {
        reject(new Error(chrome.runtime.lastError.message));
      } else {
        resolve(result);
      }
    });
  });
}

function debuggerDetach(tabId) {
  return new Promise((resolve, reject) => {
    chrome.debugger.detach({ tabId }, () => {
      if (chrome.runtime.lastError) {
        reject(new Error(chrome.runtime.lastError.message));
      } else {
        resolve();
      }
    });
  });
}

async function attachDebugger(tabId) {
  if (attachedTabs.has(tabId)) {
    return;
  }

  let tab;
  try {
    tab = await chrome.tabs.get(tabId);
  } catch (error) {
    log(`读取标签页 #${tabId} 失败: ${error.message}`);
    return;
  }

  if (!isAttachableUrl(tab.url)) {
    return;
  }

  try {
    await debuggerAttach(tabId);
    await debuggerSendCommand(tabId, 'Network.enable');
    attachedTabs.set(tabId, true);
    log(`已监听标签页 #${tabId}: ${tab.url}`);

    if (!globalThis.__spiderAgentDebuggerListenerInstalled) {
      chrome.debugger.onEvent.addListener(onDebuggerEvent);
      chrome.debugger.onDetach.addListener((source) => {
        attachedTabs.delete(source.tabId);
        if (isRecording && source.tabId) {
          log(`标签页 #${source.tabId} 调试器已断开（可能打开了 DevTools），请关闭 DevTools 或刷新页面。`);
        }
      });
      globalThis.__spiderAgentDebuggerListenerInstalled = true;
    }
  } catch (error) {
    log(`无法监听标签页 #${tabId} (${tab.url}): ${error.message}`);
  }
}

async function detachDebugger(tabId) {
  if (!attachedTabs.has(tabId)) {
    return;
  }

  try {
    await debuggerDetach(tabId);
  } catch {
    // tab may already be closed
  }

  attachedTabs.delete(tabId);
}

function onDebuggerEvent(source, method, params) {
  if (!isRecording || !source.tabId) {
    return;
  }

  const requestId = params.requestId;
  if (method === 'Network.requestWillBeSent') {
    requestStore.set(requestId, {
      id: requestId,
      url: params.request?.url,
      method: params.request?.method,
      resourceType: params.type,
      requestHeadersJson: JSON.stringify(params.request?.headers ?? {}),
      requestBody: params.request?.postData ?? null,
      timestamp: new Date().toISOString(),
      tabId: source.tabId
    });
    return;
  }

  if (method === 'Network.responseReceived') {
    const existing = requestStore.get(requestId) ?? { id: requestId, tabId: source.tabId };
    existing.statusCode = params.response?.status;
    existing.mimeType = params.response?.mimeType;
    existing.responseHeadersJson = JSON.stringify(params.response?.headers ?? {});
    requestStore.set(requestId, existing);
    return;
  }

  if (method === 'Network.loadingFinished') {
    const existing = requestStore.get(requestId);
    if (!existing) {
      return;
    }

    chrome.debugger.sendCommand(
      { tabId: source.tabId },
      'Network.getResponseBody',
      { requestId },
      (body) => {
        if (chrome.runtime.lastError) {
          sendNetworkEvent(existing);
          requestStore.delete(requestId);
          return;
        }

        existing.responseBody = body.body;
        existing.responseBodyIsBase64 = !!body.base64Encoded;
        sendNetworkEvent(existing);
        requestStore.delete(requestId);
      }
    );
  }
}

function sendNetworkEvent(entry) {
  sendToHost({
    type: 'network_event',
    sessionId: recordingSessionId,
    payload: entry
  });
}

async function collectExistingScripts(tabId) {
  try {
    const results = await chrome.scripting.executeScript({
      target: { tabId },
      func: () => performance.getEntriesByType('resource')
        .filter((item) => item.initiatorType === 'script' || /\.m?js(\?|$)/i.test(item.name))
        .map((item) => ({
          url: item.name,
          loadedBeforeAttach: true,
          timestamp: new Date().toISOString()
        }))
    });

    const scripts = results?.[0]?.result ?? [];
    for (const script of scripts) {
      sendToHost({
        type: 'script_discovered',
        sessionId: recordingSessionId,
        payload: {
          id: btoa(unescape(encodeURIComponent(script.url))).slice(0, 16),
          url: script.url,
          loadedBeforeAttach: true,
          timestamp: script.timestamp,
          tabId
        }
      });

      fetchScriptContent(script.url, tabId);
    }
  } catch (error) {
    sendError(`收集已加载 JS 失败: ${error.message}`);
  }
}

async function fetchScriptContent(url, tabId) {
  try {
    const response = await fetch(url, { credentials: 'include' });
    const content = await response.text();
    sendToHost({
      type: 'script_content',
      sessionId: recordingSessionId,
      payload: {
        id: btoa(unescape(encodeURIComponent(url))).slice(0, 16),
        url,
        content,
        loadedBeforeAttach: true,
        timestamp: new Date().toISOString(),
        tabId
      }
    });
  } catch (error) {
    log(`无法拉取脚本 ${url}: ${error.message}`);
  }
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === 'reconnect') {
    resumeAutoReconnect('manual');
    scheduleBridgeConnection('manual_reconnect');
    sendResponse({
      connected: isSocketOpen(),
      paused: autoReconnectPaused
    });
    return true;
  }

  if (message?.type === 'get_status') {
    sendResponse({
      connected: isSocketOpen(),
      paused: autoReconnectPaused,
      recording: isRecording
    });
    return true;
  }

  if (message?.type === 'script_discovered' && isRecording) {
    sendToHost({
      type: 'script_discovered',
      sessionId: recordingSessionId,
      payload: {
        ...message.payload,
        tabId: sender.tab?.id
      }
    });
  }

  return false;
});

function sendError(message) {
  if (!sendToHost({ type: 'error', payload: { message } })) {
    console.warn('[SpiderAgent]', message);
  }
}

function log(message) {
  if (!sendToHost({ type: 'log', payload: { message } })) {
    console.log('[SpiderAgent]', message);
  }
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function getOrCreateInstanceId() {
  const stored = await chrome.storage.local.get(STORAGE_KEY_INSTANCE_ID);
  if (stored[STORAGE_KEY_INSTANCE_ID]) {
    return stored[STORAGE_KEY_INSTANCE_ID];
  }

  const created = createInstanceId();
  await chrome.storage.local.set({ [STORAGE_KEY_INSTANCE_ID]: created });
  return created;
}

function createInstanceId() {
  if (globalThis.crypto && typeof globalThis.crypto.randomUUID === 'function') {
    return globalThis.crypto.randomUUID();
  }

  return 'inst-' + Date.now().toString(36) + '-' + Math.random().toString(36).slice(2, 10);
}

function buildInstanceLabel() {
  const userAgent = navigator.userAgent || 'Chromium';
  const browserName = userAgent.includes('Edg/')
    ? 'Edge'
    : userAgent.includes('Chrome/')
      ? 'Chrome'
      : 'Chromium';

  return browserName + ' / ' + (instanceId || 'unknown').slice(0, 8);
}
