// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace BindingsGeneration
{
    public class HandlerFactory
    {
        protected readonly ILogger _handlerLogger;

        /// <summary>
        /// Creates a new instance of the <see cref="HandlerFactory"/> class.
        /// </summary>
        /// <param name="handlerLogger">The logger instance.</param>
        public HandlerFactory(ILogger handlerLogger)
        {
            _handlerLogger = handlerLogger;
        }
    }
}
