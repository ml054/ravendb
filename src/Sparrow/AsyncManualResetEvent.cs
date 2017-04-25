using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Sparrow
{
    public class AsyncManualResetEvent
    {
        private volatile TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationToken _token;

        public AsyncManualResetEvent()
        {
            _token = CancellationToken.None;		    
        }

        public AsyncManualResetEvent(CancellationToken token)
        {
            token.Register(() => _tcs.TrySetResult(false));
            _token = token;
        }
        public Task<bool> WaitAsync()
        {
            _token.ThrowIfCancellationRequested();
            return _tcs.Task;
        }

        public bool IsSet => _tcs.Task.IsCompleted;

        public Task<bool> WaitAsync(TimeSpan timeout)
        {
            return new FrozenAwaiter(_tcs, this).WaitAsync(timeout);
        }

        public Task<bool> WaitAsync(int timeout)
        {
            return new FrozenAwaiter(_tcs, this).WaitAsync(timeout);
        }

        public FrozenAwaiter GetFrozenAwaiter()
        {
            return new FrozenAwaiter(_tcs, this);
        }

        public struct FrozenAwaiter
        {
            private readonly TaskCompletionSource<bool> _tcs;
            private readonly AsyncManualResetEvent _parent;

            public FrozenAwaiter(TaskCompletionSource<bool> tcs, AsyncManualResetEvent parent)
            {
                _tcs = tcs;
                _parent = parent;
            }

            [Pure]
            public Task<bool> WaitAsync()
            {
                return _tcs.Task;
            }


            [Pure]
            public async Task<bool> WaitAsync(TimeSpan timeout)
            {
                Debug.Assert(timeout != TimeSpan.MaxValue);
                var waitAsync = _tcs.Task;
                _parent._token.ThrowIfCancellationRequested();
                var result = await Task.WhenAny(waitAsync, Task.Delay(timeout, _parent._token));
                if (_parent._token != CancellationToken.None)
                    return result == waitAsync && !_parent._token.IsCancellationRequested;

                return result == waitAsync;
            }

            [Pure]
            public async Task<bool> WaitAsync(int timeout)
            {
                var waitAsync = _tcs.Task;
                _parent._token.ThrowIfCancellationRequested();
                var result = await Task.WhenAny(waitAsync, Task.Delay(timeout, _parent._token));
                if (_parent._token != CancellationToken.None)
                    return result == waitAsync && !_parent._token.IsCancellationRequested;

                return result == waitAsync;
            }
        }


        public void Set()
        {
            _tcs.TrySetResult(true);
        }

        public void SetByAsyncCompletion()
        {
            SetInAsyncManner(_tcs);
        }

        public void Reset(bool force = false)
        {
            while (true)
            {
                var tcs = _tcs;
                if ((tcs.Task.IsCompleted == false && force == false) ||
#pragma warning disable 420
                    Interlocked.CompareExchange(ref _tcs, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously), tcs) == tcs)
#pragma warning restore 420
                    return;
            }
        }

        public void SetAndResetAtomically()
        {
            // we intentionally reset it first to have this operation to behave as atomic
            var previousTcs = _tcs;

            Reset(force: true);
            SetInAsyncManner(previousTcs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetInAsyncManner(TaskCompletionSource<bool> tcs)
        {
            // run the completion asynchronously to ensure that continuations (await WaitAsync()) won't happen as part of a call to TrySetResult
            // http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266920.aspx

            var currentTcs = tcs;

            Task.Factory.StartNew(s => ((TaskCompletionSource<bool>)s).TrySetResult(true),
                currentTcs, CancellationToken.None, TaskCreationOptions.PreferFairness | TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);

            currentTcs.Task.Wait();
        }

        public void SetInAsyncMannerFireAndForget()
        {
            SetInAsyncMannerFireAndForget(_tcs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetInAsyncMannerFireAndForget(TaskCompletionSource<bool> tcs)
        {
            // run the completion asynchronously to ensure that continuations (await WaitAsync()) won't happen as part of a call to TrySetResult
            // http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266920.aspx

            var currentTcs = tcs;

            Task.Factory.StartNew(s => ((TaskCompletionSource<bool>)s).TrySetResult(true),
                currentTcs, CancellationToken.None, TaskCreationOptions.PreferFairness | TaskCreationOptions.RunContinuationsAsynchronously, TaskScheduler.Default);
        }
    }
}
