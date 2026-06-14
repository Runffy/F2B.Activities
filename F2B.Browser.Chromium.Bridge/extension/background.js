importScripts('bridge-trace.js', 'background-commands.js?v=2.5.30');

const BRIDGE_HOST = '127.0.0.1';
const BRIDGE_PORT = 19222;
const CONNECT_TIMEOUT_MS = 5000;
const HEARTBEAT_INTERVAL_MS = 25000;
const WS_URL = `ws://${BRIDGE_HOST}:${BRIDGE_PORT}/`;
const HTTP_BASE = `http://${BRIDGE_HOST}:${BRIDGE_PORT}`;
const STORAGE_KEY_INSTANCE_ID = 'f2bBridgeInstanceId';
const STORAGE_KEY_MANIFEST_VERSION = 'f2bBridgeManifestVersion';
const ALARM_NAME = 'f2bBridgeKeepAlive';

let activeSocket = null;
let reconnectLoopRunning = false;
let heartbeatTimer = null;
let instanceId = null;
let instanceLabel = null;
let shouldStayConnected = true;
let messageChain = Promise.resolve();

const pendingNewWindowIds = new Set();
globalThis.__f2bPendingNewWindowIds = pendingNewWindowIds;

const bootstrapReady = bootstrap();

chrome.runtime.onStartup.addListener(onBootstrap);
chrome.runtime.onInstalled.addListener(onBootstrap);
chrome.alarms.onAlarm.addListener(onAlarm);
chrome.windows.onCreated.addListener(onWindowCreated);
chrome.tabs.onCreated.addListener(onTabCreated);

function onWindowCreated(window) {
  if (window && window.id > 0) {
    pendingNewWindowIds.add(window.id);
  }

  ensureConnected();
}

function onTabCreated() {
  ensureConnected();
}

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
  console.log('[F2B Bridge] bootstrap ready as', instanceId);
  startBridgeConnection();
}

function onBootstrap() {
  ensureAlarm();
  ensureConnected();
}

function onAlarm(alarm) {
  if (alarm.name === ALARM_NAME) {
    ensureConnected();
  }
}

function ensureAlarm() {
  chrome.alarms.create(ALARM_NAME, { periodInMinutes: 1 });
}

function closeActiveSocket() {
  stopHeartbeat();

  if (activeSocket != null) {
    try {
      activeSocket.close();
    } catch (error) {
      // Ignore close failures on half-open sockets.
    }

    activeSocket = null;
  }
}

function ensureConnected() {
  void bootstrapReady.then(() => {
    if (!instanceId) {
      return;
    }

    if (isSocketOpen() || isSocketConnecting() || reconnectLoopRunning) {
      return;
    }

    startBridgeConnection();
  });
}

function isSocketOpen() {
  return activeSocket != null && activeSocket.readyState === WebSocket.OPEN;
}

function isSocketConnecting() {
  return activeSocket != null && activeSocket.readyState === WebSocket.CONNECTING;
}

function startBridgeConnection() {
  shouldStayConnected = true;
  connectWithRetry();
}

