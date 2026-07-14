using System;
using System.IO;
using System.Text;

namespace F2B.Browser.Chromium.Cdp.ConsoleTest
{
    internal static class TestPageGenerator
    {
        internal static string CreateAndGetFileUri()
        {
            var path = Path.Combine(Path.GetTempPath(), "f2b-cdp-test-" + Guid.NewGuid().ToString("N") + ".html");
            File.WriteAllText(path, BuildHtml(), Encoding.UTF8);
            return new Uri(path).AbsoluteUri;
        }

        internal static string CreateNavSecondPageUri()
        {
            var path = Path.Combine(Path.GetTempPath(), "f2b-cdp-nav2-" + Guid.NewGuid().ToString("N") + ".html");
            const string html = "<!DOCTYPE html><html><head><meta charset=\"UTF-8\"><title>F2B CDP Nav Page 2</title></head>" +
                                "<body><h1 id=\"nav-page-2\">Navigation Page 2</h1></body></html>";
            File.WriteAllText(path, html, Encoding.UTF8);
            return new Uri(path).AbsoluteUri;
        }

        private static string BuildHtml()
        {
            return @"<!DOCTYPE html>
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
        }
    }
}
