namespace System.IO.StreamSourceGeneration
{
    internal static partial class StreamBoilerplateConstants
    {
        internal const string UsingDirectives =
    @"using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;";

        internal const string CanRead = @"
        public override bool CanRead => true;
";

        internal const string CanSeek = @"
        public override bool CanSeek => true;
";

        internal const string CanWrite = @"
        public override bool CanWrite => true;
";

        internal const string BeginRead = @"
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanRead();
        
            return TaskToApm.Begin(ReadAsync(buffer, offset, count, CancellationToken.None), callback, state);
        }
";

        internal const string BeginWrite = @"
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            ValidateBufferArguments(buffer, offset, count);
            EnsureCanWrite();
        
            return TaskToApm.Begin(WriteAsync(buffer, offset, count, CancellationToken.None), callback, state);
        }
";

        internal const string EndRead = @"
        public override int EndRead(IAsyncResult asyncResult)
        {
            return TaskToApm.End<int>(asyncResult);
        }
";

        internal const string EndWrite = @"
        public override void EndWrite(IAsyncResult asyncResult)
        {
            TaskToApm.End(asyncResult);
        }
";

        // Helpers
        internal const string Helpers = @"
        private void EnsureCanRead()
        {
            if (!CanRead)
            {
                throw new NotSupportedException(""Stream does not support reading."");
            }
        }

        private void EnsureCanWrite()
        {
            if (!CanWrite)
            {
                throw new NotSupportedException(""Stream does not support writing."");
            }
        }

        private void EnsureCanSeek()
        {
            if (!CanSeek)
            {
                throw new NotSupportedException(""Stream does not support seeking."");
            }
        }";
    }
}