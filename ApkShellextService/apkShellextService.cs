using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Threading;
using System.Collections.Generic;

namespace ApkShellext2 {
    class apkShellextService : ServiceBase {
        public apkShellextService() {
            ServiceName = "ApkShellext Service";
            EventLog.Log = "Application";
            
            CanHandlePowerEvent = false;
            CanHandleSessionChangeEvent = false;
            CanPauseAndContinue = false;
            CanShutdown = false;
            CanStop = true;
        }

        static void Main() {
            ServiceBase.Run(new apkShellextService());
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);
        }

        private WebServer ws;

        private static string LocalIPAddress() {
            var card = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()[0];
            var str = card.GetIPProperties().GatewayAddresses;
            
            IPHostEntry host;
            string localIP = "";
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList) {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) {
                    localIP = ip.ToString();
                    break;
                }
            }
            return localIP;
        }

        protected override void OnStart(string[] args) {
            base.OnStart(args);

            string[] prefixes = WebServer.GetPrefixes(LocalIPAddress());

            ws = new WebServer(SendResponse, prefixes);
            ws.Run();
        }

        protected override void OnStop() {
            base.OnStop();
            ws.Stop();
        }

        /// <summary>
        /// OnCustomCommand(): If you need to send a command to your
        ///   service without the need for Remoting or Sockets, use
        ///   this method to do custom methods.
        /// </summary>
        /// <param name="command">Arbitrary Integer between 128 & 256</param>
        protected override void OnCustomCommand(int command) {
            //  A custom command can be sent to a service by using this method:
            //#  int command = 128; //Some Arbitrary number between 128 & 256
            //#  ServiceController sc = new ServiceController("NameOfService");
            //#  sc.ExecuteCommand(command);

            base.OnCustomCommand(command);
        }

        private readonly PackageShareRegistry shares = new PackageShareRegistry();

        public string SendResponse(HttpListenerRequest request) {
            string key = request.QueryString["md5"];
            if (key != null) {
                shares.TryRegister(request.RemoteEndPoint == null ? null : request.RemoteEndPoint.Address,
                    key, request.QueryString["path"]);
                return "";
            }
            return shares.Lookup(request.Url.AbsolutePath.Trim('/'));
        }
    }

    // Registration is local-only; download URLs remain reachable from a phone.
    // Loopback is not user authentication: see README.md before enabling this legacy service.
    internal sealed class PackageShareRegistry {
        private readonly object sync = new object();
        private readonly Dictionary<string, string> paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        internal static bool IsValidKey(string key) {
            if (key == null || key.Length != 32) return false;
            foreach (char c in key)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            return true;
        }

        internal bool TryRegister(IPAddress remote, string key, string path) {
            if (remote == null || !IPAddress.IsLoopback(remote) || !IsValidKey(key) || string.IsNullOrWhiteSpace(path)) return false;
            try {
                if (!Path.IsPathRooted(path)) return false;
                string fullPath = Path.GetFullPath(path);
                string extension = Path.GetExtension(fullPath);
                if (!string.Equals(extension, ".apk", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".ipa", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".appx", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(extension, ".appxbundle", StringComparison.OrdinalIgnoreCase)) return false;
                if (!File.Exists(fullPath) || (File.GetAttributes(fullPath) & (FileAttributes.Directory | FileAttributes.Device | FileAttributes.ReparsePoint)) != 0) return false;
                lock (sync) {
                    if (!paths.ContainsKey(key)) paths.Add(key, fullPath);
                }
                return true;
            } catch (ArgumentException) { return false; }
              catch (NotSupportedException) { return false; }
              catch (IOException) { return false; }
              catch (UnauthorizedAccessException) { return false; }
        }

        internal string Lookup(string key) {
            if (!IsValidKey(key)) return "";
            lock (sync) {
                string path;
                return paths.TryGetValue(key, out path) ? path : "";
            }
        }
    }


    public class WebServer
    {
        private readonly HttpListener _listener = new HttpListener();
        private readonly Func<HttpListenerRequest, string> _responderMethod;
 
        public WebServer(string[] prefixes, Func<HttpListenerRequest, string> method)
        {
            if (!HttpListener.IsSupported)
                throw new NotSupportedException(
                    "Needs Windows XP SP2, Server 2003 or later.");
 
            if (prefixes == null || prefixes.Length == 0)
                throw new ArgumentException("prefixes");
 
            // A responder method is required
            if (method == null)
                throw new ArgumentException("method");
 
            foreach (string s in prefixes)
                _listener.Prefixes.Add(s);
 
            _responderMethod = method;
            _listener.Start();
        }
 
        public WebServer(Func<HttpListenerRequest, string> method, params string[] prefixes)
            : this(prefixes, method) { }
 
        public void Run()
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                Console.WriteLine("Webserver running...");
                try
                {
                    while (_listener.IsListening)
                    {
                        ThreadPool.QueueUserWorkItem((c) =>
                        {
                            var ctx = c as HttpListenerContext;
                            try
                            {
                                string rstr = _responderMethod(ctx.Request);
                                if (rstr != "") {
                                    string filename = Path.GetFileName(rstr);
                                    using (FileStream fs = OpenPackageRead(rstr)) {
                                        long length = fs.Length;
                                        ctx.Response.ContentType = "application/octet-stream";
                                        ctx.Response.AddHeader("Content-Disposition", "attachment; filename=\"" + filename + "\"");
                                        ctx.Response.ContentLength64 = length;
                                        CopyPackage(fs, ctx.Response.OutputStream, length);
                                    }
                                }                         
                            }
                            catch (Exception ex){
                                try { EventLog.WriteEntry("ApkShellext Service", ex.Message, EventLogEntryType.Warning); }
                                catch { } // Reporting a failed download must not escape the worker thread.
                            } // suppress any exceptions
                            finally
                            {
                                // always close the stream
                                ctx.Response.OutputStream.Close();
                            }
                        }, _listener.GetContext());
                    }
                }
                catch { } // suppress any exceptions
            });
        }
 
        internal static string[] GetPrefixes(string localAddress) {
            return new[] { "http://*:42728/", "http://localhost:42728/",
                "http://" + localAddress + ":42728/", "http://127.0.0.1:42728/" };
        }

        internal static FileStream OpenPackageRead(string path) {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }

        internal static void CopyPackage(Stream input, Stream output, long length) {
            if (length < 0) throw new ArgumentOutOfRangeException("length");
            byte[] buffer = new byte[64 * 1024];
            while (length > 0) {
                int count = input.Read(buffer, 0, (int)Math.Min(buffer.Length, length));
                if (count == 0) throw new EndOfStreamException("Package changed during download.");
                output.Write(buffer, 0, count);
                length -= count;
            }
        }

        public void Stop()
        {
            _listener.Stop();
            _listener.Close();
        }
    }
}
