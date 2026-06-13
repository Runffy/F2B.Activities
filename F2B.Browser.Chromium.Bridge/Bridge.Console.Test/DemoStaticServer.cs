using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace F2B.Bridge.ConsoleTest
{
    internal sealed class DemoStaticServer : IDisposable
    {
        private readonly HttpListener _listener = new HttpListener();
        private readonly string _rootPath;
        private CancellationTokenSource _cts;

        public DemoStaticServer(int port, string rootPath)
        {
            Port = port;
            _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
            _listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
        }

        public int Port { get; }

        public string BaseUrl
        {
            get { return "http://127.0.0.1:" + Port + "/"; }
        }

        public void Start()
        {
            if (_listener.IsListening)
                return;

            Directory.CreateDirectory(_rootPath);
            _cts = new CancellationTokenSource();
            _listener.Start();
            Task.Run(() => ListenLoop(_cts.Token));
        }

        public void Dispose()
        {
            _cts?.Cancel();
            if (_listener.IsListening)
                _listener.Stop();
            _listener.Close();
        }

        private async Task ListenLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                _ = Task.Run(() => HandleRequest(context));
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                var path = context.Request.Url.AbsolutePath;
                if (string.IsNullOrEmpty(path) || path == "/")
                    path = "/index.html";

                var relative = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var filePath = Path.GetFullPath(Path.Combine(_rootPath, relative));
                if (!filePath.StartsWith(Path.GetFullPath(_rootPath), StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(filePath))
                {
                    WriteText(context, 404, "text/plain", "Not Found");
                    return;
                }

                var bytes = File.ReadAllBytes(filePath);
                var contentType = GuessContentType(filePath);
                context.Response.StatusCode = 200;
                context.Response.ContentType = contentType;
                context.Response.ContentLength64 = bytes.Length;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                WriteText(context, 500, "text/plain", ex.Message);
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }

        private static void WriteText(HttpListenerContext context, int statusCode, string contentType, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = contentType;
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        }

        private static string GuessContentType(string filePath)
        {
            var ext = Path.GetExtension(filePath)?.ToLowerInvariant();
            switch (ext)
            {
                case ".html":
                case ".htm":
                    return "text/html; charset=utf-8";
                case ".js":
                    return "application/javascript; charset=utf-8";
                case ".css":
                    return "text/css; charset=utf-8";
                case ".json":
                    return "application/json; charset=utf-8";
                default:
                    return "application/octet-stream";
            }
        }
    }
}
