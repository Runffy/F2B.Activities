namespace F2B.Browser.Chromium.Cdp.ConsoleTest
{
    /// <summary>Mirrors TestServer nested-frames fixtures for in-process HttpListener tests.</summary>
    internal static class NestedFramesPages
    {
        internal const string Main = @"<!DOCTYPE html>
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

        internal const string L1 = @"<!DOCTYPE html>
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

        internal const string L2 = @"<!DOCTYPE html>
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

        internal const string L3 = @"<!DOCTYPE html>
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

        internal const string L3V2 = @"<!DOCTYPE html>
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