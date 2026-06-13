using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using F2B.Browser.Chromium.Bridge;

namespace F2B.Bridge.ConsoleTest
{
    internal static class Program
    {
        private const int DemoPort = 19223;

        private static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            BridgeDiagnostics.Log = BridgeFileLog.Write;

            var demoRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "demo", "www");
            var baseUrl = "http://127.0.0.1:" + DemoPort + "/";
            var fileDemoUrl = GetFileDemoUrl(demoRoot);

            var autoStart = HasArg(args, "--auto");
            var httpOnly = HasArg(args, "--http-only");
            var httpSmoke = HasArg(args, "--http-smoke") || httpOnly;

            var testOutputDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "test_output"));
            Directory.CreateDirectory(testOutputDir);

            using (var demoServer = new DemoStaticServer(DemoPort, demoRoot))
            using (var wsServer = new BridgeWebSocketServer())
            using (var host = new BridgeHost(wsServer))
            {
                wsServer.MessageReceived += (sender, e) =>
                {
                    var msg = new JavaScriptSerializer().DeserializeObject(e.Message) as Dictionary<string, object>;
                    if (msg == null || !msg.TryGetValue("type", out var typeObj))
                        return;

                    if (string.Equals(Convert.ToString(typeObj), "trace", StringComparison.OrdinalIgnoreCase))
                    {
                        msg.TryGetValue("step", out var stepObj);
                        BridgeFileLog.Write("TRACE [ext:" + e.InstanceId + "] " + Convert.ToString(stepObj));
                    }
                };

                demoServer.Start();
                wsServer.Start();

                Console.WriteLine("F2B Bridge 全量回归测试 (Console)");
                Console.WriteLine("Demo HTTP  : " + baseUrl);
                Console.WriteLine("Demo FILE  : " + fileDemoUrl);
                Console.WriteLine("Bridge WS  : ws://" + BridgeConstants.DefaultHost + ":" + BridgeConstants.DefaultPort + "/");
                Console.WriteLine("Log file   : " + BridgeFileLog.LogFilePath);
                Console.WriteLine("Output dir : " + testOutputDir);
                Console.WriteLine("扩展要求   : F2B Chromium Bridge v2.5.17+（OpenRPA 全量镜像 + browser.getStorage）");
                Console.WriteLine("默认       : OpenRPA bridge-full-regression 镜像（file://）");
                Console.WriteLine("可选       : --http-smoke  附加 HTTP 冒烟测试");
                Console.WriteLine();

                if (!autoStart)
                {
                    Console.WriteLine("请确保 Chromium 扩展已加载并重载，然后按 Enter 开始...");
                    Console.WriteLine("（勿与 OpenRPA 同时占用 Bridge 端口 19222）");
                    Console.ReadLine();
                }

                var instanceId = WaitForExtension(wsServer, TimeSpan.FromSeconds(60));
                if (instanceId == null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("未检测到扩展连接，测试中止。");
                    Console.ResetColor();
                    return 1;
                }

                Console.WriteLine("已连接扩展 instanceId=" + instanceId);
                Console.WriteLine();

                var client = host.GetClient(instanceId);
                var runner = new FullRegressionTests(host, client, baseUrl, fileDemoUrl, testOutputDir);
                return runner.RunAll(runOpenRpaFull: !httpOnly, runHttpRegression: httpSmoke);
            }
        }

        internal static string GetFileDemoUrl(string demoRoot)
        {
            var indexPath = Path.GetFullPath(Path.Combine(demoRoot, "index.html"));
            if (!File.Exists(indexPath))
                throw new FileNotFoundException("Demo index.html not found: " + indexPath);

            return new Uri(indexPath).AbsoluteUri;
        }

        private static bool HasArg(string[] args, string name)
        {
            return args != null && Array.Exists(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string WaitForExtension(BridgeWebSocketServer server, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var clients = server.GetConnectedClients();
                if (clients.Count > 0)
                    return clients[0].InstanceId;

                Thread.Sleep(500);
            }

            return null;
        }
    }
}
