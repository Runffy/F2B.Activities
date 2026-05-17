#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import argparse
import json
import html
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse


MAIN_PAGE_HTML = """<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Playwright Activities E2E Mock Site</title>
  <style>
    body { font-family: Arial, sans-serif; margin: 18px; line-height: 1.5; background: #fafafa; }
    h1, h2 { margin: 0 0 8px 0; }
    .card { border: 1px solid #d0d0d0; padding: 12px; margin: 10px 0; border-radius: 8px; background: #fff; }
    .row { margin: 8px 0; }
    .children-zone > * { margin-right: 8px; }
    .badge { display: inline-block; padding: 2px 6px; border-radius: 4px; background: #eef3ff; margin-left: 8px; }
    .ok { color: #0a7a2f; font-weight: 600; }
    .warn { color: #9b5c00; font-weight: 600; }
    code { background: #f1f1f1; padding: 2px 6px; border-radius: 4px; }
    #op-log { max-height: 190px; overflow: auto; border: 1px solid #e0e0e0; border-radius: 6px; padding: 8px; margin: 0; }
    #op-log li { font-family: Consolas, monospace; margin: 2px 0; }
    #screenshot-target { width: 280px; height: 90px; border: 1px dashed #777; display: flex; align-items: center; justify-content: center; }
    #toggle-target { padding: 6px; background: #fff8d8; border: 1px solid #ddd2a8; }
  </style>
</head>
<body>
  <h1 id="main-title" data-role="title">Playwright Activities E2E 测试主页</h1>
  <div class="card">
    <h2>操作日志 / 总体判定</h2>
    <div class="row">
      <span id="log-count">log-count:0</span>
      <span id="last-action" class="badge">last:none</span>
      <span id="storage-seed" class="badge">storage-seed:ready</span>
    </div>
    <ol id="op-log"></ol>
  </div>

  <div class="card" id="nav-card">
    <h2>Tab / Navigation</h2>
    <div class="row">
      <a id="link-page2" href="/page2">去 page2（用于 Back/Forward）</a>
    </div>
    <div class="row">
      <a id="link-new-tab" href="/new-tab?name=from-link" target="_blank" rel="noreferrer">新标签打开 /new-tab?name=from-link</a>
      <a id="link-new-tab-disappear" href="/new-tab?name=from-link-disappear" target="_blank" rel="noreferrer">新标签+Disappear校验</a>
      <a id="link-new-tab-appear" href="/new-tab?name=from-link-appear" target="_blank" rel="noreferrer">新标签+Appear校验</a>
      <button id="btn-open-tab-alpha" type="button">打开新标签 alpha</button>
      <button id="btn-open-tab-beta" type="button">打开新标签 beta</button>
    </div>
    <div class="row">
      <div id="newtab-disappear-target">newtab 校验用：点击后消失</div>
      <div id="newtab-appear-host"></div>
    </div>
    <div class="row">
      <button id="btn-go-query" onclick="location.href='/query?from=main&ts=' + Date.now()">跳到 query 页面（NavigateUrl）</button>
    </div>
  </div>

  <div class="card" id="click-card">
    <h2>Element Click / DoubleClick</h2>
    <div class="row">
      <button id="btn-click" data-click-count="0">普通点击按钮</button>
      <span id="click-result">clicked:0</span>
    </div>
    <div class="row">
      <button id="btn-click-disappear">点击后移除目标元素</button>
      <div id="click-disappear-target">我会在点击后消失（用于 CurrentElementDisappear）</div>
      <span id="click-disappear-result">disappear:false</span>
    </div>
    <div class="row">
      <button id="btn-click-appear">点击后出现新元素</button>
      <span id="click-appear-result">appear:false</span>
      <div id="marker-appear-host"></div>
    </div>
    <div class="row">
      <button id="btn-dblclick">双击按钮</button>
      <span id="dblclick-result">dblclicked:false</span>
    </div>
    <div class="row">
      <button id="btn-dblclick-disappear">双击后移除目标元素</button>
      <div id="dbl-disappear-target">dblclick 校验用：双击后消失</div>
      <span id="dbl-disappear-result">dbl-disappear:false</span>
    </div>
    <div class="row">
      <button id="btn-dblclick-appear">双击后出现新元素</button>
      <span id="dbl-appear-result">dbl-appear:false</span>
      <div id="dbl-appear-host"></div>
    </div>
  </div>

  <div class="card" id="download-card">
    <h2>Download</h2>
    <div class="row">
      <a id="link-download" href="/download?name=demo.txt">下载文件（ForDownload）</a>
      <a id="link-download-disappear" href="/download?name=demo-disappear.txt">下载+Disappear校验</a>
      <a id="link-download-appear" href="/download?name=demo-appear.txt">下载+Appear校验</a>
    </div>
    <div class="row">
      <div id="download-disappear-target">download 校验用：点击后消失</div>
      <div id="download-appear-host"></div>
    </div>
  </div>

  <div class="card" id="form-card">
    <h2>Input / Select / Check</h2>
    <div class="row">
      <label for="input-text">输入框：</label>
      <input id="input-text" type="text" value="init-value" />
      <span id="input-result">input:init-value</span>
    </div>
    <div class="row">
      <label for="select-fruit">下拉框：</label>
      <select id="select-fruit">
        <option value="">--请选择--</option>
        <option value="apple">apple</option>
        <option value="banana">banana</option>
        <option value="orange">orange</option>
      </select>
      <span id="select-result">select:none</span>
    </div>
    <div class="row">
      <label><input id="check-accept" type="checkbox" /> 同意协议</label>
      <span id="check-result">checked:false</span>
    </div>
    <div class="row">
      <label for="key-capture">按键捕获：</label>
      <input id="key-capture" type="text" />
      <span id="key-capture-result">key:none</span>
    </div>
  </div>

  <div class="card" id="dom-card">
    <h2>FindElement / Parent / Children / Attribute / Text / Rect / RunJS</h2>
    <div id="parent-box" data-custom="parent-init">
      <div id="child-a" class="child-node">child-a-text</div>
      <div id="child-b" class="child-node" data-item="b">child-b-text</div>
      <div id="child-c" class="child-node"><span id="child-c-inner">child-c-inner-text</span></div>
      <div id="nested-zone" class="child-node">
        <span class="nested-item" data-depth="1">nested-1</span>
        <span class="nested-item" data-depth="2">nested-2</span>
      </div>
    </div>
    <div class="row">
      <div id="attr-target" data-custom="attr-init" data-flag="false">属性测试节点</div>
      <span id="attr-result">attr:init</span>
    </div>
    <div class="row">
      <div id="screenshot-target">截图区域 target</div>
    </div>
    <div class="row">
      <div id="toggle-target">ElementExists 目标（可隐藏）</div>
      <button id="btn-toggle-target" type="button">切换 ElementExists 目标显示状态</button>
    </div>
  </div>

  <div class="card" id="cookie-card">
    <h2>Cookies</h2>
    <div class="row">
      <button id="btn-set-cookie" onclick="document.cookie='client_cookie=ok; path=/'; this.dataset.done='1';">设置页面 Cookie</button>
      <span id="cookie-result">cookie:not-set</span>
    </div>
  </div>

  <div class="card" id="storage-card">
    <h2>Storage</h2>
    <div class="row">
      <button id="btn-set-storage" type="button">写入 local/session storage</button>
      <span id="storage-result">storage:not-set</span>
    </div>
  </div>

  <script>
    (function () {
      var logList = document.getElementById('op-log');
      var logCount = document.getElementById('log-count');
      var lastAction = document.getElementById('last-action');
      var storageSeed = document.getElementById('storage-seed');

      function pushLog(action, detail) {
        var line = new Date().toISOString() + " | " + action + " | " + (detail || "");
        var li = document.createElement('li');
        li.textContent = line;
        logList.appendChild(li);
        logCount.textContent = "log-count:" + logList.children.length;
        lastAction.textContent = "last:" + action;
        lastAction.className = "badge ok";

        try {
          var localLogs = JSON.parse(localStorage.getItem('mock.logs.local') || '[]');
          localLogs.push(line);
          localStorage.setItem('mock.logs.local', JSON.stringify(localLogs));

          var sessionLogs = JSON.parse(sessionStorage.getItem('mock.logs.session') || '[]');
          sessionLogs.push(line);
          sessionStorage.setItem('mock.logs.session', JSON.stringify(sessionLogs));
        } catch (e) {
          // ignore logging errors
        }
      }

      localStorage.setItem('mock.local.seed', 'LOCAL_SEED_V1');
      sessionStorage.setItem('mock.session.seed', 'SESSION_SEED_V1');
      storageSeed.textContent = "storage-seed:LOCAL_SEED_V1|SESSION_SEED_V1";

      var clickBtn = document.getElementById('btn-click');
      var clickResult = document.getElementById('click-result');
      clickBtn.addEventListener('click', function () {
        var n = Number(clickBtn.getAttribute('data-click-count') || '0') + 1;
        clickBtn.setAttribute('data-click-count', String(n));
        clickResult.textContent = 'clicked:' + n;
        pushLog('btn-click', 'count=' + n);
      });

      var clickDisappearBtn = document.getElementById('btn-click-disappear');
      var clickDisappearTarget = document.getElementById('click-disappear-target');
      var clickDisappearResult = document.getElementById('click-disappear-result');
      clickDisappearBtn.addEventListener('click', function () {
        if (clickDisappearTarget && clickDisappearTarget.parentNode) {
          clickDisappearTarget.parentNode.removeChild(clickDisappearTarget);
        }
        clickDisappearResult.textContent = 'disappear:true';
        pushLog('btn-click-disappear', 'removed target');
      });

      var clickAppearBtn = document.getElementById('btn-click-appear');
      var clickAppearResult = document.getElementById('click-appear-result');
      var markerHost = document.getElementById('marker-appear-host');
      clickAppearBtn.addEventListener('click', function () {
        var marker = document.getElementById('marker-appeared');
        if (!marker) {
          marker = document.createElement('div');
          marker.id = 'marker-appeared';
          marker.textContent = 'I appeared';
          marker.setAttribute('data-state', 'appeared');
          markerHost.appendChild(marker);
        }
        clickAppearResult.textContent = 'appear:true';
        pushLog('btn-click-appear', 'marker appeared');
      });

      var dblBtn = document.getElementById('btn-dblclick');
      var dblResult = document.getElementById('dblclick-result');
      dblBtn.addEventListener('dblclick', function () {
        dblResult.textContent = 'dblclicked:true';
        dblBtn.setAttribute('data-dbl', '1');
        pushLog('btn-dblclick', 'ok');
      });

      var dblDisappearBtn = document.getElementById('btn-dblclick-disappear');
      var dblDisappearTarget = document.getElementById('dbl-disappear-target');
      var dblDisappearResult = document.getElementById('dbl-disappear-result');
      dblDisappearBtn.addEventListener('dblclick', function () {
        if (dblDisappearTarget && dblDisappearTarget.parentNode) {
          dblDisappearTarget.parentNode.removeChild(dblDisappearTarget);
        }
        dblDisappearResult.textContent = 'dbl-disappear:true';
        pushLog('btn-dblclick-disappear', 'removed target');
      });

      var dblAppearBtn = document.getElementById('btn-dblclick-appear');
      var dblAppearResult = document.getElementById('dbl-appear-result');
      var dblAppearHost = document.getElementById('dbl-appear-host');
      dblAppearBtn.addEventListener('dblclick', function () {
        var marker = document.getElementById('dbl-marker-appeared');
        if (!marker) {
          marker = document.createElement('div');
          marker.id = 'dbl-marker-appeared';
          marker.textContent = 'dbl marker appeared';
          dblAppearHost.appendChild(marker);
        }
        dblAppearResult.textContent = 'dbl-appear:true';
        pushLog('btn-dblclick-appear', 'marker appeared');
      });

      var inputText = document.getElementById('input-text');
      var inputResult = document.getElementById('input-result');
      inputText.addEventListener('input', function () {
        inputResult.textContent = 'input:' + inputText.value;
      });
      inputText.addEventListener('change', function () {
        pushLog('input-text', inputText.value);
      });

      var selectFruit = document.getElementById('select-fruit');
      var selectResult = document.getElementById('select-result');
      selectFruit.addEventListener('change', function () {
        var text = selectFruit.options[selectFruit.selectedIndex] ? selectFruit.options[selectFruit.selectedIndex].text : '';
        selectResult.textContent = 'select:' + selectFruit.value + '|' + text + '|' + selectFruit.selectedIndex;
        pushLog('select-fruit', selectResult.textContent);
      });

      var checkAccept = document.getElementById('check-accept');
      var checkResult = document.getElementById('check-result');
      checkAccept.addEventListener('change', function () {
        checkResult.textContent = 'checked:' + checkAccept.checked;
        pushLog('check-accept', checkResult.textContent);
      });

      var keyCapture = document.getElementById('key-capture');
      var keyCaptureResult = document.getElementById('key-capture-result');
      keyCapture.addEventListener('keydown', function (e) {
        keyCaptureResult.textContent = 'key:' + e.key;
        pushLog('key-capture', e.key);
      });

      var btnToggleTarget = document.getElementById('btn-toggle-target');
      var toggleTarget = document.getElementById('toggle-target');
      btnToggleTarget.addEventListener('click', function () {
        if (!toggleTarget) return;
        var hidden = toggleTarget.style.display === 'none';
        toggleTarget.style.display = hidden ? '' : 'none';
        pushLog('toggle-target', hidden ? 'show' : 'hide');
      });

      var attrTarget = document.getElementById('attr-target');
      var attrResult = document.getElementById('attr-result');
      var observer = new MutationObserver(function () {
        attrResult.textContent = 'attr:' + (attrTarget.getAttribute('data-custom') || '');
      });
      observer.observe(attrTarget, { attributes: true, attributeFilter: ['data-custom'] });

      var setCookieBtn = document.getElementById('btn-set-cookie');
      var cookieResult = document.getElementById('cookie-result');
      setCookieBtn.addEventListener('click', function () {
        cookieResult.textContent = 'cookie:set';
        pushLog('set-cookie', document.cookie);
      });

      var setStorageBtn = document.getElementById('btn-set-storage');
      var storageResult = document.getElementById('storage-result');
      setStorageBtn.addEventListener('click', function () {
        localStorage.setItem('mock.local.custom', 'L-' + Date.now());
        sessionStorage.setItem('mock.session.custom', 'S-' + Date.now());
        storageResult.textContent = 'storage:set';
        pushLog('set-storage', 'local/session custom set');
      });

      var btnOpenTabAlpha = document.getElementById('btn-open-tab-alpha');
      var btnOpenTabBeta = document.getElementById('btn-open-tab-beta');
      var linkNewTabDisappear = document.getElementById('link-new-tab-disappear');
      var linkNewTabAppear = document.getElementById('link-new-tab-appear');
      var newtabDisappearTarget = document.getElementById('newtab-disappear-target');
      var newtabAppearHost = document.getElementById('newtab-appear-host');
      btnOpenTabAlpha.addEventListener('click', function () {
        window.open('/new-tab?name=alpha', '_blank', 'noopener,noreferrer');
        pushLog('open-tab', 'alpha');
      });
      btnOpenTabBeta.addEventListener('click', function () {
        window.open('/new-tab?name=beta', '_blank', 'noopener,noreferrer');
        pushLog('open-tab', 'beta');
      });

      linkNewTabDisappear.addEventListener('click', function () {
        if (newtabDisappearTarget && newtabDisappearTarget.parentNode) {
          newtabDisappearTarget.parentNode.removeChild(newtabDisappearTarget);
        }
        pushLog('link-new-tab-disappear', 'removed target');
      });

      linkNewTabAppear.addEventListener('click', function () {
        var marker = document.getElementById('newtab-marker-appeared');
        if (!marker) {
          marker = document.createElement('div');
          marker.id = 'newtab-marker-appeared';
          marker.textContent = 'newtab marker appeared';
          newtabAppearHost.appendChild(marker);
        }
        pushLog('link-new-tab-appear', 'marker appeared');
      });

      var linkDownloadDisappear = document.getElementById('link-download-disappear');
      var linkDownloadAppear = document.getElementById('link-download-appear');
      var downloadDisappearTarget = document.getElementById('download-disappear-target');
      var downloadAppearHost = document.getElementById('download-appear-host');
      linkDownloadDisappear.addEventListener('click', function () {
        if (downloadDisappearTarget && downloadDisappearTarget.parentNode) {
          downloadDisappearTarget.parentNode.removeChild(downloadDisappearTarget);
        }
        pushLog('link-download-disappear', 'removed target');
      });

      linkDownloadAppear.addEventListener('click', function () {
        var marker = document.getElementById('download-marker-appeared');
        if (!marker) {
          marker = document.createElement('div');
          marker.id = 'download-marker-appeared';
          marker.textContent = 'download marker appeared';
          downloadAppearHost.appendChild(marker);
        }
        pushLog('link-download-appear', 'marker appeared');
      });

      pushLog('page-load', 'main loaded');
    })();
  </script>
</body>
</html>
"""


