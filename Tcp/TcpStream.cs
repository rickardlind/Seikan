using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Seikan
{
    public class TcpStream : Stream
    {
        protected TcpClient client;
        protected NetworkStream innerStream;

        public TcpStream(TcpClient client,
                         int readTimeout = Timeout.Infinite,
                         int writeTimeout = Timeout.Infinite)
        {
            this.client = client;
            ReadTimeout = readTimeout;
            WriteTimeout = writeTimeout;
            innerStream = client.GetStream();
        }

        public override bool CanRead { get { return innerStream.CanRead; } }
        public override bool CanSeek { get { return innerStream.CanSeek; } }
        public override bool CanTimeout { get { return true; } }
        public override bool CanWrite { get { return innerStream.CanWrite; } }
        public override long Length { get { return innerStream.Length; } }
        public override long Position
        { 
            get { return innerStream.Position; }
            set { innerStream.Position = value; }
        }
        public override int ReadTimeout { get; set; }
        public override int WriteTimeout { get; set; }

        public override void Flush()
        {
            innerStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return innerStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            innerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            innerStream.Write(buffer, offset, count);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var ar = innerStream.BeginRead(buffer, 0, buffer.Length, null, null);
            int idx = 0, len = 0;

            if (!ar.CompletedSynchronously)
            {
                var handles = new[] { ar.AsyncWaitHandle, cancellationToken.WaitHandle };
                idx = await handles.WaitAny(ReadTimeout);

            }

            switch (idx)
            {
                case 0:
                    len = innerStream.EndRead(ar);
                    break;

                case 1:
                    throw new OperationCanceledException("Read cancelled");

                case WaitHandle.WaitTimeout:
                    throw new TimeoutException("Read timed out");
            }

            return len;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var ar = innerStream.BeginWrite(buffer, offset, count, null, null);
            int idx = 0;

            if (!ar.CompletedSynchronously)
            {
                var handles = new[] { ar.AsyncWaitHandle, cancellationToken.WaitHandle };
                idx = await handles.WaitAny(WriteTimeout);

            }

            switch (idx)
            {
                case 0:
                    innerStream.EndWrite(ar);
                    break;

                case 1:
                    throw new OperationCanceledException("Write cancelled");

                case WaitHandle.WaitTimeout:
                    throw new TimeoutException("Write timed out");
            }
        }

        public void Shutdown(SocketShutdown how)
        {
            client.Client.Shutdown(how);
        }
    }
}
