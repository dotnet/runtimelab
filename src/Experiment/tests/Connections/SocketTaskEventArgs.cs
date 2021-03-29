using System.Net.Sockets;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace System.Net.Http.LowLevel.Tests.Connections
{
    internal class SocketTaskEventArgs<T> : SocketAsyncEventArgs, IValueTaskSource, IValueTaskSource<T>
    {
        private ManualResetValueTaskSourceCore<T> _taskSource;

        public ValueTask Task => new ValueTask(this, _taskSource.Version);

        public ValueTask<T> GenericTask => new ValueTask<T>(this, _taskSource.Version);

        public SocketTaskEventArgs() : base(unsafeSuppressExecutionContextFlow: true)
        {
        }

        public void Reset() =>
            _taskSource.Reset();

        public void SetResult(T value) =>
            _taskSource.SetResult(value);

        public void SetException(Exception error) =>
            _taskSource.SetException(error);

        void IValueTaskSource.GetResult(short token) =>
            _taskSource.GetResult(token);

        T IValueTaskSource<T>.GetResult(short token) =>
            _taskSource.GetResult(token);

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) =>
            _taskSource.GetStatus(token);

        ValueTaskSourceStatus IValueTaskSource<T>.GetStatus(short token) =>
            _taskSource.GetStatus(token);

        void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
            _taskSource.OnCompleted(continuation, state, token, flags);

        void IValueTaskSource<T>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
            _taskSource.OnCompleted(continuation, state, token, flags);

        protected override void OnCompleted(SocketAsyncEventArgs e)
        {
            SetResult(default!);
        }
    }
}
