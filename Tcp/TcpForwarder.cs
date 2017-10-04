using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Seikan
{
    public class TcpForwarder
    {
        protected readonly IPEndPoint srcEP;
        protected readonly IPEndPoint dstEP;
        protected readonly int maxConnections;
        protected readonly int backlog;

        public TcpForwarder(IPEndPoint srcEP,
                            IPEndPoint dstEP,
                            int maxConnections = 10,
                            int backlog = 4)
        {
            this.srcEP = srcEP;
            this.dstEP = dstEP;
            this.maxConnections = maxConnections;
            this.backlog = backlog;
        }

        public async Task RunAsync(CancellationToken ct,
                                   int timeout = Timeout.Infinite)
        {
            var listener = new TcpListener(srcEP);
            listener.Start(backlog);
            Trace.TraceInformation("Listen: {0}", listener.LocalEndpoint);

            var tasks = new HashSet<Task<TcpClient>> { listener.AcceptTcpClientAsync(ct) };
            bool accepting = true;

            while (tasks.Count > 0)
            {
                var task = await Task.WhenAny(tasks);
                tasks.Remove(task);

                var src = task.Result;

                if (src != null)
                {
                    accepting = false;
                    Trace.TraceInformation("Accept: {0} -> {1}",
                        src.Client.RemoteEndPoint,
                        src.Client.LocalEndPoint);
                    tasks.Add(ForwardAsync(src, dstEP, ct, timeout));
                }

                if (!accepting && tasks.Count < maxConnections && !ct.IsCancellationRequested)
                {
                    tasks.Add(listener.AcceptTcpClientAsync(ct));
                    accepting = true;
                }
            }

            listener.Stop();
        }

        static async Task<TcpClient> ForwardAsync(TcpClient src,
                                                  IPEndPoint dstEP,
                                                  CancellationToken ct,
                                                  int timeout)
        {
            var srcEP = src.Client.RemoteEndPoint;
            var dst = new TcpClient();
            try
            {
                await dst.ConnectAsync(dstEP, ct, timeout);
                Trace.TraceInformation("Connect: {0} -> {1}",
                                       dst.Client.LocalEndPoint, dstEP);

                using (TcpStream ss = new TcpStream(src, timeout, timeout),
                                 ds = new TcpStream(dst, timeout, timeout))
                {
                    await Task.WhenAll(CopyAsync(ss, ds, ct),
                                       CopyAsync(ds, ss, ct));
                }

                Trace.TraceInformation("Closed: {0} -> {1}",
                                       srcEP, dstEP);
            }
            catch (OperationCanceledException ex)
            {
                Trace.TraceWarning(ex.Message);
            }
            catch (TimeoutException ex)
            {
                Trace.TraceWarning(ex.Message);
            }
            catch (Exception ex)
            {
                Trace.TraceError("Forward failed: {0}", ex.Message);
            }

            src.Close();
            dst.Close();
            return null;
        }

        static async Task CopyAsync(TcpStream src,
                                    TcpStream dst,
                                    CancellationToken ct)
        {
            await src.CopyToAsync(dst, 4096, ct);
            dst.Shutdown(SocketShutdown.Send);
        }
    }
}