using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Seikan
{
    static class Extensions
    {
        public static async Task<TcpClient> AcceptTcpClientAsync(this TcpListener listener,
                                                                 CancellationToken ct)
        {
            var ar = listener.BeginAcceptSocket(null, null);
            int idx = 0;

            if (!ar.CompletedSynchronously)
            {
                var handles = new[] { ar.AsyncWaitHandle, ct.WaitHandle };
                idx = await handles.WaitAny();
            }

            if (idx != 0)
            {
                Debug.WriteLine("Accept cancelled");
                return null;
            }

            return listener.EndAcceptTcpClient(ar);
        }

        public static async Task ConnectAsync(this TcpClient client,
                                              IPEndPoint dstEP,
                                              CancellationToken ct,
                                              int timeout = Timeout.Infinite)
        {
            var ar = client.BeginConnect(dstEP.Address, dstEP.Port, null, null);
            int idx = 0;

            if (!ar.CompletedSynchronously)
            {
                var handles = new[] { ar.AsyncWaitHandle, ct.WaitHandle };
                idx = await handles.WaitAny(timeout);
            }

            switch (idx)
            {
                case 0:
                    client.EndConnect(ar);
                    break;

                case 1:
                    throw new OperationCanceledException("Connect cancelled");

                case WaitHandle.WaitTimeout:
                default:
                    throw new TimeoutException("Connect timed out");
            }
        }

        public static Task<int> WaitAny(this WaitHandle[] handles,
                                        int timeout = Timeout.Infinite)
        {
            return Task.Run(() => WaitHandle.WaitAny(handles, timeout));
        }
    }
}
