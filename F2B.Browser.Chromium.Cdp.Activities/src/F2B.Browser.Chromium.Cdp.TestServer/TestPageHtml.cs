namespace F2B.Browser.Chromium.Cdp.TestServer
{
    internal static class TestPageHtml
    {
        internal const string MainPage = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<title>F2B CDP Test Page</title>
<style>
  body { font-family: sans-serif; margin: 20px; }
  #scroll-box { width: 200px; height: 80px; overflow: auto; border: 1px solid #ccc; }
  #scroll-inner { height: 400px; background: linear-gradient(#eee, #999); }
  .item { padding: 4px; border-bottom: 1px solid #ddd; }
  #drag-source { width: 60px; height: 30px; background: #4af; cursor: move; }
  #drag-target { width: 120px; height: 60px; border: 2px dashed #888; margin-top: 8px; }
  #styled { color: red; font-size: 14px; }
</style>
</head>
<body>
  <h1 id=""page-title"">F2B CDP Test</h1>

  <input id=""txt-input"" type=""text"" value=""hello"" placeholder=""type here"" />
  <textarea id=""txt-area"">area text</textarea>
  <input id=""chk-box"" type=""checkbox"" />
  <button id=""btn-click"" type=""button"">Click Me</button>
  <span id=""click-result""></span>

  <select id=""sel-single"">
    <option value=""a"">Alpha</option>
    <option value=""b"" selected>Beta</option>
    <option value=""c"">Gamma</option>
  </select>

  <select id=""sel-multi"" multiple size=""3"">
    <option value=""m1"">One</option>
    <option value=""m2"">Two</option>
    <option value=""m3"">Three</option>
    <option value=""m4"">Four</option>
  </select>

  <div id=""container"">
    <div class=""item"" data-role=""row"">Row A</div>
    <div class=""item"" data-role=""row"">Row B</div>
    <div class=""item"" data-role=""row"">Row C</div>
  </div>

  <a id=""lnk-test"" href=""https://example.com/test"">Example Link</a>
  <input id=""file-upload"" type=""file"" />
  <a id=""lnk-download"" href=""/api/download/sample.txt"" download>Download Sample</a>
  <a id=""lnk-newtab"" target=""_blank"" href=""/new-tab-page"">Open New Tab</a>
  <div id=""scroll-box""><div id=""scroll-inner"">Scroll Content</div></div>
  <div id=""drag-source"" draggable=""true"">Drag</div>
  <div id=""drag-target"">Drop Here</div>
  <p id=""styled"">Styled paragraph</p>
  <div id=""hidden-later"" style=""display:none"">Hidden</div>
  <div id=""dynamic-host""></div>

  <iframe id=""inner-frame"" srcdoc=""&lt;html&gt;&lt;body&gt;&lt;input id='frame-input' value='inside-frame' /&gt;&lt;/body&gt;&lt;/html&gt;""></iframe>

<script>
document.getElementById('btn-click').addEventListener('click', function() {
  document.getElementById('click-result').textContent = 'clicked';
});
sessionStorage.setItem('f2b-test', 'session-value');
localStorage.setItem('f2b-test', 'local-value');
</script>
</body>
</html>";

        internal const string NewTabPage = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<title>New Tab Page</title>
</head>
<body>
  <h1 id=""new-tab-title"">New Tab Page</h1>
</body>
</html>";

        internal const string DownloadSampleContent = "F2B CDP download sample file.\r\n";

        /// <summary>Main page: stable L1 iframe; deeper levels are created dynamically.</summary>
        internal const string NestedFramesMain = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<title>Nested Frames Main</title>
</head>
<body>
  <h1 id=""nested-main-title"">Nested Frames</h1>
  <iframe id=""nf-l1"" src=""/nested-frames/l1""></iframe>
</body>
</html>";

        /// <summary>L1: after delay, creates L2 iframe with title only (no id/name).</summary>
        internal const string NestedFramesL1 = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<title>Nested Frames L1</title>
</head>
<body>
  <p id=""l1-marker"">L1</p>
  <div id=""l1-host""></div>
<script>
setTimeout(function() {
  var f = document.createElement('iframe');
  f.setAttribute('title', 'Nested Level 2');
  f.src = '/nested-frames/l2';
  document.getElementById('l1-host').appendChild(f);
}, 400);
</script>
</body>
</html>";

        /// <summary>L2: after delay, creates L3 iframe by id; includes swap control.</summary>
        internal const string NestedFramesL2 = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<title>Nested Frames L2</title>
</head>
<body>
  <p id=""l2-marker"">L2</p>
  <button id=""btn-swap-deep"" type=""button"">Swap L3</button>
  <div id=""l2-host""></div>
<script>
setTimeout(function() {
  var f = document.createElement('iframe');
  f.id = 'nf-l3';
  f.src = '/nested-frames/l3?delayMs=800';
  document.getElementById('l2-host').appendChild(f);
  window.__nfL3 = f;
}, 500);
document.getElementById('btn-swap-deep').addEventListener('click', function() {
  var f = window.__nfL3 || document.getElementById('nf-l3');
  if (f) {
    f.src = '/nested-frames/l3-v2';
  }
});
</script>
</body>
</html>";

        /// <summary>L3: document may be delayed by server; input inserted after short JS delay.</summary>
        internal const string NestedFramesL3 = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<title>Nested Frames L3</title>
</head>
<body>
  <p id=""l3-marker"">L3</p>
  <div id=""l3-host""></div>
<script>
setTimeout(function() {
  var input = document.createElement('input');
  input.id = 'deep-input';
  input.type = 'text';
  input.value = 'nested-ok';
  document.getElementById('l3-host').appendChild(input);
}, 300);
</script>
</body>
</html>";

        /// <summary>L3 after src swap.</summary>
        internal const string NestedFramesL3V2 = @"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<title>Nested Frames L3 v2</title>
</head>
<body>
  <p id=""l3-marker-v2"">L3v2</p>
  <input id=""deep-input-v2"" type=""text"" value=""nested-swapped"" />
</body>
</html>";
    }
}
