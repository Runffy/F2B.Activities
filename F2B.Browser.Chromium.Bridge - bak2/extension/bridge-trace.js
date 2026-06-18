function createRpcTrace(requestId) {
  const started = Date.now();
  const shortId = String(requestId || '').slice(0, 8);

  return function trace(step) {
    const elapsed = Date.now() - started;
    const line = `[RPC:${shortId}] ${step} +${elapsed}ms`;
    console.log('[F2B Bridge] ' + line);

    if (requestId && typeof globalThis.sendTraceToHost === 'function') {
      globalThis.sendTraceToHost(requestId, step, elapsed);
    }

    return elapsed;
  };
}

function createPageTrace() {
  const started = performance.now();
  const steps = [];

  const trace = function (step) {
    const elapsed = Math.round(performance.now() - started);
    steps.push({ step: step, elapsedMs: elapsed });
    console.log('[F2B Bridge][page] ' + step + ' +' + elapsed + 'ms');
    return elapsed;
  };

  trace.steps = steps;
  return trace;
}