PAGE2_HTML = """<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8" />
  <title>Mock Page2</title>
</head>
<body>
  <h1 id="page2-title">这是 Page2（用于 Back / Forward / Refresh）</h1>
  <a id="back-main" href="/">返回主页</a>
  <p id="page2-text">如果你能读到这段文本，说明导航成功。</p>
</body>
</html>
"""


NEW_TAB_HTML_TEMPLATE = """<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8" />
  <title>Mock New Tab - {tab_name}</title>
</head>
<body>
  <h1 id="new-tab-title">新标签页已打开：{tab_name}</h1>
  <p id="new-tab-text" data-source="new-tab">这里用于测试 Click.ForNewTab / SwitchTab / GetLatestTab / GetActivatedTab。</p>
  <div id="new-tab-marker" data-tab-name="{tab_name}">tab-marker:{tab_name}</div>
  <script>
    try {{
      localStorage.setItem('mock.local.last_tab', '{tab_name}');
      sessionStorage.setItem('mock.session.last_tab', '{tab_name}');
    }} catch (e) {{}}
  </script>
</body>
</html>
"""


QUERY_HTML_TEMPLATE = """<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8" />
  <title>Query Page</title>
</head>
<body>
  <h1 id="query-title">Query 页面</h1>
  <pre id="query-data">{query_json}</pre>
  <a id="query-back" href="/">返回主页</a>
</body>
</html>
"""


