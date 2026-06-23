const scripts = performance.getEntriesByType('resource')
  .filter((item) => item.initiatorType === 'script' || /\.m?js(\?|$)/i.test(item.name))
  .map((item) => ({
    url: item.name,
    loadedBeforeAttach: false,
    timestamp: new Date().toISOString()
  }));

for (const script of scripts) {
  chrome.runtime.sendMessage({
    type: 'script_discovered',
    payload: script
  });
}
