function refreshStatus() {
  chrome.runtime.sendMessage({ type: 'get_status' }, (response) => {
    const err = chrome.runtime.lastError;
    const status = document.getElementById('status');
    if (err) {
      status.textContent = `状态未知: ${err.message}`;
      return;
    }

    if (response?.connected) {
      status.textContent = response?.recording
        ? '已连接 SpiderAgent（录制中）'
        : '已连接 SpiderAgent';
      return;
    }

    status.textContent = response?.paused
      ? 'SpiderAgent 未运行（已暂停自动重连，可点下方按钮重试）'
      : '正在尝试连接 SpiderAgent...';
  });
}

document.getElementById('reconnect').addEventListener('click', () => {
  chrome.runtime.sendMessage({ type: 'reconnect' }, (response) => {
    const err = chrome.runtime.lastError;
    const status = document.getElementById('status');
    if (err) {
      status.textContent = `重连失败: ${err.message}`;
      return;
    }

    status.textContent = response?.connected
      ? '已连接 SpiderAgent'
      : response?.paused
        ? '已触发重连，请确认 SpiderAgent 主程序正在运行'
        : '正在连接，请稍候...';
  });
});

refreshStatus();
setInterval(refreshStatus, 2000);
