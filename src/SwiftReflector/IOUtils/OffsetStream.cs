// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

namespace SwiftReflector.IOUtils
{
    public class OffsetStream : Stream
    {
        Stream stm;
        long offset;
        public OffsetStream(Stream stm, long offset)
        {
            this.stm = stm;
            this.offset = offset;
            stm.Seek(offset, SeekOrigin.Begin);
        }

        public override bool CanRead
        {
            get
            {
                return stm.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return stm.CanSeek;
            }
        }

        public override bool CanTimeout
        {
            get
            {
                return stm.CanTimeout;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return stm.CanWrite;
            }
        }

        public override void Close()
        {
            stm.Close();
            base.Close();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return stm.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return stm.BeginWrite(buffer, offset, count, callback, state);
        }

        public override System.Threading.Tasks.Task CopyToAsync(Stream destination, int bufferSize, System.Threading.CancellationToken cancellationToken)
        {
            return stm.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                stm.Dispose();
            base.Dispose();
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return stm.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            stm.EndWrite(asyncResult);
        }

        public override void Flush()
        {
            stm.Flush();
        }

        public override System.Threading.Tasks.Task FlushAsync(System.Threading.CancellationToken cancellationToken)
        {
            return stm.FlushAsync(cancellationToken);
        }

        public override long Length
        {
            get
            {
                return stm.Length - offset;
            }
        }

        public override long Position
        {
            get
            {
                return stm.Position - offset;
            }
            set
            {
                stm.Position = value + offset;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return stm.Read(buffer, offset, count);
        }

        public override System.Threading.Tasks.Task<int> ReadAsync(byte[] buffer, int offset, int count, System.Threading.CancellationToken cancellationToken)
        {
            return stm.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override int ReadByte()
        {
            return stm.ReadByte();
        }

        public override int ReadTimeout
        {
            get
            {
                return stm.ReadTimeout;
            }
            set
            {
                stm.ReadTimeout = value;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    return stm.Seek(offset + this.offset, origin) - this.offset;
                case SeekOrigin.Current:
                    return stm.Seek(offset, origin) - this.offset;
                case SeekOrigin.End:
                    return stm.Seek(offset, origin) - this.offset;
                default:
                    return 0;
            }
        }

        public override void SetLength(long value)
        {
            stm.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            stm.Write(buffer, offset, count);
        }
    }
}