function connectWithRetry() {
  if (reconnectLoopRunning || isSocketOpen() || isSocketConnecting()) {
    return;
  }

  attemptConnectLoop();
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

async function attemptConnectLoop() {
  await bootstrapReady;

  if (!instanceId) {
    return;
  }

  reconnectLoopRunning = true;

  try {
    while (shouldStayConnected) {
      if (isSocketOpen()) {
        return;
      }

      const reachable = await isBridgeServerReachable();
      if (!reachable) {
        await sleep(300);
        continue;
      }

      try {
        await connectOnce();
        return;
      } catch (error) {
        console.warn('[F2B Bridge] connect failed, retrying in 300ms:', error.message);
        closeActiveSocket();
        await sleep(300);
      }
    }
  } finally {
    reconnectLoopRunning = false;
  }
}

function connectOnce() {
  return bootstrapReady.then(() => new Promise((resolve, reject) => {
    if (!instanceId) {
      reject(new Error('instanceId not ready'));
      return;
    }

    if (isSocketOpen() || isSocketConnecting()) {
      resolve();
      return;
    }

    let settled = false;
    const ws = new WebSocket(WS_URL);
    activeSocket = ws;

    const timeoutId = setTimeout(() => {
      if (settled) {
        return;
      }

      settled = true;
      ws.close();
      if (activeSocket === ws) {
        activeSocket = null;
      }
      reject(new Error('connection timeout'));
    }, CONNECT_TIMEOUT_MS);

    ws.onopen = () => {
      if (settled) {
        return;
      }

      if (!instanceId) {
        settled = true;
        clearTimeout(timeoutId);
        ws.close();
        if (activeSocket === ws) {
          activeSocket = null;
        }
        reject(new Error('instanceId not ready'));
        return;
      }

      settled = true;
      clearTimeout(timeoutId);
      activeSocket = ws;

      ws.send(JSON.stringify({
        type: 'hello',
        instanceId: instanceId,
        label: instanceLabel
      }));

      console.log('[F2B Bridge] connected to host as', instanceId);

      ws.onmessage = async (event) => {
        await enqueueHostMessage(ws, event.data);
      };

      ws.onclose = () => {
        stopHeartbeat();

        if (activeSocket === ws) {
          activeSocket = null;
        }

        if (shouldStayConnected) {
          connectWithRetry();
        }
      };

      ws.onerror = () => {
        // Browser may still log network failures; retry loop handles recovery.
      };

      startHeartbeat(ws);
      resolve();
    };

    ws.onerror = () => {
      if (settled) {
        return;
      }

      settled = true;
      clearTimeout(timeoutId);
      if (activeSocket === ws) {
        activeSocket = null;
      }
      reject(new Error('websocket error'));
    };
  }));
}

function startHeartbeat(ws) {
  stopHeartbeat();

  heartbeatTimer = setInterval(() => {
    if (ws.readyState !== WebSocket.OPEN) {
      stopHeartbeat();
      return;
    }

    ws.send(JSON.stringify({ type: 'ping', instanceId: instanceId }));
  }, HEARTBEAT_INTERVAL_MS);
}

function stopHeartbeat() {
  if (heartbeatTimer != null) {
    clearInterval(heartbeatTimer);
    heartbeatTimer = null;
  }
}

function enqueueHostMessage(ws, rawMessage) {
  try {
    const message = JSON.parse(rawMessage);
    if (message && message.type === 'command' && message.id) {
      createRpcTrace(message.id)('ws-message received');
    }
  } catch (error) {
    // Ignore non-json payloads.
  }

  messageChain = messageChain
    .then(() => handleHostMessage(ws, rawMessage))
    .catch((error) => {
      console.warn('[F2B Bridge] message handling failed:', error);
    });

  return messageChain;
}

function sendToHost(payload) {
  if (!isSocketOpen()) {
    throw new Error('Bridge WebSocket is not connected.');
  }

  activeSocket.send(typeof payload === 'string' ? payload : JSON.stringify(payload));
  return Promise.resolve();
}

function sendTraceToHost(requestId, step, elapsedMs) {
  if (!isSocketOpen() || !requestId) {
    return;
  }

  try {
    activeSocket.send(JSON.stringify({
      type: 'trace',
      id: requestId,
      step: step,
      elapsedMs: elapsedMs
    }));
  } catch (error) {
    // Ignore trace send failures.
  }
}

globalThis.sendTraceToHost = sendTraceToHost;

async function handleHostMessage(ws, rawMessage) {
  await chrome.storage.local.get(STORAGE_KEY_INSTANCE_ID);

  let message;
  try {
    message = JSON.parse(rawMessage);
  } catch (error) {
    console.warn('[F2B Bridge] invalid message:', rawMessage);
    return;
  }

  if (message.type === 'ping') {
    ws.send(JSON.stringify({ type: 'pong', instanceId: instanceId }));
    return;
  }

  if (message.type === 'pong') {
    return;
  }

  if (message.type !== 'command') {
    return;
  }

  if (message.id) {
    createRpcTrace(message.id)('service worker handleHostMessage');
    await handleHostCommand(sendToHost, rawMessage);
    return;
  }

  if (message.action === 'alert') {
    try {
      await showAlert(message.message || 'Hello from F2B Bridge');
      reportCommandResult('alert', true);
    } catch (error) {
      reportCommandResult('alert', false, error.message || String(error));
    }
  }
}

function reportCommandResult(action, success, error) {
  if (!isSocketOpen()) {
    return;
  }

  activeSocket.send(JSON.stringify({
    type: 'result',
    action: action,
    success: success,
    error: error || ''
  }));
}

function runBridgePageCommandInTab(msg) {
  const pageStarted = performance.now();
  const pageTraceSteps = [];

  globalThis.__f2bPageTrace = function (step) {
    pageTraceSteps.push({
      step: step,
      elapsedMs: Math.round(performance.now() - pageStarted)
    });
  };

  const run = globalThis.__f2bExecuteBridgeCommand;
  if (typeof run !== 'function') {
    throw new Error('Bridge executor is not loaded in tab.');
  }

  globalThis.__f2bPageTrace('page command start: ' + (msg.action || 'unknown'));

  return run(msg)
    .then((data) => ({
      success: true,
      data: data || {},
      pageTrace: pageTraceSteps.slice()
    }))
    .catch((error) => ({
      success: false,
      error: error.message || String(error),
      pageTrace: pageTraceSteps.slice()
    }))
    .finally(() => {
      globalThis.__f2bPageTrace = null;
    });
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function isInjectableUrl(url) {
  return typeof url === 'string' && (
    url.startsWith('http://') ||
    url.startsWith('https://') ||
    url.startsWith('file://')
  );
}

async function findInjectableTab() {
  const activeTabs = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
  const activeTab = activeTabs[0];

  if (activeTab && activeTab.id != null && isInjectableUrl(activeTab.url)) {
    return activeTab;
  }

  const allTabs = await chrome.tabs.query({});
  return allTabs.find((tab) => tab.id != null && isInjectableUrl(tab.url)) || null;
}

async function showAlert(text) {
  const tab = await findInjectableTab();

  if (!tab || tab.id == null) {
    throw new Error('No injectable tab found. Open a normal http/https page in the browser first.');
  }

  await chrome.scripting.executeScript({
    target: { tabId: tab.id },
    func: (alertText) => {
      window.alert(alertText);
    },
    args: [text]
  });
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
