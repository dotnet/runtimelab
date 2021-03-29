// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace System.Net.Http.LowLevel
{
    internal sealed class ResettableValueTaskSource<T> : IValueTaskSource<T>, IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<T> _source;

        public ValueTask<T> Task => new ValueTask<T>(this, _source.Version);
        public ValueTask UntypedTask => new ValueTask(this, _source.Version);

        public void Reset() =>
            _source.Reset();

        public void SetResult(T result) =>
            _source.SetResult(result);

        public void SetException(Exception ex) =>
            _source.SetException(ex);

        T IValueTaskSource<T>.GetResult(short token) =>
            _source.GetResult(token);

        void IValueTaskSource.GetResult(short token) =>
            _source.GetResult(token);

        ValueTaskSourceStatus IValueTaskSource<T>.GetStatus(short token) =>
            _source.GetStatus(token);

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token) =>
            _source.GetStatus(token);

        void IValueTaskSource<T>.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
            _source.OnCompleted(continuation, state, token, flags);

        void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
            _source.OnCompleted(continuation, state, token, flags);
    }
}
