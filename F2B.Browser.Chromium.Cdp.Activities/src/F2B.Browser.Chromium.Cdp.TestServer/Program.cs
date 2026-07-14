using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace F2B.Browser.Chromium.Cdp.TestServer
{
    internal static class Program
    {
        private const int DefaultPort = 8765;

        private static int Main(string[] args)
        {
            var port = DefaultPort;
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (!int.TryParse(args[i + 1], out port) || port <= 0 || port > 65535)
                    {
                        Console.Error.WriteLine("Invalid port: {0}", args[i + 1]);
                        return 1;
                    }

                    i++;
                }
            }

            var prefix = string.Format("http://127.0.0.1:{0}/", port);
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);

            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                Console.Error.WriteLine("Failed to start HttpListener on {0}: {1}", prefix, ex.Message);
                return 1;
            }

            Console.WriteLine("F2B CDP TestServer listening on {0}", prefix.TrimEnd('/'));
            Console.WriteLine("Press Ctrl+C to stop.");

            using (var stopEvent = new ManualResetEvent(false))
            {
                Console.CancelKeyPress += (_, e) =>
                {
                    e.Cancel = true;
                    stopEvent.Set();
                };

                ThreadPool.QueueUserWorkItem(_ => ServeLoop(listener));

                stopEvent.WaitOne();
            }

            try
            {
                listener.Stop();
                listener.Close();
            }
            catch
            {
            }

            return 0;
        }

        private static void ServeLoop(HttpListener listener)
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                try
                {
                    HandleRequest(context);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Request error: {0}", ex.Message);
                    try
                    {
                        context.Response.StatusCode = 500;
                        context.Response.Close();
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            var path = request.Url.AbsolutePath.TrimEnd('/');
            if (path.Length == 0)
            {
                path = "/";
            }

            if (path == "/" && request.HttpMethod == "GET")
            {
                WriteText(response, TestPageHtml.MainPage, "text/html; charset=utf-8");
                return;
            }

            if (path == "/new-tab-page" && request.HttpMethod == "GET")
            {
                WriteText(response, TestPageHtml.NewTabPage, "text/html; charset=utf-8");
                return;
            }

            if (path == "/api/echo" && request.HttpMethod == "GET")
            {
                WriteText(response, "ok", "text/plain; charset=utf-8");
                return;
            }

            if (path == "/api/echo" && request.HttpMethod == "POST")
            {
                WriteText(response, "posted", "text/plain; charset=utf-8");
                return;
            }

            if (path == "/api/download/sample.txt" && request.HttpMethod == "GET")
            {
                var bytes = Encoding.UTF8.GetBytes(TestPageHtml.DownloadSampleContent);
                response.StatusCode = 200;
                response.ContentType = "text/plain; charset=utf-8";
                response.ContentLength64 = bytes.Length;
                response.AddHeader("Content-Disposition", "attachment; filename=\"sample.txt\"");
                response.OutputStream.Write(bytes, 0, bytes.Length);
                response.Close();
                return;
            }

            if (request.HttpMethod == "GET" && TryServeNestedFrames(path, request, response))
            {
                return;
            }

            response.StatusCode = 404;
            WriteText(response, "Not Found", "text/plain; charset=utf-8");
        }

        private static bool TryServeNestedFrames(string path, HttpListenerRequest request, HttpListenerResponse response)
        {
            string html;
            switch (path)
            {
                case "/nested-frames":
                    html = TestPageHtml.NestedFramesMain;
                    break;
                case "/nested-frames/l1":
                    html = TestPageHtml.NestedFramesL1;
                    break;
                case "/nested-frames/l2":
                    html = TestPageHtml.NestedFramesL2;
                    break;
                case "/nested-frames/l3":
                    SleepQueryDelay(request);
                    html = TestPageHtml.NestedFramesL3;
                    break;
                case "/nested-frames/l3-v2":
                    html = TestPageHtml.NestedFramesL3V2;
                    break;
                default:
                    return false;
            }

            WriteText(response, html, "text/html; charset=utf-8");
            return true;
        }

        private static void SleepQueryDelay(HttpListenerRequest request)
        {
            var raw = request.QueryString["delayMs"];
            int delayMs;
            if (!string.IsNullOrEmpty(raw) && int.TryParse(raw, out delayMs) && delayMs > 0)
            {
                Thread.Sleep(Math.Min(delayMs, 10000));
            }
        }

        private static void WriteText(HttpListenerResponse response, string body, string contentType)
        {
            var bytes = Encoding.UTF8.GetBytes(body ?? string.Empty);
            response.StatusCode = 200;
            response.ContentType = contentType;
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }
    }
}
