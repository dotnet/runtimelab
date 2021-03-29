// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Net.Http.LowLevel
{
    /// <summary>
    /// A base connection used for provided <see cref="HttpConnection"/> implementations.
    /// </summary>
    public abstract class HttpBaseConnection : HttpConnection
    {
        private long _creationTicks, _lastUsedTicks;

        internal HttpBaseConnection()
        {
            long curTicks = Environment.TickCount64;
            _creationTicks = curTicks;
            _lastUsedTicks = curTicks;
        }

        internal bool IsExpired(long curTicks, TimeSpan lifetimeLimit, TimeSpan idleLimit)
        {
            return Tools.TimeoutExpired(curTicks, _creationTicks, lifetimeLimit)
                || Tools.TimeoutExpired(curTicks, _lastUsedTicks, idleLimit);
        }

        /// <summary>
        /// Refreshes the last used time of the connection.
        /// </summary>
        /// <param name="curTicks">The number of ticks to set the connection's last used time to.</param>
        protected void RefreshLastUsed(long curTicks)
        {
            _lastUsedTicks = curTicks;
        }
    }
}