class MockHandler(BaseHTTPRequestHandler):
    server_version = "MockSite/1.0"

    def do_GET(self):
        parsed = urlparse(self.path)
        path = parsed.path

        if path == "/":
            self._send_html(MAIN_PAGE_HTML, cookies=["server_cookie=home; Path=/"])
            return

        if path == "/page2":
            self._send_html(PAGE2_HTML, cookies=["server_cookie=page2; Path=/"])
            return

        if path == "/new-tab":
            q = parse_qs(parsed.query)
            tab_name = q.get("name", ["new-tab"])[0]
            safe_name = html.escape(tab_name, quote=True)
            self._send_html(
                NEW_TAB_HTML_TEMPLATE.format(tab_name=safe_name),
                cookies=[f"server_cookie_newtab={safe_name}; Path=/"]
            )
            return

        if path == "/query":
            q = parse_qs(parsed.query)
            query_json = json.dumps(q, ensure_ascii=False, indent=2)
            self._send_html(QUERY_HTML_TEMPLATE.format(query_json=query_json))
            return

        if path == "/download":
            q = parse_qs(parsed.query)
            file_name = q.get("name", ["mock-download.txt"])[0]
            content = (
                "This is a mock download file for Playwright tests.\n"
                "You can test Element.Click.ForDownload here.\n"
            ).encode("utf-8")
            self.send_response(HTTPStatus.OK)
            self.send_header("Content-Type", "application/octet-stream")
            self.send_header("Content-Length", str(len(content)))
            self.send_header("Content-Disposition", f'attachment; filename="{file_name}"')
            self.end_headers()
            self.wfile.write(content)
            return

        self.send_error(HTTPStatus.NOT_FOUND, "Not Found")

    def log_message(self, fmt, *args):
        print("[MockSite] " + (fmt % args))

    def _send_html(self, html: str, cookies=None):
        content = html.encode("utf-8")
        self.send_response(HTTPStatus.OK)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(content)))
        if cookies:
            for cookie in cookies:
                self.send_header("Set-Cookie", cookie)
        self.end_headers()
        self.wfile.write(content)


def parse_args():
    parser = argparse.ArgumentParser(description="Playwright Sync 本地测试站")
    parser.add_argument("--host", default="127.0.0.1", help="监听地址，默认 127.0.0.1")
    parser.add_argument("--port", default=8000, type=int, help="监听端口，默认 8000")
    return parser.parse_args()


def main():
    args = parse_args()
    server = ThreadingHTTPServer((args.host, args.port), MockHandler)
    print(f"[MockSite] 启动成功: http://{args.host}:{args.port}")
    print("[MockSite] 按 Ctrl+C 停止")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n[MockSite] 正在停止...")
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
